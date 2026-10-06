using MediatR;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Options;
using ParkingSystem.Booking.Application.Reservations.Errors;
using ParkingSystem.Booking.Domain.Enums;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Booking.Application.Reservations.Commands.HoldReservationCapacity;

public sealed class HoldReservationCapacityCommandHandler(
    IReservationRepository reservationRepository,
    IReservationCapacityCoordinator capacityCoordinator,
    IParkingLotCapacityProvider capacityProvider,
    IClock clock,
    IOptions<BookingRulesOptions>? options = null)
    : IRequestHandler<HoldReservationCapacityCommand, Result<ReservationHoldResult>>
{
    private readonly BookingRulesOptions _options = options?.Value ?? new BookingRulesOptions();

    public async Task<Result<ReservationHoldResult>> Handle(
        HoldReservationCapacityCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Idempotency pre-check: if reservation already exists with same parameters, return existing (Test E)
        var existing = await reservationRepository.GetByIdAsync(request.ReservationId, cancellationToken);
        if (existing is not null)
        {
            if (IsMatchingReservation(existing, request))
            {
                return Result<ReservationHoldResult>.Success(MapToResult(existing));
            }

            return Result<ReservationHoldResult>.Failure(BookingErrors.ReservationAlreadyExistsConflict);
        }

        // 2. Validate vehicle group (BR-01.a: only Car can reserve)
        if (request.VehicleGroup != VehicleGroup.Car)
        {
            return Result<ReservationHoldResult>.Failure(BookingErrors.VehicleGroupNotSupported);
        }

        // 3. Resolve parking lot capacity snapshot from provider or request
        var snapshot = request.CapacitySnapshot ??
            await capacityProvider.GetCapacitySnapshotAsync(request.LotId, request.VehicleGroup, request.AppointmentTime, cancellationToken);

        if (snapshot is null)
        {
            return Result<ReservationHoldResult>.Failure(BookingErrors.LotCapacityUnavailable);
        }

        if (!snapshot.BookingEnabled)
        {
            return Result<ReservationHoldResult>.Failure(BookingErrors.BookingDisabled);
        }

        var requestedWindow = ReservationTimeWindow.ForReservation(
            request.ReservationType,
            request.AppointmentTime,
            request.DurationMinutes);

        // 4. Execute capacity evaluation and reservation hold under exclusive capacity lock (US-RES-005, BR-02.c)
        return await capacityCoordinator.ExecuteUnderCapacityLockAsync(
            request.LotId,
            request.VehicleGroup,
            async ct =>
            {
                var utcNow = clock.UtcNow;

                // Idempotency check inside lock to prevent race condition on retry
                var concurrentExisting = await reservationRepository.GetByIdAsync(request.ReservationId, ct);
                if (concurrentExisting is not null)
                {
                    if (IsMatchingReservation(concurrentExisting, request))
                    {
                        return Result<ReservationHoldResult>.Success(MapToResult(concurrentExisting));
                    }

                    return Result<ReservationHoldResult>.Failure(BookingErrors.ReservationAlreadyExistsConflict);
                }

                // US-RES-010 Rule 2: Account active reservation limit (BR-01.f)
                var activeUserReservations = await reservationRepository.GetActiveReservationsByUserIdAsync(
                    request.UserId,
                    utcNow,
                    ct);

                if (!ReservationAbusePolicy.CanAccountCreateReservation(activeUserReservations.Count, _options.MaxActiveReservationsPerAccount))
                {
                    return Result<ReservationHoldResult>.Failure(BookingErrors.AccountActiveLimitExceeded);
                }

                // US-RES-010 Rule 1: One active reservation per plate per overlapping time window (BR-01.f)
                var activePlateReservations = await reservationRepository.GetActiveReservationsByPlateAsync(
                    request.PlateNumber,
                    utcNow,
                    ct);

                if (!ReservationAbusePolicy.CanPlateReserveTimeWindow(activePlateReservations, requestedWindow, utcNow))
                {
                    return Result<ReservationHoldResult>.Failure(BookingErrors.PlateOverlappingReservationExists);
                }

                // US-RES-005: Overbooking protection (BR-02.c)
                // Condition 1: Active overlapping reservations must be less than reservation ceiling
                var overlappingCount = await reservationRepository.CountActiveOverlappingReservationsAsync(
                    request.LotId,
                    request.VehicleGroup,
                    requestedWindow,
                    utcNow,
                    ct);

                // Condition 2: If hold window has already started, currently held reservations must be less than Available physical slots
                var heldCount = 0;
                if (utcNow >= requestedWindow.Start)
                {
                    heldCount = await reservationRepository.CountCurrentlyHeldReservationsAsync(
                        request.LotId,
                        request.VehicleGroup,
                        utcNow,
                        ct);
                }

                var evaluation = ReservationCapacityPolicy.EvaluateCapacity(
                    snapshot.ValidCapacity,
                    snapshot.ReservationAllocationRatio,
                    overlappingCount,
                    heldCount,
                    snapshot.AvailableSlotCount,
                    requestedWindow,
                    utcNow);

                if (!evaluation.IsAllowed)
                {
                    return evaluation.Reason == CapacityRejectionReason.PhysicalSlotsUnavailable
                        ? Result<ReservationHoldResult>.Failure(BookingErrors.PhysicalSlotsUnavailable)
                        : Result<ReservationHoldResult>.Failure(BookingErrors.ReservationCeilingExceeded);
                }

                // Create reservation in TemporaryHold state (BR-01.b)
                var tempHoldExpiresAt = utcNow.AddMinutes(_options.TemporaryHoldMinutes);
                var reservation = Reservation.CreateTemporaryHold(
                    request.ReservationId,
                    request.UserId,
                    request.VehicleId,
                    request.PlateNumber,
                    request.LotId,
                    request.VehicleGroup,
                    request.ReservationType,
                    request.AppointmentTime,
                    request.DurationMinutes,
                    request.PrepaidAmount,
                    tempHoldExpiresAt,
                    utcNow,
                    request.PriceSnapshot);

                reservationRepository.Add(reservation);
                await capacityCoordinator.RecordCapacityUpdatedAsync(request.LotId, request.VehicleGroup, utcNow, ct);
                await reservationRepository.SaveChangesAsync(ct);

                return Result<ReservationHoldResult>.Success(MapToResult(reservation));
            },
            cancellationToken);
    }

    private static bool IsMatchingReservation(Reservation reservation, HoldReservationCapacityCommand request)
    {
        return reservation.UserId == request.UserId &&
               reservation.LotId == request.LotId &&
               string.Equals(reservation.PlateNumber, Reservation.NormalizePlateNumber(request.PlateNumber), StringComparison.OrdinalIgnoreCase) &&
               reservation.AppointmentTime == request.AppointmentTime;
    }

    private static ReservationHoldResult MapToResult(Reservation reservation)
    {
        return new ReservationHoldResult(
            reservation.Id,
            reservation.UserId,
            reservation.VehicleId,
            reservation.PlateNumber,
            reservation.LotId,
            reservation.VehicleGroup,
            reservation.ReservationType,
            reservation.Status,
            reservation.AppointmentTime,
            reservation.DurationMinutes,
            reservation.TempHoldExpiresAt ?? reservation.CreatedAt.AddMinutes(10),
            reservation.PrepaidAmount,
            reservation.GetReservationWindow());
    }
}
