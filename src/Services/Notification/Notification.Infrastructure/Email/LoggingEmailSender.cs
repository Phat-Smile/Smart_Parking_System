using Microsoft.Extensions.Logging;
using ParkingSystem.Notification.Application.Common.Email;

namespace ParkingSystem.Notification.Infrastructure.Email;

/// <summary>
/// A zero-dependency email sender that logs rendered email details and OTP codes directly to application logs.
/// Ideal for local development, CI/CD pipelines, and zero-config cloud deployments.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendVerificationOtpEmailAsync(
        string email,
        string otpCode,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default) =>
        Log(email, otpCode, expiresAtUtc, "[Smart Parking] Mã xác thực tài khoản của bạn");

    public Task SendPasswordResetOtpEmailAsync(
        string email,
        string otpCode,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default) =>
        Log(email, otpCode, expiresAtUtc, "[Smart Parking] Mã đặt lại mật khẩu");

    private Task Log(string email, string otpCode, DateTimeOffset expiresAtUtc, string subject)
    {
        logger.LogInformation(
            """

            ======================= [DEV EMAIL DISPATCH] =======================
            To: {Email}
            Subject: {Subject}
            OTP Code: {OtpCode}
            Expires At: {ExpiresAtLocal:HH:mm:ss dd/MM/yyyy}
            ====================================================================
            """,
            email,
            subject,
            otpCode,
            expiresAtUtc.ToLocalTime());

        return Task.CompletedTask;
    }
}
