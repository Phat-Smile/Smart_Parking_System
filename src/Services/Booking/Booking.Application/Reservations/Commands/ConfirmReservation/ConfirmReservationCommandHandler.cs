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
    private static readonly EventId PaymentNotAvailableEventId = new(1001, "PaymentNotAvailable");

    private readonly IReservationRepository _reservationRepository;
    private readonly IReservationPaymentClient _paymentClient;
    private readonly IClock _clock;
    private readonly ILogger<ConfirmReservationCommandHandler> _logger;
    private readonly IOutboxWriter? _outbox;

    public ConfirmReservationCommandHandler(
        IReservationRepository reservationRepository,
        IReservationPaymentClient paymentClient,
        IClock clock,
        ILogger<ConfirmReservationCommandHandler> logger,
        IOutboxWriter? outbox = null)
    {
        _reservationRepository = reservationRepository;
        _paymentClient = paymentClient;
        _clock = clock;
        _logger = logger;
        _outbox = outbox;
    }

    public ConfirmReservationCommandHandler(
        IReservationRepository reservationRepository,
        IClock clock,
        ILogger<ConfirmReservationCommandHandler> logger,
        IOutboxWriter? outbox = null)
        : this(reservationRepository, new UnconfiguredReservationPaymentClient(), clock, logger, outbox)
    {
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
        // Step b: Gate checks
        // ---------------------------------------------------------------------
        if (reservation.ReservationType == ReservationType.Scheduled)
        {
            // Scheduled reservations keep the existing PaymentNotAvailable gate (US-RES-004 is out of scope)
            _logger.LogError(
                PaymentNotAvailableEventId,
                "Cannot confirm scheduled reservation {ReservationId} because payment function is unavailable. UserId: {UserId}, LotId: {LotId}, ReservationType: {ReservationType}, Status: {Status}, PrepaidAmount: {PrepaidAmount}, TempHoldExpiresAt: {TempHoldExpiresAt}, Reason: ScheduledPaymentNotImplemented.",
                reservation.Id,
                reservation.UserId,
                reservation.LotId,
                reservation.ReservationType,
                reservation.Status,
                reservation.PrepaidAmount,
                reservation.TempHoldExpiresAt);

            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentNotAvailable);
        }

        // HoldWhileDriving with PrepaidAmount == 0 -> step f
        if (reservation.PrepaidAmount == 0m)
        {
            return await ExecuteConfirmationAndSaveAsync(reservation, now, isDebited: false, cancellationToken);
        }

        if (_paymentClient is UnconfiguredReservationPaymentClient)
        {
            _logger.LogError(
                PaymentNotAvailableEventId,
                "Cannot confirm reservation {ReservationId} because payment function is unavailable. UserId: {UserId}, LotId: {LotId}, ReservationType: {ReservationType}, Status: {Status}, PrepaidAmount: {PrepaidAmount}, TempHoldExpiresAt: {TempHoldExpiresAt}, Reason: PaymentRequired.",
                reservation.Id,
                reservation.UserId,
                reservation.LotId,
                reservation.ReservationType,
                reservation.Status,
                reservation.PrepaidAmount,
                reservation.TempHoldExpiresAt);

            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentNotAvailable);
        }

        // ---------------------------------------------------------------------
        // Step c: DebitAsync with stored PrepaidAmount (never client amount)
        // ---------------------------------------------------------------------
        var debitResult = await _paymentClient.DebitAsync(
            reservation.Id,
            reservation.UserId,
            reservation.LotId,
            (long)reservation.PrepaidAmount,
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
                var reason = reservation.Status == ReservationStatus.Cancelled ? "ReservationCancelled" : "HoldExpiredDuringDebit";
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
                        reservation.RecordPrepaidRefunded();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refund debited amount for reservation {ReservationId}. Worker will reconcile.", reservation.Id);
                }

                if (reservation.Status == ReservationStatus.TemporaryHold)
                {
                    reservation.Expire();
                }

                try
                {
                    await _reservationRepository.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save state for reservation {ReservationId}.", reservation.Id);
                }

                return Result<ConfirmedReservationDto>.Failure(
                    reservation.Status == ReservationStatus.Cancelled
                        ? ReservationErrors.InvalidStatus
                        : ReservationErrors.HoldExpired);
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
            _outbox?.Enqueue(
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
            await _reservationRepository.ReloadAsync(reservation, cancellationToken);
            if (reservation.Status == ReservationStatus.Confirmed)
            {
                // Confirmed -> replay success with NO refund
                return Result<ConfirmedReservationDto>.Success(BuildConfirmedDto(reservation, reservation.ConfirmedAt ?? _clock.UtcNow));
            }

            // Otherwise refund
            if (isDebited)
            {
                try
                {
                    var refundResult = await _paymentClient.RefundAsync(
                        reservation.Id,
                        reservation.UserId,
                        (long)reservation.PrepaidAmount,
                        "ConcurrencyConflict",
                        cancellationToken);

                    if (refundResult.IsSuccess && refundResult.Value == ReservationDebitState.Refunded)
                    {
                        reservation.RecordPrepaidRefunded();
                        await _reservationRepository.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception rex)
                {
                    _logger.LogError(rex, "Failed to refund debited amount after concurrency conflict on reservation {ReservationId}. Worker will reconcile.", reservation.Id);
                }
            }

            return Result<ConfirmedReservationDto>.Failure(ReservationErrors.HoldExpired);
        }
        catch (ReservationDomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception confirming reservation {ReservationId}.", reservation.Id);
            return Result<ConfirmedReservationDto>.Failure(new Error("booking.reservation.transition_failed", ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save failure confirming reservation {ReservationId}.", reservation.Id);
            if (isDebited)
            {
                try
                {
                    var refundResult = await _paymentClient.RefundAsync(
                        reservation.Id,
                        reservation.UserId,
                        (long)reservation.PrepaidAmount,
                        "SaveConfirmationFailed",
                        cancellationToken);

                    if (refundResult.IsSuccess && refundResult.Value == ReservationDebitState.Refunded)
                    {
                        reservation.RecordPrepaidRefunded();
                        await _reservationRepository.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception rex)
                {
                    _logger.LogError(rex, "Failed to refund debited amount after save failure on reservation {ReservationId}. Worker will reconcile.", reservation.Id);
                }

                return Result<ConfirmedReservationDto>.Failure(ReservationErrors.PaymentUnconfirmed);
            }

            return Result<ConfirmedReservationDto>.Failure(new Error("booking.reservation.save_failed", ex.Message));
        }

        _logger.LogInformation("Reservation {ReservationId} confirmed for user {UserId}.", reservation.Id, reservation.UserId);
        return Result<ConfirmedReservationDto>.Success(BuildConfirmedDto(reservation, reservation.ConfirmedAt ?? now));
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

    private sealed class UnconfiguredReservationPaymentClient : IReservationPaymentClient
    {
        public Task<Result<ReservationDebitResult>> DebitAsync(Guid reservationId, Guid accountId, Guid lotId, long amountVnd, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitResult>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> GetDebitStateAsync(Guid reservationId, Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable));

        public Task<Result<ReservationDebitState>> RefundAsync(Guid reservationId, Guid accountId, long amountVnd, string reason, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<ReservationDebitState>.Failure(ReservationPaymentErrors.Unavailable));
    }
}
