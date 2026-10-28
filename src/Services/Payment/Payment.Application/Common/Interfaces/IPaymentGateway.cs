namespace ParkingSystem.Payment.Application.Common.Interfaces;

public interface IPaymentGateway
{
    string CreatePaymentUrl(
        string txnRef,
        decimal amount,
        DateTimeOffset expiresAtUtc,
        string clientIp);

    // Xác thực chữ ký HMAC-SHA512 từ Webhook của VNPay
    bool VerifyIpnSignature(IReadOnlyDictionary<string, string> queryParameters);
}
