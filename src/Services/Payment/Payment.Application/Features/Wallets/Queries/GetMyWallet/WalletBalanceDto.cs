namespace ParkingSystem.Payment.Application.Features.Wallets.Queries.GetMyWallet;

public class WalletLimitsDto
{
    public decimal MinTopUp { get; set; }
    public decimal MaxTopUp { get; set; }
    public decimal MaxBalance { get; set; }
}

public class PendingTopUpDto
{
    public string TopupId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public class WalletBalanceDto
{
    public Guid? WalletId { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsDemo { get; set; } = true;
    public string DemoNotice { get; set; } = string.Empty;
    public WalletLimitsDto Limits { get; set; } = new WalletLimitsDto();
    public string GatewayMode { get; set; } = "Sandbox";
    public IReadOnlyList<PendingTopUpDto> PendingTopUps { get; set; } = new List<PendingTopUpDto>();
}
