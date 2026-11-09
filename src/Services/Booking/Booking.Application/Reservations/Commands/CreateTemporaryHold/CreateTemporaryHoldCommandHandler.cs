using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using ParkingSystem.Booking.Application.Parking;
using ParkingSystem.Booking.Application.Vehicles;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Booking.Application.Reservations.Commands.CreateTemporaryHold;

/// <summary>
/// Unified handler executing the US-RES-005 and US-RES-002 capacity guard and temporary hold use cases.
/// Inter-service queries to Parking and Identity are executed BEFORE opening the local database transaction.
/// </summary>
public sealed class CreateTemporaryHoldCommandHandler : IRequestHandler<CreateTemporaryHoldCommand, Result<Reservation>>
{
    private readonly IVehicleOwnershipClient _vehicleOwnershipClient;
    private readonly IParkingLotCapacityProvider _capacityProvider;
    private readonly IReservationCapacityCoordinator _capacityCoordinator;
    private readonly IClock _clock;
    private readonly ILogger<CreateTemporaryHoldCommandHandler> _logger;
    private readonly IParkingPricingProvider _pricingProvider;
    private readonly BookingCapacityOptions _options;

    public CreateTemporaryHoldCommandHandler(
        IVehicleOwnershipClient vehicleOwnershipClient,
        IParkingLotCapacityProvider capacityProvider,
        IReservationCapacityCoordinator capacityCoordinator,
        IClock clock,
        ILogger<CreateTemporaryHoldCommandHandler> logger,
        IParkingPricingProvider pricingProvider,
        BookingCapacityOptions? options = null)
    {
        _vehicleOwnershipClient = vehicleOwnershipClient;
        _capacityProvider = capacityProvider;
        _capacityCoordinator = capacityCoordinator;
        _clock = clock;
        _logger = logger;
        _pricingProvider = pricingProvider;
        _options = options ?? new BookingCapacityOptions();
    }

