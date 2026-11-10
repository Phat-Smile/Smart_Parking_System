namespace ParkingSystem.Booking.Application.Payments;

/// <summary>
/// Prepayment purpose classification distinguishing driving-hold deposit from full scheduled prepayment (US-RES-004, FR-PAY-007, MF-07).
/// </summary>
public enum PrepaymentPurpose
{
    Deposit,
    Scheduled
}

