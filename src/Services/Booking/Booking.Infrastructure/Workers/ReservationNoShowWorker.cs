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
/// Background worker that periodically scans confirmed reservations past the 15-minute grace period
/// and automatically cancels them as NoShow, forfeiting prepaid deposits (US-RES-007).
/// </summary>
public sealed class ReservationNoShowWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReservationNoShowOptions> options,
    ILogger<ReservationNoShowWorker> logger) : BackgroundService
{
    private readonly ReservationNoShowOptions _options = options.Value;

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
                    await ProcessNoShowsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Unexpected error in reservation no-show worker cycle.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful application shutdown.
        }
    }

    /// <summary>
    /// Executes a single pass to cancel eligible no-show reservations.
    /// Each reservation is processed in its own DI scope to guarantee failure isolation.
    /// </summary>
    public async Task<int> ProcessNoShowsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Reservation> candidates;
        await using (var initialScope = scopeFactory.CreateAsyncScope())
        {
            var repository = initialScope.ServiceProvider.GetRequiredService<IReservationRepository>();
            var clock = initialScope.ServiceProvider.GetRequiredService<IClock>();
            var now = clock.UtcNow;
            var batchSize = _options.BatchSize > 0 ? _options.BatchSize : 50;
            candidates = await repository.ListNoShowCandidatesAsync(now, batchSize, cancellationToken);
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
                var graceThreshold = candidate.AppointmentTime.AddMinutes(Reservation.NoShowGracePeriodMinutes + Reservation.TechnicalToleranceMinutes);
                if (now < graceThreshold)
                {
                    continue;
                }

                candidate.Cancel(now, CancelReason.NoShow, candidate.PrepaidAmount > 0 ? PrepaidOutcome.Forfeited : null);

                outbox.Enqueue(
                    new ReservationCancelledIntegrationEvent(
                        candidate.Id,
                        candidate.UserId,
                        candidate.LotId,
                        CancelReason.NoShow.ToString(),
                        refundAmount: 0m,
                        now),
                    "booking.reservation-cancelled.v1",
                    1,
                    correlationId: candidate.Id);

                await repository.SaveChangesAsync(cancellationToken);
                processedCount++;

                logger.LogInformation(
                    "Reservation {ReservationId} (User {UserId}, Lot {LotId}) marked as NoShow after grace period expired at {GraceExpiry}.",
                    candidate.Id, candidate.UserId, candidate.LotId, graceThreshold);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process NoShow for reservation candidate {ReservationId}.", candidateStub.Id);
            }
        }

        return processedCount;
    }
}
