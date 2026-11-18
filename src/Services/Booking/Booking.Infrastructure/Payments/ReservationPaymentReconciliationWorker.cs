using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Booking.Infrastructure.Payments;

/// <summary>
/// Background worker that periodically reconciles and refunds orphaned or expired reservation payments (US-RES-003, Section 11 Saga, US-RES-006).
/// </summary>
public sealed class ReservationPaymentReconciliationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ReservationPaymentReconciliationOptions> options,
    ILogger<ReservationPaymentReconciliationWorker> logger) : BackgroundService
{
    private readonly ReservationPaymentReconciliationOptions _options = options.Value;

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
                    await ReconcileBatchAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Unexpected error in reservation payment reconciliation worker cycle.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during graceful application shutdown.
        }
    }

    /// <summary>
    /// Executes a single reconciliation pass across eligible candidate reservations.
    /// Each candidate is processed in its own DI scope to guarantee failure isolation.
    /// </summary>
    public async Task<int> ReconcileBatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Reservation> candidates;
        await using (var initialScope = scopeFactory.CreateAsyncScope())
        {
            var repository = initialScope.ServiceProvider.GetRequiredService<IReservationRepository>();
            var clock = initialScope.ServiceProvider.GetRequiredService<IClock>();
            var now = clock.UtcNow;
            var batchSize = _options.BatchSize > 0 ? _options.BatchSize : 50;
            candidates = await repository.ListPaymentReconciliationCandidatesAsync(now, batchSize, cancellationToken);
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
            var paymentClient = itemScope.ServiceProvider.GetRequiredService<IReservationPaymentClient>();

            try
            {
                var candidate = await repository.FindByIdAsync(candidateStub.Id, cancellationToken);
                if (candidate is null)
                {
                    continue;
                }

                // Candidate set 2: Cancelled confirmed reservation with pending refund (US-RES-006 / US-RES-009)
                if (candidate.Status == ReservationStatus.Cancelled &&
                    (candidate.CancelReason == CancelReason.CustomerCancelled ||
                     candidate.CancelReason == CancelReason.NoSuitableSlot) &&
                    candidate.ConfirmedAt != null &&
                    candidate.PrepaidAmount > 0m &&
                    candidate.PrepaidOutcome == null)
                {
                    var reason = candidate.CancelReason == CancelReason.NoSuitableSlot
                        ? "NoSuitableSlot"
                        : "CustomerCancelled";

                    var refundResult = await paymentClient.RefundAsync(
                        candidate.Id,
                        candidate.UserId,
                        (long)candidate.PrepaidAmount,
                        reason,
                        cancellationToken);

                    if (refundResult.IsSuccess && refundResult.Value == ReservationDebitState.Refunded)
                    {
                        candidate.RecordCancellationRefunded();
                        await repository.SaveChangesAsync(cancellationToken);
                        processedCount++;
                    }
                    continue;
                }

                // Candidate set 1: Expire a stale TemporaryHold
                if (candidate.Status == ReservationStatus.TemporaryHold)
                {
                    candidate.Expire();
                }

                var stateResult = await paymentClient.GetDebitStateAsync(candidate.Id, candidate.UserId, cancellationToken);
                if (!stateResult.IsSuccess)
                {
                    logger.LogWarning("Failed to query debit state for candidate reservation {ReservationId}: {Error}",
                        candidate.Id, stateResult.Error!.Message);
                    continue;
                }

                if (stateResult.Value == ReservationDebitState.Debited)
                {
                    var refundResult = await paymentClient.RefundAsync(
                        candidate.Id,
                        candidate.UserId,
                        (long)candidate.PrepaidAmount,
                        "ReconciliationRefund",
                        cancellationToken);

                    if (refundResult.IsSuccess && refundResult.Value == ReservationDebitState.Refunded)
                    {
                        candidate.RecordPrepaidRefunded();
                        await repository.SaveChangesAsync(cancellationToken);
                        processedCount++;
                    }
                }
                else if (stateResult.Value == ReservationDebitState.Refunded)
                {
                    candidate.RecordPrepaidRefunded();
                    await repository.SaveChangesAsync(cancellationToken);
                    processedCount++;
                }
                else if (stateResult.Value == ReservationDebitState.NotDebited)
                {
                    // NOT_DEBITED -> nothing (save Expire if status changed from TemporaryHold)
                    if (candidate.Status == ReservationStatus.Expired)
                    {
                        await repository.SaveChangesAsync(cancellationToken);
                    }
                    processedCount++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failing row must not stop or leak into other rows!
                logger.LogError(ex, "Reconciliation failed for reservation {ReservationId}.", candidateStub.Id);
            }
        }

        return processedCount;
    }
}
