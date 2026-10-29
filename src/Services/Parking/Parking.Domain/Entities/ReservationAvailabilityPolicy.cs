using System;

namespace ParkingSystem.Parking.Domain.Entities;

public class ReservationAvailabilityPolicy
{
    public const bool DefaultReservationEnabled = true;

    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public bool ReservationEnabled { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ReservationAvailabilityPolicy() { }

    public static ReservationAvailabilityPolicy Create(Guid lotId, bool reservationEnabled)
    {
        if (lotId == Guid.Empty)
            throw new ArgumentException("Lot ID must not be empty.", nameof(lotId));

        return new ReservationAvailabilityPolicy
        {
            Id = Guid.NewGuid(),
            LotId = lotId,
            ReservationEnabled = reservationEnabled,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void Update(bool reservationEnabled)
    {
        ReservationEnabled = reservationEnabled;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
