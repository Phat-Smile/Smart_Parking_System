using ParkingSystem.Booking.Domain.Reservations;

namespace ParkingSystem.Booking.Application.Reservations.Cancellation;

public abstract record CancellationRefundDecision
{
    private CancellationRefundDecision() { }

    public sealed record Refund(decimal Amount) : CancellationRefundDecision;
    public sealed record Forfeit : CancellationRefundDecision;
    public sealed record NoMoney : CancellationRefundDecision;
}

/// <summary>
/// Pure policy evaluating refund eligibility upon reservation cancellation (BR-03.b, BR-03.c, BR-12).
/// </summary>
public static class CancellationRefundPolicy
{
    public static readonly TimeSpan HoldWhileDrivingRefundWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Decides whether cancellation qualifies for a refund, forfeiture, or involves no money.
    /// BR-03.b: HoldWhileDriving confirmed and cancelled within 5 minutes (half-open: now &lt; ConfirmedAt + 5m) -> 100% refund.
    /// After that (now &gt;= ConfirmedAt + 5m) -> deposit is forfeited.
    /// Scheduled: 100% refund (existing behavior preserved until US-RES-007).
    /// TemporaryHold or PrepaidAmount == 0: NoMoney.
    /// </summary>
    public static CancellationRefundDecision Decide(Reservation r, DateTimeOffset now)
    {
        if (r.Status == ReservationStatus.TemporaryHold || r.PrepaidAmount <= 0m)
        {
            return new CancellationRefundDecision.NoMoney();
        }

        if (r.ReservationType == ReservationType.HoldWhileDriving)
        {
            if (r.ConfirmedAt.HasValue)
            {
                var cutoff = r.ConfirmedAt.Value.Add(HoldWhileDrivingRefundWindow);
                if (now.ToUniversalTime() < cutoff.ToUniversalTime())
                {
                    return new CancellationRefundDecision.Refund(r.PrepaidAmount);
                }

                return new CancellationRefundDecision.Forfeit();
            }

            return new CancellationRefundDecision.NoMoney();
        }

        if (r.ReservationType == ReservationType.Scheduled)
        {
            return new CancellationRefundDecision.Refund(r.PrepaidAmount);
        }

        return new CancellationRefundDecision.NoMoney();
    }
}
