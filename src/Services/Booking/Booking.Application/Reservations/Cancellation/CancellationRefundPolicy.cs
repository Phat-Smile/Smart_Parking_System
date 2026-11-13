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
/// Pure policy evaluating refund eligibility upon reservation cancellation (BR-03.a, BR-03.b, BR-03.c, BR-12).
/// </summary>
public static class CancellationRefundPolicy
{
    public static readonly TimeSpan HoldWhileDrivingRefundWindow = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ScheduledRefundWindow = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Decides whether cancellation qualifies for a refund, forfeiture, or involves no money.
    /// BR-03.a: Scheduled confirmed and cancelled 30 minutes or more before entry time (AppointmentTime) -> 100% refund.
    /// Cancelled later (now &gt; AppointmentTime - 30m) -> prepaid amount is forfeited.
    /// BR-03.b: HoldWhileDriving confirmed and cancelled within 5 minutes (half-open: now &lt; ConfirmedAt + 5m) -> 100% refund.
    /// After that (now &gt;= ConfirmedAt + 5m) -> deposit is forfeited.
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
            if (r.ConfirmedAt.HasValue)
            {
                var cutoff = r.AppointmentTime.Subtract(ScheduledRefundWindow);
                if (now.ToUniversalTime() <= cutoff.ToUniversalTime())
                {
                    return new CancellationRefundDecision.Refund(r.PrepaidAmount);
                }

                return new CancellationRefundDecision.Forfeit();
            }

            return new CancellationRefundDecision.NoMoney();
        }

        return new CancellationRefundDecision.NoMoney();
    }
}
