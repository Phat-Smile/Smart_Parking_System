using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;

namespace ParkingSystem.Booking.Infrastructure.Workers;

/// <summary>
/// Background worker that periodically scans confirmed reservations to dispatch arrival reminders:
/// 1. 30 minutes before scheduled arrival time (skipped if created less than 30 minutes before arrival).
/// 2. 5 minutes before no-show cutoff (cutoff is arrival + 16 minutes).
/// Each reminder milestone is recorded directly on the Reservation entity (ArrivalReminderSentAtUtc, NoShowWarningSentAtUtc)
/// and committed atomically with the Outbox message in a single database transaction.
/// </summary>
public sealed class ReservationArrivalReminderWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReservationArrivalReminderOptions> options,
    ILogger<ReservationArrivalReminderWorker> logger) : BackgroundService
{
    private readonly ReservationArrivalReminderOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _options.IntervalSeconds > 0 ? _options.IntervalSeconds : 60;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        try
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessRemindersAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Unexpected error in reservation arrival reminder worker cycle.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful application shutdown.
        }
    }

    /// <summary>
    /// Executes a single pass to evaluate confirmed reservations and send eligible reminders.
    /// Each reservation is processed in its own DI scope to guarantee failure isolation.
    /// Entity milestone timestamps and outbox records are saved in the same transaction.
    /// </summary>
    public async Task<int> ProcessRemindersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Reservation> candidates;
        await using (var initialScope = scopeFactory.CreateAsyncScope())
        {
            var repository = initialScope.ServiceProvider.GetRequiredService<IReservationRepository>();
            var clock = initialScope.ServiceProvider.GetRequiredService<IClock>();
            var now = clock.UtcNow;
            var batchSize = _options.BatchSize > 0 ? _options.BatchSize : 50;
            candidates = await repository.ListArrivalReminderCandidatesAsync(now, batchSize, cancellationToken);
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        int processedCount = 0;
        foreach (var candidateStub in candidates)
        {
            await using var itemScope = scopeFactory.CreateAsyncScope();
            var repository = itemScope.ServiceProvider.GetRequiredService<IReservationRepository>();
            var clock = itemScope.ServiceProvider.GetRequiredService<IClock>();
            var outbox = itemScope.ServiceProvider.GetRequiredService<IOutboxWriter>();

            try
            {
                var candidate = await repository.FindByIdAsync(candidateStub.Id, cancellationToken);
                if (candidate is null || candidate.Status != ReservationStatus.Confirmed)
                {
                    continue;
                }

                var now = clock.UtcNow;
                var cutoff = Reservation.GetNoShowCutoff(candidate.AppointmentTime);
                var maskedPlate = MaskPlateNumber(candidate.PlateNumber);
                bool updated = false;

                // Milestone 1: 30 minutes before arrival
                // Rule: Skip if created less than 30 minutes before arrival
                var createdTooClose = (candidate.AppointmentTime - candidate.CreatedAt) < TimeSpan.FromMinutes(30);
                var thirtyMinMark = candidate.AppointmentTime.AddMinutes(-30);

                if (!createdTooClose && now >= thirtyMinMark && now < candidate.AppointmentTime)
                {
                    if (candidate.ArrivalReminderSentAtUtc == null && candidate.MarkArrivalReminderSent(now.UtcDateTime))
                    {
                        var reminderEvent = new ReservationArrivalReminderIntegrationEvent(
                            accountId: candidate.UserId,
                            reservationId: candidate.Id,
                            lotId: candidate.LotId,
                            reminderType: ReservationArrivalReminderTypes.ThirtyMinutesBeforeArrival,
                            scheduledArrivalTimeUtc: candidate.AppointmentTime.UtcDateTime,
                            cutoffTimeUtc: cutoff.UtcDateTime,
                            maskedPlateNumber: maskedPlate);

                        outbox.Enqueue(
                            reminderEvent,
                            "booking.reservation-arrival-reminder.v1",
                            1,
                            correlationId: candidate.Id);

                        updated = true;
                        processedCount++;

                        logger.LogInformation(
                            "Dispatched ThirtyMinutesBeforeArrival reminder for reservation {ReservationId} (Account {AccountId}, Lot {LotId}).",
                            candidate.Id, candidate.UserId, candidate.LotId);
                    }
                }

                // Milestone 2: 5 minutes before no-show cutoff
                var fiveMinBeforeCutoff = cutoff.AddMinutes(-5);

                if (now >= fiveMinBeforeCutoff && now < cutoff)
                {
                    if (candidate.NoShowWarningSentAtUtc == null && candidate.MarkNoShowWarningSent(now.UtcDateTime))
                    {
                        var reminderEvent = new ReservationArrivalReminderIntegrationEvent(
                            accountId: candidate.UserId,
                            reservationId: candidate.Id,
                            lotId: candidate.LotId,
                            reminderType: ReservationArrivalReminderTypes.FiveMinutesBeforeNoShow,
                            scheduledArrivalTimeUtc: candidate.AppointmentTime.UtcDateTime,
                            cutoffTimeUtc: cutoff.UtcDateTime,
                            maskedPlateNumber: maskedPlate);

                        outbox.Enqueue(
                            reminderEvent,
                            "booking.reservation-arrival-reminder.v1",
                            1,
                            correlationId: candidate.Id);

                        updated = true;
                        processedCount++;

                        logger.LogInformation(
                            "Dispatched FiveMinutesBeforeNoShow reminder for reservation {ReservationId} (Account {AccountId}, Lot {LotId}).",
                            candidate.Id, candidate.UserId, candidate.LotId);
                    }
                }

                if (updated)
                {
                    await repository.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process arrival reminder for reservation candidate {ReservationId}.", candidateStub.Id);
            }
        }

        return processedCount;
    }

    public static string? MaskPlateNumber(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return null;

        var trimmed = plateNumber.Trim();
        if (trimmed.Length <= 4)
            return "****";

        var prefix = trimmed[..3];
        var suffix = trimmed[^2..];
        return $"{prefix}***{suffix}";
    }
}