    public async Task<Result<Reservation>> Handle(
        CreateTemporaryHoldCommand request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Verify vehicle registration and user ownership against Identity Service
        var ownershipResult = await _vehicleOwnershipClient.VerifyOwnershipAsync(
            request.PlateNumber,
            request.UserId,
            cancellationToken);

        if (ownershipResult.IsFailure || ownershipResult.Value is null)
        {
            var error = ownershipResult.Error ?? VehicleOwnershipErrors.CommunicationFailed;
            if (error == VehicleOwnershipErrors.ServiceAuthFailed || error == VehicleOwnershipErrors.CommunicationFailed)
            {
                _logger.LogError("Vehicle ownership verification failed due to infrastructure error {ErrorCode} for user {UserId}.",
                    error.Code, request.UserId);
            }
            else if (error == VehicleOwnershipErrors.DeadlineExceeded ||
                     error == VehicleOwnershipErrors.ServiceUnavailable ||
                     error == VehicleOwnershipErrors.RequestCancelled)
            {
                _logger.LogWarning("Vehicle ownership verification unavailable with {ErrorCode} for user {UserId}.",
                    error.Code, request.UserId);
            }
            else if (error == VehicleOwnershipErrors.VehicleNotRegistered ||
                     error == VehicleOwnershipErrors.VehicleOwnershipMismatch ||
                     error == VehicleOwnershipErrors.AccountNotEligible)
            {
                _logger.LogInformation("Vehicle ownership verification rejected with {ErrorCode} for user {UserId}.",
                    error.Code, request.UserId);
            }
            // EmptyPlate, InvalidPlateFormat, UnsupportedVehicleType: no log per Table 4.3

            return Result<Reservation>.Failure(error);
        }

        var ownershipInfo = ownershipResult.Value;
        var canonicalPlate = ownershipInfo.CanonicalPlate;
        var verifiedVehicleId = ownershipInfo.VehicleId;
        const VehicleGroup verifiedVehicleGroup = VehicleGroup.Car; // Identity verified Car

        var now = _clock.UtcNow;

        // 2. Validate scheduled time constraints & calculate windows (US-RES-002 Section 3.2)
        if (request.ReservationType == ReservationType.Scheduled)
        {
            try
            {
                ReservationTimeWindow.ValidateScheduledConstraints(
                    request.AppointmentTime,
                    request.DurationMinutes,
                    now);
            }
            catch (Exception ex) when (ex is ReservationDomainException or ArgumentOutOfRangeException or OverflowException)
            {
                _logger.LogWarning(ex, "Scheduled time validation failed for reservation {ReservationId}.", request.ReservationId);
                return Result<Reservation>.Failure(ReservationCapacityErrors.InvalidTimeWindow);
            }
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
        catch (Exception ex) when (ex is ReservationDomainException or ArgumentOutOfRangeException or OverflowException)
        {
            _logger.LogWarning(ex, "Domain validation failed when calculating windows for reservation {ReservationId}.", request.ReservationId);
            return Result<Reservation>.Failure(ReservationCapacityErrors.InvalidTimeWindow);
        }

        // 3. Port query: Retrieve Parking lot capacity details BEFORE opening local DB transaction
        var capacityResult = await _capacityProvider.GetCapacityInfoAsync(request.LotId, cancellationToken);
        if (capacityResult.IsFailure || capacityResult.Value is null)
        {
            _logger.LogWarning("Failed to retrieve capacity details from Parking for lot {LotId}: {Error}",
                request.LotId, capacityResult.Error?.Message ?? "No capacity data returned.");
            return Result<Reservation>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
        }

        var capacityInfo = capacityResult.Value;

        // Sanity check on remote data (abnormal or negative values -> fail closed)
        if (capacityInfo.ValidCapacity < 0 ||
            capacityInfo.ReservationRatioPercent < 0 ||
            capacityInfo.ReservationRatioPercent > 100 ||
            capacityInfo.AvailableSlotCount < 0 ||
            capacityInfo.DepositAmount < 0m ||
            capacityInfo.DepositAmount % 1m != 0m)
        {
            _logger.LogError("Parking service returned abnormal capacity figures for lot {LotId}: ValidCapacity={ValidCapacity}, Ratio={Ratio}, AvailableSlots={AvailableSlots}, DepositAmount={DepositAmount}.",
                request.LotId, capacityInfo.ValidCapacity, capacityInfo.ReservationRatioPercent, capacityInfo.AvailableSlotCount, capacityInfo.DepositAmount);
            return Result<Reservation>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
        }

        // Check if reservations are enabled for this parking lot (US-OWNER-006 AC 2)
        if (!capacityInfo.ReservationEnabled)
        {
            _logger.LogInformation("Rejected reservation request for lot {LotId} because reservations are currently disabled.", request.LotId);
            return Result<Reservation>.Failure(ReservationCapacityErrors.LotReservationsDisabled);
        }

        // 4. Calculate ceiling capacity: floor(validCapacity * ratioPercent / 100) (BR-02.b)
        var ceiling = ReservationTimeWindow.CalculateCeiling(capacityInfo.ValidCapacity, capacityInfo.ReservationRatioPercent);

        // 5. Check condition (ii) requirement: if new reservation window has already started at 'now',
        // verify active holds against availableSlotCount
        int? availableSlotCountToCheck = (now >= windowStartAt)
            ? capacityInfo.AvailableSlotCount
            : null;

        // 6. Pricing & Price snapshot calculation (US-RES-002 Section 3.5, BR-10.a, BR-09.b)
        // For Scheduled reservations: query authoritative pricing from Parking service and compute 100% upfront amount.
        // Client-supplied PrepaidAmount and PriceSnapshot are NEVER trusted.
        decimal effectivePrepaidAmount;
        string? effectivePriceSnapshot;

        if (request.ReservationType == ReservationType.Scheduled)
        {
            var pricingResult = await _pricingProvider.GetPricingInfoAsync(request.LotId, verifiedVehicleGroup, cancellationToken);
            if (pricingResult.IsFailure || pricingResult.Value is null)
            {
                _logger.LogWarning("Failed to retrieve pricing details for lot {LotId}: {Error}",
                    request.LotId, pricingResult.Error?.Message ?? "No pricing data returned.");
                return Result<Reservation>.Failure(pricingResult.Error ?? ReservationCapacityErrors.CapacityInfoUnavailable);
            }

            var pricingInfo = pricingResult.Value;

            if (pricingInfo.Price <= 0m ||
                pricingInfo.Price % 1m != 0m ||
                pricingInfo.OvernightFee < 0m ||
                pricingInfo.OvernightFee % 1m != 0m ||
                (pricingInfo.BlockMinutes.HasValue && pricingInfo.BlockMinutes.Value <= 0) ||
                !string.Equals(pricingInfo.Currency, "VND", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Parking service returned invalid pricing figures for lot {LotId}: Price={Price}, BlockMinutes={BlockMinutes}, OvernightFee={OvernightFee}, Currency={Currency}.",
                    request.LotId, pricingInfo.Price, pricingInfo.BlockMinutes, pricingInfo.OvernightFee, pricingInfo.Currency);
                return Result<Reservation>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
            }

            var appointmentTime = request.AppointmentTime;
            int durationMinutes = request.DurationMinutes!.Value;
            var endTime = appointmentTime.AddMinutes(durationMinutes);

            decimal totalAmount;
            int overnightMarks;
            try
            {
                totalAmount = ScheduledPrepaymentCalculator.Calculate(
                    pricingInfo.BillingMode,
                    pricingInfo.BlockMinutes,
                    pricingInfo.Price,
                    pricingInfo.OvernightFee,
                    pricingInfo.OvernightTimeMark,
                    appointmentTime,
                    endTime);

                overnightMarks = ScheduledPrepaymentCalculator.CountOvernightMarks(
                    appointmentTime,
                    endTime,
                    pricingInfo.OvernightTimeMark ?? ScheduledPrepaymentCalculator.DefaultOvernightTimeMark,
                    ScheduledPrepaymentCalculator.DefaultVietnamOffset);

            }
            catch (ReservationDomainException ex)
            {
                _logger.LogError(ex, "Domain exception while calculating scheduled prepayment for lot {LotId}: {Message}",
                    request.LotId, ex.Message);
                return Result<Reservation>.Failure(ReservationCapacityErrors.CapacityInfoUnavailable);
            }

            var snapshotObj = new
            {
                lotId = request.LotId,
                vehicleGroup = verifiedVehicleGroup.ToString(),
                billingMode = pricingInfo.BillingMode,
                blockMinutes = pricingInfo.BlockMinutes,
                unitPrice = pricingInfo.Price,
                durationMinutes = durationMinutes,
                overnightFee = pricingInfo.OvernightFee,
                overnightTimeMark = pricingInfo.OvernightTimeMark?.ToString() ?? "00:00:00",
                overnightMarks = overnightMarks,
                calculatedTotal = totalAmount,
                currency = pricingInfo.Currency,
                policyVersion = pricingInfo.PolicyVersion,
                calculatedAt = now
            };

            effectivePrepaidAmount = totalAmount;
            effectivePriceSnapshot = JsonSerializer.Serialize(snapshotObj);
        }
        else
        {
            // HoldWhileDriving: takes authoritative deposit from Parking, client snapshot is never stored (B5)
            effectivePrepaidAmount = capacityInfo.DepositAmount;
            effectivePriceSnapshot = null;
        }

        // 7. Build reservation aggregate with temporary hold status (BR-01.b, 10 min expiry)
        var tempHoldExpiresAt = now.AddMinutes(_options.TempHoldDurationMinutes);

        Reservation reservation;
        try
        {
            reservation = Reservation.CreateTemporaryHold(
                id: request.ReservationId,
                userId: request.UserId,
                vehicleId: verifiedVehicleId,
                plateNumber: canonicalPlate,
                lotId: request.LotId,
                vehicleGroup: verifiedVehicleGroup,
                reservationType: request.ReservationType,
                appointmentTime: request.AppointmentTime,
                prepaidAmount: effectivePrepaidAmount,
                createdAt: now,
                durationMinutes: request.DurationMinutes,
                tempHoldExpiresAt: tempHoldExpiresAt,
                priceSnapshot: effectivePriceSnapshot,
                windowStartAt: windowStartAt,
                windowEndAt: windowEndAt,
                holdEndAt: holdEndAt);
        }
        catch (ReservationDomainException domainEx)
        {
            _logger.LogWarning(domainEx, "Domain invariant check failed for reservation {ReservationId}.", request.ReservationId);
            return Result<Reservation>.Failure(ReservationCapacityErrors.InvalidTimeWindow);
        }

        // 8. Atomic DB execution in persistence coordinator (single transaction with row-level lock)
        var lockTimeout = TimeSpan.FromSeconds(_options.LockTimeoutSeconds);
        var atomicResult = await _capacityCoordinator.CreateTemporaryHoldAtomicAsync(
            reservation,
            ceiling,
            availableSlotCountToCheck,
            lockTimeout,
            _options.MaxActiveReservationsPerUser,
            cancellationToken);

        return atomicResult;
    }
}
