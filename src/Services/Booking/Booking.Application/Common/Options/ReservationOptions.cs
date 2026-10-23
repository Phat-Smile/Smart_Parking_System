namespace ParkingSystem.Booking.Application.Common.Options;

/// <summary>
/// Configuration options for reservation deposits and related settings.
/// Specifically prepares the configuration consumed by the Hold While Driving workflow (US-OWNER-005).
/// </summary>
public sealed class ReservationOptions
{
    public const string SectionName = "Reservation";

    /// <summary>
    /// Provisional development/test deposit amount in Vietnamese Dong (VND) for reservations.
    /// In Vietnam, currency amounts must be whole non-negative integers.
    /// The 20,000 VND value is a provisional fixture inferred from existing test scenarios;
    /// the approved Production deposit amount remains an open decision requiring Product Owner/Business Analyst confirmation.
    /// </summary>
    public decimal DepositAmountVnd { get; set; } = 20000m;

    /// <summary>
    /// Optional specific deposit amount in VND for Hold While Driving (instant hold) reservations.
    /// When specified, this overrides <see cref="DepositAmountVnd"/> for the Hold While Driving workflow.
    /// </summary>
    public decimal? HoldWhileDrivingDepositVnd { get; set; }

    /// <summary>
    /// Gets the effective deposit amount in VND for Hold While Driving reservations.
    /// </summary>
    public decimal GetHoldWhileDrivingDeposit() => HoldWhileDrivingDepositVnd ?? DepositAmountVnd;
}
