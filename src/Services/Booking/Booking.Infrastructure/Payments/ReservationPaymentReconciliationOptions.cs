namespace ParkingSystem.Booking.Infrastructure.Payments;

public sealed class ReservationPaymentReconciliationOptions
{
    public const string SectionName = "PaymentReconciliation";

    public int IntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 50;
}

