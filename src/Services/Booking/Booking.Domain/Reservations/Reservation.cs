namespace ParkingSystem.Booking.Domain.Reservations;

public sealed class Reservation
{
    public const int PlateNumberMaxLength = 20;

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
        string? priceSnapshot = null)
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
            AppointmentTime = appointmentTime,
            DurationMinutes = durationMinutes,
            TempHoldExpiresAt = tempHoldExpiresAt,
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
            CreatedAt = createdAt
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
    /// Executes a status transition enforcing domain transition rules.
    /// Rejects any transition outside the provisional transition matrix with <see cref="ReservationDomainException"/>.
    /// </summary>
    public void TransitionTo(ReservationStatus newStatus)
    {
        if (!CanTransition(Status, newStatus))
        {
            throw new ReservationDomainException(
                $"Cannot transition reservation from status '{Status}' to '{newStatus}'.");
        }

        Status = newStatus;
    }

    public void Confirm(DateTimeOffset confirmedAt)
    {
        TransitionTo(ReservationStatus.Confirmed);
        ConfirmedAt = confirmedAt;
    }

    public void Use(DateTimeOffset usedAt, Guid? sessionId = null)
    {
        TransitionTo(ReservationStatus.Used);
        UsedAt = usedAt;
        if (sessionId.HasValue)
        {
            SessionId = sessionId;
        }
    }

    public void Cancel(DateTimeOffset cancelledAt, CancelReason? cancelReason = null, bool isNoShow = false, PrepaidOutcome? prepaidOutcome = null)
    {
        TransitionTo(ReservationStatus.Cancelled);
        CancelledAt = cancelledAt;
        CancelReason = cancelReason;
        if (isNoShow)
        {
            IsNoShow = true;
        }
        if (prepaidOutcome.HasValue)
        {
            PrepaidOutcome = prepaidOutcome;
        }
    }

    public void Expire()
    {
        TransitionTo(ReservationStatus.Expired);
    }
}
