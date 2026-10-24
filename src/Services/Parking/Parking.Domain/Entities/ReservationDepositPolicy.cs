using System;

namespace ParkingSystem.Parking.Domain.Entities;

public class ReservationDepositPolicy
{
    public Guid Id { get; private set; }
    public Guid LotId { get; private set; }
    public decimal HoldDepositVnd { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private ReservationDepositPolicy() { }

    public static ReservationDepositPolicy Create(Guid lotId, decimal holdDepositVnd)
    {
        if (lotId == Guid.Empty)
            throw new ArgumentException("Lot ID must not be empty.", nameof(lotId));
        ValidateDepositAmount(holdDepositVnd);

        return new ReservationDepositPolicy
        {
            Id = Guid.NewGuid(),
            LotId = lotId,
            HoldDepositVnd = holdDepositVnd,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void Update(decimal holdDepositVnd)
    {
        ValidateDepositAmount(holdDepositVnd);

        HoldDepositVnd = holdDepositVnd;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ValidateDepositAmount(decimal holdDepositVnd)
    {
        if (holdDepositVnd < 0m)
            throw new ArgumentException("Hold deposit cannot be negative.", nameof(holdDepositVnd));
        if (holdDepositVnd % 1m != 0m)
            throw new ArgumentException("Hold deposit must be an integer VND value.", nameof(holdDepositVnd));
    }
}
