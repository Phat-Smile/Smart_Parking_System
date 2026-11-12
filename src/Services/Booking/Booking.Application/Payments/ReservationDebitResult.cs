namespace ParkingSystem.Booking.Application.Payments;

public sealed record ReservationDebitResult(
    ReservationDebitOutcome Outcome,
    string? LedgerEntryId,
    long BalanceAfterVnd);

