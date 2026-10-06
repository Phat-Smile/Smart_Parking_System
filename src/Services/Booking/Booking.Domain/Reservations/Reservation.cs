using ParkingSystem.Booking.Domain.Enums;

namespace ParkingSystem.Booking.Domain.Reservations;

public sealed class Reservation
{
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

    /// <summary>
    /// Factory for creating a new reservation in TemporaryHold state (BR-01.b).
    /// </summary>
    public static Reservation CreateTemporaryHold(
        Guid id,
        Guid userId,
        Guid vehicleId,
        string plateNumber,
        Guid lotId,
        VehicleGroup vehicleGroup,
        ReservationType reservationType,
        DateTimeOffset appointmentTime,
        int? durationMinutes,
        decimal prepaidAmount,
        DateTimeOffset tempHoldExpiresAt,
        DateTimeOffset createdAt,
        string? priceSnapshot = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Reservation id cannot be empty.", nameof(id));
        if (userId == Guid.Empty)
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        if (vehicleId == Guid.Empty)
            throw new ArgumentException("Vehicle id cannot be empty.", nameof(vehicleId));
        if (string.IsNullOrWhiteSpace(plateNumber))
            throw new ArgumentException("Plate number cannot be empty.", nameof(plateNumber));
        if (lotId == Guid.Empty)
            throw new ArgumentException("Lot id cannot be empty.", nameof(lotId));
        if (vehicleGroup != VehicleGroup.Car)
            throw new InvalidOperationException("Only passenger cars can reserve parking capacity (BR-01.a).");
        if (prepaidAmount < 0)
            throw new ArgumentException("Prepaid amount cannot be negative.", nameof(prepaidAmount));

        if (reservationType == ReservationType.Scheduled)
        {
            if (!durationMinutes.HasValue || durationMinutes.Value <= 0)
                throw new ArgumentException("Duration minutes is required for scheduled reservations.", nameof(durationMinutes));
            if (durationMinutes.Value > 1440)
                throw new ArgumentException("Scheduled reservation duration cannot exceed 24 hours (1440 minutes) (BR-01.a).", nameof(durationMinutes));
        }

        return new Reservation
        {
            Id = id,
            UserId = userId,
            VehicleId = vehicleId,
            PlateNumber = NormalizePlateNumber(plateNumber),
            LotId = lotId,
            VehicleGroup = vehicleGroup,
            ReservationType = reservationType,
            Status = ReservationStatus.TemporaryHold,
            AppointmentTime = appointmentTime,
            DurationMinutes = durationMinutes,
            TempHoldExpiresAt = tempHoldExpiresAt,
            PrepaidAmount = prepaidAmount,
            PriceSnapshot = priceSnapshot,
            IsNoShow = false,
            RowVersion = 0,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Calculates the effective reservation hold window (BR-02.e).
    /// </summary>
    public ReservationTimeWindow GetReservationWindow()
    {
        return ReservationTimeWindow.ForReservation(ReservationType, AppointmentTime, DurationMinutes);
    }

    /// <summary>
    /// Checks whether the reservation is currently active/effective according to BR-01.f.
    /// A reservation is active when:
    /// - status is TemporaryHold (and hold has not expired based on TempHoldExpiresAt) OR Confirmed;
    /// - vehicle has not entered the gate (not Used / used_at is null);
    /// - it has not been cancelled;
    /// - it has not expired.
    /// </summary>
    public bool IsActive(DateTimeOffset utcNow)
    {
        if (Status == ReservationStatus.Used || UsedAt is not null)
            return false;

        if (Status == ReservationStatus.Cancelled || CancelledAt is not null)
            return false;

        if (Status == ReservationStatus.Expired)
            return false;

        if (Status == ReservationStatus.TemporaryHold)
        {
            return TempHoldExpiresAt is null || TempHoldExpiresAt.Value > utcNow;
        }

        return Status == ReservationStatus.Confirmed;
    }

    /// <summary>
    /// Transitions reservation to Confirmed state (BR-01.c).
    /// </summary>
    public void Confirm(DateTimeOffset confirmedAt)
    {
        if (Status != ReservationStatus.TemporaryHold)
            throw new InvalidOperationException($"Cannot confirm reservation in status '{Status}'.");

        Status = ReservationStatus.Confirmed;
        ConfirmedAt = confirmedAt;
        RowVersion++;
    }

    /// <summary>
    /// Marks reservation as Used when vehicle enters the gate (BR-03.c, BR-04.b).
    /// </summary>
    public void MarkUsed(Guid sessionId, DateTimeOffset usedAt)
    {
        if (Status != ReservationStatus.Confirmed)
            throw new InvalidOperationException($"Cannot mark reservation as used from status '{Status}'. Must be Confirmed.");

        Status = ReservationStatus.Used;
        SessionId = sessionId;
        UsedAt = usedAt;
        RowVersion++;
    }

    /// <summary>
    /// Cancels reservation (BR-03.a, BR-03.b, BR-03.d).
    /// </summary>
    public void Cancel(CancelReason reason, DateTimeOffset cancelledAt, PrepaidOutcome? outcome = null)
    {
        if (Status == ReservationStatus.Used)
            throw new InvalidOperationException("Cannot cancel a reservation that has already been used (BR-03.c).");
        if (Status == ReservationStatus.Cancelled)
            return;

        Status = ReservationStatus.Cancelled;
        CancelledAt = cancelledAt;
        CancelReason = reason;
        PrepaidOutcome = outcome;
        if (reason == Enums.CancelReason.NoShow)
        {
            IsNoShow = true;
        }
        RowVersion++;
    }

    /// <summary>
    /// Expires temporary hold when timeout is reached without successful payment (BR-01.d).
    /// </summary>
    public void Expire(DateTimeOffset expiredAt)
    {
        if (Status != ReservationStatus.TemporaryHold)
            return;

        Status = ReservationStatus.Expired;
        CancelledAt = expiredAt;
        RowVersion++;
    }

    public static string NormalizePlateNumber(string plate)
    {
        return plate.Trim().ToUpperInvariant();
    }
}
