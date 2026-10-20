namespace ParkingSystem.Notification.Application.Common.Email;

public interface IEmailSender
{
    Task SendVerificationOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);

    Task SendPasswordResetOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);

    Task SendOwnerApplicationApprovedEmailAsync(string email, DateTimeOffset approvedAtUtc, CancellationToken cancellationToken = default);

    Task SendOwnerApplicationRejectedEmailAsync(string email, string reason, DateTimeOffset rejectedAtUtc, CancellationToken cancellationToken = default);
}

