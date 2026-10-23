namespace ParkingSystem.Payment.Application.Common.Options;

public class WalletOptions
{
    public const string SectionName = "Wallet";

    public decimal MinTopUpVnd { get; set; } = 10000m;
    public decimal MaxTopUpVnd { get; set; } = 5000000m;
    public decimal MaxBalanceVnd { get; set; } = 10000000m;
}
