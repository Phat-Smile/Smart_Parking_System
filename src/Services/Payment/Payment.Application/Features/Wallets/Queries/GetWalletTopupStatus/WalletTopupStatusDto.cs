namespace ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTopupStatus;

// Du lieu phan hoi trang thai nap tien theo dac ta API
public record WalletTopupStatusDto(
    string TopupId,
    long Amount,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset ExpiresAtUtc);
