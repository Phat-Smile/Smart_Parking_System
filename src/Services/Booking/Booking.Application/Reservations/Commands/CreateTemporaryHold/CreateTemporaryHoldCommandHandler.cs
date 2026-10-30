using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;

/// <summary>
/// Unified handler executing the US-RES-005 capacity guard use case.
/// Inter-service queries to Parking are executed BEFORE opening the local database transaction.
/// </summary>
public sealed class CreateTemporaryHoldCommandHandler(
    IParkingLotCapacityProvider capacityProvider,
    IReservationCapacityCoordinator capacityCoordinator,
    IClock clock,
    ILogger<CreateTemporaryHoldCommandHandler> logger,
    BookingCapacityOptions? options = null)
    : IRequestHandler<CreateTemporaryHoldCommand, Result<Reservation>>
{
    private readonly BookingCapacityOptions _options = options ?? new BookingCapacityOptions();

    public async Task<Result<Reservation>> Handle(
        CreateTemporaryHoldCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Vehicle group invariant: Only cars are permitted for reservation (BR-01.a)
        if (request.VehicleGroup != VehicleGroup.Car)
        {
            logger.LogInformation("Rejected reservation request {ReservationId} because vehicle group '{VehicleGroup}' is not eligible for reservations.",
                request.ReservationId, request.VehicleGroup);
            return Result<Reservation>.Failure(ReservationCapacityErrors.VehicleGroupNotReservable);
        }

        // 2. Validate time window invariants
        if (request.ReservationType == ReservationType.Scheduled && (!request.DurationMinutes.HasValue || request.DurationMinutes.Value <= 0))
        {
            logger.LogWarning("Rejected scheduled reservation request {ReservationId} due to non-positive duration: {DurationMinutes}.",
                request.ReservationId, request.DurationMinutes);
            return Result<Reservation>.Failure(ReservationCapacityErrors.InvalidTimeWindow);
        }

        DateTimeOffset windowStartAt;
        DateTimeOffset windowEndAt;
        DateTimeOffset holdEndAt;

        try
        {
            (windowStartAt, windowEndAt, holdEndAt) = ReservationTimeWindow.CalculateWindows(
                request.ReservationType,
                request.AppointmentTime,
                request.DurationMinutes,
                leadMinutes: _options.LeadTimeMinutes,
                graceMinutes: _options.GracePeriodMinutes);
        }
        catch (ReservationDomainException ex)
        {
            logger.LogWarning(ex, "Domain validation failed when calculating windows for reservation {ReservationId}.", request.ReservationId);
            return Result<Reservation>.Failure(ReservationCapacityErrors.InvalidTimeWindow);
        }

        var now = clock.UtcNow;

        // 3. Port query: Retrieve Parking lot capacity details BEFORE opening local DB transaction
        var capacityResult = await capacityProvider.GetCapacityInfoAsync(request.LotId, cancellationToken);
        if (capacityResult.IsFailure || capacityResult.Value is null)
        {
            logger.LogWarning("Failed to retrieve capacity details from Parking for lot {LotId}: {Error}",
                request.LotId, capacityResult.Error?.Message ?? "No capacity data returned.");
            return Result<Reservation>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
        }

        var capacityInfo = capacityResult.Value;

        // Sanity check on remote data (abnormal or negative values -> fail closed)
        if (capacityInfo.ValidCapacity < 0 ||
            capacityInfo.ReservationRatioPercent < 0 ||
            capacityInfo.ReservationRatioPercent > 100 ||
            capacityInfo.AvailableSlotCount < 0)
        {
            logger.LogError("Parking service returned abnormal capacity figures for lot {LotId}: ValidCapacity={ValidCapacity}, Ratio={Ratio}, AvailableSlots={AvailableSlots}.",
                request.LotId, capacityInfo.ValidCapacity, capacityInfo.ReservationRatioPercent, capacityInfo.AvailableSlotCount);
            return Result<Reservation>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
        }

        // 4. Calculate ceiling capacity: floor(validCapacity * ratioPercent / 100) (BR-02.b)
        var ceiling = ReservationTimeWindow.CalculateCeiling(capacityInfo.ValidCapacity, capacityInfo.ReservationRatioPercent);

        // 5. Check condition (ii) requirement: if new reservation window has already started at 'now',
        // verify active holds against availableSlotCount
        int? availableSlotCountToCheck = (now >= windowStartAt)
            ? capacityInfo.AvailableSlotCount
            : null;

        // 6. Build reservation aggregate with temporary hold status (BR-01.b, 10 min expiry)
        var tempHoldExpiresAt = now.AddMinutes(_options.TempHoldDurationMinutes);
        Reservation reservation;
        try
        {
            reservation = Reservation.CreateTemporaryHold(
                id: request.ReservationId,
                userId: request.UserId,
                vehicleId: request.VehicleId,
                plateNumber: request.PlateNumber,
                lotId: request.LotId,
                vehicleGroup: request.VehicleGroup,
                reservationType: request.ReservationType,
                appointmentTime: request.AppointmentTime,
                prepaidAmount: request.PrepaidAmount,
                createdAt: now,
                durationMinutes: request.DurationMinutes,
                tempHoldExpiresAt: tempHoldExpiresAt,
                priceSnapshot: request.PriceSnapshot,
                windowStartAt: windowStartAt,
                windowEndAt: windowEndAt,
                holdEndAt: holdEndAt);
        }
        catch (ReservationDomainException domainEx)
        {
            logger.LogWarning(domainEx, "Domain invariant check failed for reservation {ReservationId}.", request.ReservationId);
            return Result<Reservation>.Failure(ReservationCapacityErrors.InvalidTimeWindow);
        }

        // 7. Atomic DB execution in persistence coordinator (single transaction with row-level lock)
        var lockTimeout = TimeSpan.FromSeconds(_options.LockTimeoutSeconds);
        var atomicResult = await capacityCoordinator.CreateTemporaryHoldAtomicAsync(
            reservation,
            ceiling,
            availableSlotCountToCheck,
            lockTimeout,
            cancellationToken);

        return atomicResult;
    }
}
