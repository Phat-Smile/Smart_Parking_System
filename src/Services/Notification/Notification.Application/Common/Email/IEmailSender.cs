namespace ParkingSystem.Notification.Application.Common.Email;

public interface IEmailSender
{
    Task SendVerificationOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);
}
