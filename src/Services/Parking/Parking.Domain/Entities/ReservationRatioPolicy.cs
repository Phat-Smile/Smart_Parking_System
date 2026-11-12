using System;

namespace ParkingSystem.Parking.Domain.Entities;

public class ReservationRatioPolicy
{
    public const int DefaultReservationRatioPercent = 50;

    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public int ReservationRatioPercent { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ReservationRatioPolicy() { }

    public static ReservationRatioPolicy Create(Guid lotId, int reservationRatioPercent)
    {
        if (lotId == Guid.Empty)
            throw new ArgumentException("Lot ID must not be empty.", nameof(lotId));
        ValidateRatioPercent(reservationRatioPercent);

        return new ReservationRatioPolicy
        {
            Id = Guid.NewGuid(),
            LotId = lotId,
            ReservationRatioPercent = reservationRatioPercent,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void Update(int reservationRatioPercent)
    {
        ValidateRatioPercent(reservationRatioPercent);

        ReservationRatioPercent = reservationRatioPercent;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static int CalculateReservedSlots(int eligibleCapacity, int ratioPercent)
    {
        if (eligibleCapacity < 0)
            throw new ArgumentOutOfRangeException(nameof(eligibleCapacity), "Eligible capacity cannot be negative.");
        ValidateRatioPercent(ratioPercent);

        return (int)((long)eligibleCapacity * ratioPercent / 100);
    }

    public int CalculateReservedSlots(int eligibleCapacity) =>
        CalculateReservedSlots(eligibleCapacity, ReservationRatioPercent);

    private static void ValidateRatioPercent(int ratioPercent)
    {
        if (ratioPercent < 0 || ratioPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(ratioPercent), "Reservation ratio percent must be between 0 and 100.");
    }
}
