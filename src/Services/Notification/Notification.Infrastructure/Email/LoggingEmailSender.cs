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
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            """

            ================================================================================
            📬 [NOTIFICATION SERVICE] EMAIL DISPATCH (ZERO-CONFIG / DEV FALLBACK)
            --------------------------------------------------------------------------------
            To:          {Email}
            Subject:     [Smart Parking] Mã xác thực tài khoản của bạn
            OTP Code:    >>> {OtpCode} <<<
            Expires At:  {ExpiresAtUtc} (Local: {ExpiresAtLocal:HH:mm:ss dd/MM/yyyy})
            Notice:      Use this OTP to complete email verification or login.
            ================================================================================
            """,
            email,
            otpCode,
            expiresAtUtc,
            expiresAtUtc.ToLocalTime());

        return Task.CompletedTask;
    }
}
