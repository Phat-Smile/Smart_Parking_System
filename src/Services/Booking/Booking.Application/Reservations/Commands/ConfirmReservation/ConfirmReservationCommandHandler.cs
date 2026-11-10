using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Payments;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;

namespace ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;

public sealed class ConfirmReservationCommandHandler : IRequestHandler<ConfirmReservationCommand, Result<ConfirmedReservationDto>>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IReservationPaymentClient _paymentClient;
    private readonly IClock _clock;
    private readonly ILogger<ConfirmReservationCommandHandler> _logger;
    private readonly IOutboxWriter _outbox;

    public ConfirmReservationCommandHandler(
        IReservationRepository reservationRepository,
        IReservationPaymentClient paymentClient,
        IClock clock,
        ILogger<ConfirmReservationCommandHandler> logger,
        IOutboxWriter outbox)
    {
        _reservationRepository = reservationRepository;
        _paymentClient = paymentClient;
        _clock = clock;
        _logger = logger;
        _outbox = outbox;
    }

    public async Task<Result<ConfirmedReservationDto>> Handle(
        ConfirmReservationCommand request,
        CancellationToken cancellationToken)
    {
        // ---------------------------------------------------------------------
        // Step a: Load, ownership, Confirmed replay, status, and expiry checks
        // ---------------------------------------------------------------------
        var reservation = await _reservationRepository.FindByIdAsync(request.ReservationId, cancellationToken);
        if (reservation is null)
        {
            _logger.LogWarning("Reservation {ReservationId} not found.", request.ReservationId);
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.NotFound);
        }

        if (reservation.UserId != request.UserId)
        {
            _logger.LogWarning("User {UserId} attempted to confirm reservation {ReservationId} belonging to user {OwnerId}.",
                request.UserId, request.ReservationId, reservation.UserId);
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.AccessDenied);
        }

        if (reservation.Status == ReservationStatus.Confirmed)
        {
            return Result<ConfirmedReservationDto>.Success(BuildConfirmedDto(reservation, reservation.ConfirmedAt ?? _clock.UtcNow));
        }

        // BR: A refunded reservation cannot be confirmed again (F-01, F-08)
        if (reservation.PrepaidOutcome == PrepaidOutcome.Refunded)
        {
            _logger.LogWarning(
                "Reservation {ReservationId} has already been refunded (PrepaidOutcome == Refunded). Cannot confirm.",
                reservation.Id);
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentFailed);
        }

        if (reservation.Status != ReservationStatus.TemporaryHold)
        {
            _logger.LogWarning("Reservation {ReservationId} is in status {Status}, cannot confirm.",
                request.ReservationId, reservation.Status);
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.InvalidStatus);
        }

        var now = _clock.UtcNow;
        if (reservation.TempHoldExpiresAt.HasValue && now > reservation.TempHoldExpiresAt.Value)
        {
            _logger.LogWarning("Reservation {ReservationId} hold expired at {ExpiresAt}, current time {Now}.",
                request.ReservationId, reservation.TempHoldExpiresAt.Value, now);
            try
            {
                reservation.Expire();
                await _reservationRepository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark expired reservation {ReservationId}.", request.ReservationId);
            }

            // An expired hold NEVER calls Payment
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.HoldExpired);
        }

        // ---------------------------------------------------------------------
        // Step b: Gate checks and scheduled validation (US-RES-004)
        // ---------------------------------------------------------------------
        if (reservation.ReservationType == ReservationType.Scheduled)
        {
            if (reservation.PrepaidAmount <= 0m || !IsValidJson(reservation.PriceSnapshot))
            {
                _logger.LogError(
                    "Cannot confirm scheduled reservation {ReservationId} because prepayment state is invalid (PrepaidAmount: {PrepaidAmount}, PriceSnapshot: {PriceSnapshot}).",
                    reservation.Id,
                    reservation.PrepaidAmount,
                    reservation.PriceSnapshot);

                try
                {
                    reservation.Cancel(now, CancelReason.PaymentFailed);
                    await _reservationRepository.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save cancelled state for invalid scheduled reservation {ReservationId}.", reservation.Id);
                }

                return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentFailed);
            }
        }

        // The zero-amount shortcut (confirm without Payment) stays only for HoldWhileDriving (BR-01.c)
        if (reservation.ReservationType == ReservationType.HoldWhileDriving && reservation.PrepaidAmount == 0m)
        {
            return await ExecuteConfirmationAndSaveAsync(reservation, now, isDebited: false, cancellationToken);
        }

        // ---------------------------------------------------------------------
        // Step c: DebitAsync with stored PrepaidAmount (never client amount) and purpose
        // ---------------------------------------------------------------------
        var purpose = reservation.ReservationType == ReservationType.Scheduled
            ? PrepaymentPurpose.Scheduled
            : PrepaymentPurpose.Deposit;

        var debitResult = await _paymentClient.DebitAsync(
            reservation.Id,
            reservation.UserId,
            reservation.LotId,
            (long)reservation.PrepaidAmount,
            purpose,
            cancellationToken);

        // ---------------------------------------------------------------------
        // Step d: Evaluate DebitOutcome
        // ---------------------------------------------------------------------
        bool isDebited = false;
        if (debitResult.IsSuccess)
        {
            switch (debitResult.Value!.Outcome)
            {
                case ReservationDebitOutcome.Succeeded:
                    isDebited = true;
                    break;

                case ReservationDebitOutcome.InsufficientBalance:
                    // Status unchanged, no event
                    return Result<ConfirmedReservationDto>.Failure(ReservationErrors.InsufficientWalletBalance);

                case ReservationDebitOutcome.AlreadyRefunded:
                    try
                    {
                        reservation.Cancel(now, CancelReason.PaymentFailed, PrepaidOutcome.Refunded);
                        await _reservationRepository.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save cancelled state for already refunded reservation {ReservationId}.", reservation.Id);
                    }
                    return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentFailed);

                case ReservationDebitOutcome.WalletNotActive:
                case ReservationDebitOutcome.AmountMismatch:
                    try
                    {
                        reservation.Cancel(now, CancelReason.PaymentFailed);
                        await _reservationRepository.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save cancelled state for reservation {ReservationId}.", reservation.Id);
                    }
                    return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentFailed);

                default:
                    return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentFailed);
            }
        }
        else
        {
            // Transient failure -> GetDebitStateAsync
            var stateResult = await _paymentClient.GetDebitStateAsync(reservation.Id, reservation.UserId, cancellationToken);
            if (stateResult.IsSuccess && stateResult.Value == ReservationDebitState.Debited)
            {
                isDebited = true;
            }
            else
            {
                // Otherwise Failure(ReservationErrors.PaymentUnconfirmed), status unchanged, no refund
                return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentUnconfirmed);
            }
        }

        // ---------------------------------------------------------------------
        // Step e: ReloadAsync and validity check before confirmation
        // ---------------------------------------------------------------------
        if (isDebited)
        {
            await _reservationRepository.ReloadAsync(reservation, cancellationToken);
            if (reservation.Status == ReservationStatus.Confirmed)
            {
                // Already Confirmed -> replay success, no refund
                return Result<ConfirmedReservationDto>.Success(BuildConfirmedDto(reservation, reservation.ConfirmedAt ?? _clock.UtcNow));
            }

            var checkNow = _clock.UtcNow;
            if (reservation.Status != ReservationStatus.TemporaryHold ||
                (reservation.TempHoldExpiresAt.HasValue && checkNow > reservation.TempHoldExpiresAt.Value))
            {
                var reason = reservation.Status == ReservationStatus.Cancelled
                    ? "ReservationCancelled"
                    : "HoldExpiredDuringDebit";

                return await CompensateAfterDebitAsync(reservation.Id, reason, cancellationToken);
            }
        }

        // ---------------------------------------------------------------------
        // Step f & g: Confirm, Enqueue Outbox event, SaveChanges with concurrency check
        // ---------------------------------------------------------------------
        return await ExecuteConfirmationAndSaveAsync(reservation, now, isDebited, cancellationToken);
    }

    private async Task<Result<ConfirmedReservationDto>> ExecuteConfirmationAndSaveAsync(
        Reservation reservation,
        DateTimeOffset now,
        bool isDebited,
        CancellationToken cancellationToken)
    {
        try
        {
            reservation.Confirm(now);
            _outbox.Enqueue(
                new ReservationConfirmedIntegrationEvent(
                    reservation.Id,
                    reservation.UserId,
                    reservation.LotId,
                    reservation.ReservationType.ToString(),
                    reservation.PrepaidAmount,
                    reservation.AppointmentTime,
                    now),
                "booking.reservation-confirmed.v1",
                1,
                correlationId: reservation.Id);

            await _reservationRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException")
        {
            _logger.LogWarning(ex, "Concurrency exception while confirming reservation {ReservationId}. Reloading...", reservation.Id);
            if (isDebited)
            {
                return await CompensateAfterDebitAsync(reservation.Id, "ConcurrencyConflict", cancellationToken);
            }

            _reservationRepository.DiscardPendingChanges();
            var reloaded = await _reservationRepository.FindByIdAsync(reservation.Id, cancellationToken);
            if (reloaded?.Status == ReservationStatus.Confirmed)
            {
                return Result<ConfirmedReservationDto>.Success(BuildConfirmedDto(reloaded, reloaded.ConfirmedAt ?? _clock.UtcNow));
            }

            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.HoldExpired);
        }
        catch (ReservationDomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception confirming reservation {ReservationId}.", reservation.Id);
            if (isDebited)
            {
                return await CompensateAfterDebitAsync(reservation.Id, "DomainValidationFailed", cancellationToken);
            }

            return Result<ConfirmedReservationDto>.Failure(new Error("booking.reservation.transition_failed", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save failure confirming reservation {ReservationId}.", reservation.Id);
            if (isDebited)
            {
                return await CompensateAfterDebitAsync(reservation.Id, "SaveConfirmationFailed", cancellationToken);
            }

            return Result<ConfirmedReservationDto>.Failure(new Error("booking.reservation.save_failed", ex.Message));
        }

        _logger.LogInformation("Reservation {ReservationId} confirmed for user {UserId}.", reservation.Id, reservation.UserId);
        return Result<ConfirmedReservationDto>.Success(BuildConfirmedDto(reservation, reservation.ConfirmedAt ?? now));
    }

    private async Task<Result<ConfirmedReservationDto>> CompensateAfterDebitAsync(
        Guid reservationId,
        string reason,
        CancellationToken cancellationToken)
    {
        // 1) Discard pending changes (e.g. uncommitted Outbox row or in-memory state in DbContext)
        _reservationRepository.DiscardPendingChanges();

        // 2) Reload clean state from DB
        var reservation = await _reservationRepository.FindByIdAsync(reservationId, cancellationToken);
        if (reservation is null)
        {
            _logger.LogError("Reservation {ReservationId} not found during compensation.", reservationId);
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.NotFound);
        }

        // 3) If Status == Confirmed -> return replay success, NO refund
        if (reservation.Status == ReservationStatus.Confirmed)
        {
            return Result<ConfirmedReservationDto>.Success(
                BuildConfirmedDto(reservation, reservation.ConfirmedAt ?? _clock.UtcNow));
        }

        var priorStatus = reservation.Status;
        var now = _clock.UtcNow;

        // 4) RefundAsync(stored PrepaidAmount)
        bool refundSucceeded = false;
        try
        {
            var refundResult = await _paymentClient.RefundAsync(
                reservation.Id,
                reservation.UserId,
                (long)reservation.PrepaidAmount,
                reason,
                cancellationToken);

            if (refundResult.IsSuccess && refundResult.Value == ReservationDebitState.Refunded)
            {
                refundSucceeded = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refund debited amount for reservation {ReservationId}. Worker will reconcile.", reservationId);
        }

        // 5) End the hold: if TemporaryHold -> Expire() when now > TempHoldExpiresAt,
        //    otherwise Cancel(now, CancelReason.PaymentFailed); if the refund returned Refunded -> RecordPrepaidRefunded();
        bool isExpired = false;
        if (reservation.Status == ReservationStatus.TemporaryHold)
        {
            if (reservation.TempHoldExpiresAt.HasValue && now > reservation.TempHoldExpiresAt.Value)
            {
                reservation.Expire();
                isExpired = true;
            }
            else
            {
                reservation.Cancel(now, CancelReason.PaymentFailed);
            }
        }
        else if (reservation.Status == ReservationStatus.Expired)
        {
            isExpired = true;
        }

        if (refundSucceeded)
        {
            reservation.RecordPrepaidRefunded();
        }

        // 6) SaveChanges in a try/catch; on failure log and leave it to the worker.
        try
        {
            await _reservationRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save state during compensation for reservation {ReservationId}. Worker will reconcile.", reservationId);
        }

        // Results: concurrency or save failure -> PaymentUnconfirmed; expired -> HoldExpired; cancelled -> InvalidStatus.
        if (isExpired || reason == "HoldExpiredDuringDebit")
        {
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.HoldExpired);
        }

        if (priorStatus == ReservationStatus.Cancelled || reason == "ReservationCancelled")
        {
            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.InvalidStatus);
        }

        return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentUnconfirmed);
    }

    private static ConfirmedReservationDto BuildConfirmedDto(Reservation reservation, DateTimeOffset confirmedAt)
    {
        var qrCodeToken = $"RES-QR-{reservation.Id.ToString()[..8].ToUpperInvariant()}";
        return new ConfirmedReservationDto(
            reservation.Id,
            reservation.Status.ToString(),
            confirmedAt,
            qrCodeToken,
            reservation.PrepaidAmount);
    }

    private static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
