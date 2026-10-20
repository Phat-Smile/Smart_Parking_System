namespace ParkingSystem.Payment.Application.Common.Interfaces;

public interface IPaymentGateway
{
    string CreatePaymentUrl(
        string txnRef,
        decimal amount,
        DateTimeOffset expiresAtUtc,
        string clientIp);
}
