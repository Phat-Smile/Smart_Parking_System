using ParkingSystem.Payment.Domain.Entities;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.Vnpay.Commands.ProcessVnpayIpn;

public interface IVnpayResultHandler
{
    VnpayTransactionPurpose Purpose { get; }
    Task<VnpayIpnResponseDto> HandleAsync(
        VnpayTransaction transaction,
        bool isPaymentSuccess,
        string vnpayTxnNo,
        string? bankCode,
        string responseCode,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);
}
