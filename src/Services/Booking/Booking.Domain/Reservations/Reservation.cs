namespace ParkingSystem.Booking.Domain.Reservations;

public sealed class Reservation
{
    public const int PlateNumberMaxLength = 20;
    public const int NoShowGracePeriodMinutes = 15;
    public const int TechnicalToleranceMinutes = 3;
    public const int MaxActiveReservationsPerUser = 3;
    private Reservation()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid VehicleId { get; private set; }
    public string PlateNumber { get; private set; } = null!;
    public Guid LotId { get; private set; }
    public VehicleGroup VehicleGroup { get; private set; }
    public ReservationType ReservationType { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset AppointmentTime { get; private set; }
    public int? DurationMinutes { get; private set; }
    public DateTimeOffset WindowStartAt { get; private set; }
    public DateTimeOffset WindowEndAt { get; private set; }
    public DateTimeOffset HoldEndAt { get; private set; }
    public DateTimeOffset? TempHoldExpiresAt { get; private set; }
    public decimal PrepaidAmount { get; private set; }
    public string? PriceSnapshot { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public Guid? SessionId { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public CancelReason? CancelReason { get; private set; }
    public PrepaidOutcome? PrepaidOutcome { get; private set; }
    public bool IsNoShow { get; private set; }
    public int RowVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Reservation CreateTemporaryHold(
        Guid id,
        Guid userId,
        Guid vehicleId,
        string plateNumber,
        Guid lotId,
        VehicleGroup vehicleGroup,
        ReservationType reservationType,
        DateTimeOffset appointmentTime,
        decimal prepaidAmount,
        DateTimeOffset createdAt,
        int? durationMinutes = null,
        DateTimeOffset? tempHoldExpiresAt = null,
        string? priceSnapshot = null,
        DateTimeOffset? windowStartAt = null,
        DateTimeOffset? windowEndAt = null,
        DateTimeOffset? holdEndAt = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Reservation ID must not be empty.", nameof(id));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID must not be empty.", nameof(userId));
        if (vehicleId == Guid.Empty)
            throw new ArgumentException("Vehicle ID must not be empty.", nameof(vehicleId));
        if (lotId == Guid.Empty)
            throw new ArgumentException("Lot ID must not be empty.", nameof(lotId));
        ArgumentException.ThrowIfNullOrWhiteSpace(plateNumber);

        var normalizedPlate = plateNumber.Trim().ToUpperInvariant();
        if (normalizedPlate.Length > PlateNumberMaxLength)
            throw new ArgumentException($"Plate number must not exceed {PlateNumberMaxLength} characters.", nameof(plateNumber));

        if (prepaidAmount < 0)
            throw new ArgumentException("Prepaid amount cannot be negative.", nameof(prepaidAmount));

        if (!Enum.IsDefined(reservationType))
            throw new ArgumentOutOfRangeException(nameof(reservationType), $"Invalid reservation type: {reservationType}");

        if (!Enum.IsDefined(vehicleGroup))
            throw new ArgumentOutOfRangeException(nameof(vehicleGroup), $"Invalid vehicle group: {vehicleGroup}");

        if (vehicleGroup != VehicleGroup.Car)
            throw new ReservationDomainException("Only cars are allowed to create reservations.");

        var calculatedWindows = (windowStartAt.HasValue && windowEndAt.HasValue && holdEndAt.HasValue)
            ? (WindowStartAt: windowStartAt.Value.ToUniversalTime(), WindowEndAt: windowEndAt.Value.ToUniversalTime(), HoldEndAt: holdEndAt.Value.ToUniversalTime())
            : ReservationTimeWindow.CalculateWindows(reservationType, appointmentTime, durationMinutes);

        return new Reservation
        {
            Id = id,
            UserId = userId,
            VehicleId = vehicleId,
            PlateNumber = normalizedPlate,
            LotId = lotId,
            VehicleGroup = vehicleGroup,
            ReservationType = reservationType,
            Status = ReservationStatus.TemporaryHold,
            AppointmentTime = appointmentTime.ToUniversalTime(),
            DurationMinutes = durationMinutes,
            WindowStartAt = calculatedWindows.WindowStartAt,
            WindowEndAt = calculatedWindows.WindowEndAt,
            HoldEndAt = calculatedWindows.HoldEndAt,
            TempHoldExpiresAt = tempHoldExpiresAt?.ToUniversalTime(),
            PrepaidAmount = prepaidAmount,
            PriceSnapshot = priceSnapshot,
            ConfirmedAt = null,
            UsedAt = null,
            SessionId = null,
            CancelledAt = null,
            CancelReason = null,
            PrepaidOutcome = null,
            IsNoShow = false,
            RowVersion = 0,
            CreatedAt = createdAt.ToUniversalTime()
        };
    }

    /// <summary>
    /// Evaluates whether the requested status transition is permitted under the provisional transition policy.
    /// Note: This transition matrix represents an OPEN DECISION pending mentor/team confirmation.
    /// </summary>
    public static bool CanTransition(ReservationStatus current, ReservationStatus target) =>
        (current, target) switch
        {
            (ReservationStatus.TemporaryHold, ReservationStatus.Confirmed) => true,
            (ReservationStatus.TemporaryHold, ReservationStatus.Cancelled) => true,
            (ReservationStatus.TemporaryHold, ReservationStatus.Expired) => true,
            (ReservationStatus.Confirmed, ReservationStatus.Used) => true,
            (ReservationStatus.Confirmed, ReservationStatus.Cancelled) => true,
            _ => false
        };

    /// <summary>
    /// Executes a status transition enforcing domain transition rules and advancing RowVersion.
    /// Rejects any transition outside the provisional transition matrix with <see cref="ReservationDomainException"/>.
    /// </summary>
    private void TransitionTo(ReservationStatus newStatus)
    {
        if (!CanTransition(Status, newStatus))
        {
            throw new ReservationDomainException(
                $"Cannot transition reservation from status '{Status}' to '{newStatus}'.");
        }

        Status = newStatus;
        RowVersion++;
    }

    public void Confirm(DateTimeOffset confirmedAt)
    {
        TransitionTo(ReservationStatus.Confirmed);
        ConfirmedAt = confirmedAt.ToUniversalTime();
    }

    public void Use(DateTimeOffset usedAt, Guid? sessionId = null)
    {
        if (sessionId.HasValue && sessionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Session ID must not be empty.",
                nameof(sessionId));
        }

        TransitionTo(ReservationStatus.Used);

        UsedAt = usedAt.ToUniversalTime();

        if (sessionId.HasValue)
        {
            SessionId = sessionId;
        }
    }

    public void Cancel(
    DateTimeOffset cancelledAt,
    CancelReason cancelReason,
    PrepaidOutcome? prepaidOutcome = null)
    {
        // Validate enum values before changing the reservation.
        if (!Enum.IsDefined(cancelReason))
        {
            throw new ArgumentOutOfRangeException(
            nameof(cancelReason),
            $"Invalid cancel reason: {cancelReason}");
        }

        if (prepaidOutcome.HasValue &&
            !Enum.IsDefined(prepaidOutcome.Value))
        {
            throw new ArgumentOutOfRangeException(
            nameof(prepaidOutcome),
            $"Invalid prepaid outcome: {prepaidOutcome.Value}");
        }

        var cancelledAtUtc = cancelledAt.ToUniversalTime();

        // NoShow is only valid for a confirmed reservation.
        if (cancelReason == Reservations.CancelReason.NoShow)
        {
            if (Status != ReservationStatus.Confirmed)
            {
                throw new ReservationDomainException(
                    "Only confirmed reservations can be marked as NoShow.");
            }

                // The full 15-minute grace period must have elapsed.
            var noShowEligibleAt = AppointmentTime.AddMinutes(
                NoShowGracePeriodMinutes);

            if (cancelledAtUtc < noShowEligibleAt)
            {
            throw new ReservationDomainException(
                $"A reservation can only be marked as NoShow " +
                $"after {NoShowGracePeriodMinutes} minutes past its appointment time.");
        }
    }

    // Validate all business rules before changing the state.
    TransitionTo(ReservationStatus.Cancelled);

    CancelledAt = cancelledAtUtc;
    CancelReason = cancelReason;
    IsNoShow = cancelReason == Reservations.CancelReason.NoShow;
    PrepaidOutcome = prepaidOutcome;
    }

    public void Expire()
    {
        TransitionTo(ReservationStatus.Expired);
    }

    public void RecordPrepaidRefunded()
    {
        if (ConfirmedAt != null)
        {
            throw new ReservationDomainException("Cannot record prepaid refund on a confirmed reservation.");
        }

        PrepaidOutcome = Reservations.PrepaidOutcome.Refunded;
        RowVersion++;
    }

    public void RecordCancellationRefunded()
    {
        if (Status != ReservationStatus.Cancelled ||
            CancelReason != Reservations.CancelReason.CustomerCancelled ||
            PrepaidOutcome != null)
        {
            throw new ReservationDomainException(
                "Cannot record cancellation refund on a reservation that is not cancelled by customer with pending prepaid outcome.");
        }

        PrepaidOutcome = Reservations.PrepaidOutcome.Refunded;
        RowVersion++;
    }

    /// <summary>
    /// Determines whether the reservation is active at the specified point in time (BR-1, US-RES-010).
    /// Active states: Confirmed, or TemporaryHold with TempHoldExpiresAt > now.
    /// Non-active states: Used, Cancelled, Expired, or TemporaryHold with TempHoldExpiresAt <= now.
    /// </summary>
    public bool IsActive(DateTimeOffset now) =>
        Status == ReservationStatus.Confirmed ||
        (Status == ReservationStatus.TemporaryHold && TempHoldExpiresAt.HasValue && TempHoldExpiresAt.Value > now.ToUniversalTime());

    /// <summary>
    /// Evaluates whether this reservation's time window overlaps with the requested interval [start, end)
    /// under half-open interval semantics (BR-2, US-RES-010).
    /// </summary>
    public bool OverlapsWith(DateTimeOffset start, DateTimeOffset end) =>
        ReservationTimeWindow.IsOverlapping(WindowStartAt, WindowEndAt, start, end);

    /// <summary>
    /// Evaluates whether this reservation matches the given license plate under canonical normalization rules (BR-2, US-RES-010).
    /// </summary>
    public bool HasSamePlate(string? otherPlate) =>
        LicensePlateNormalizer.AreEquivalent(PlateNumber, otherPlate);

    /// <summary>
    /// Checks whether this reservation conflicts with another reservation for the same vehicle
    /// during the specified reservation window [start, end) at the reference time (BR-2, US-RES-010).
    /// Conflict occurs when this reservation is active, has an equivalent license plate, and their reservation windows overlap.
    /// </summary>
    public bool ConflictsWithPlate(string? plate, DateTimeOffset windowStart, DateTimeOffset windowEnd, DateTimeOffset now) =>
        IsActive(now) &&
        HasSamePlate(plate) &&
        OverlapsWith(windowStart, windowEnd);
}
