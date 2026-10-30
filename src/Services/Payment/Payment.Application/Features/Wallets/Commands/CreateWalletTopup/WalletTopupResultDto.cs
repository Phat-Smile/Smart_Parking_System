namespace ParkingSystem.Payment.Application.Features.Wallets.Commands.CreateWalletTopup;

public class WalletTopupResultDto
{
    public string TopupId { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public string? PaymentUrl { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }

    public bool IsReplay { get; set; }
}
