using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkingSystem.Notification.Application.Common.Email;

namespace ParkingSystem.Notification.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IConfiguration configuration,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendVerificationOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default)
    {
        var host = configuration["SMTP_HOST"] ?? configuration["Smtp:Host"] ?? "smtp.gmail.com";
        var portStr = configuration["SMTP_PORT"] ?? configuration["Smtp:Port"] ?? "587";
        var port = int.TryParse(portStr, out var p) ? p : 587;
        var username = configuration["SMTP_USERNAME"] ?? configuration["Smtp:Username"];
        var password = configuration["SMTP_PASSWORD"] ?? configuration["Smtp:Password"];
        var fromAddress = configuration["SMTP_FROM_ADDRESS"] ?? configuration["Smtp:FromAddress"] ?? username;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("SMTP credentials are not configured. Skipping email dispatch to {Email}.", email);
            return;
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(username.Trim(), password.Trim()),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromAddress!.Trim(), "Smart Parking System"),
                Subject = "[Smart Parking] Mã xác thực tài khoản của bạn",
                Body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;">
                    <div style="text-align: center; margin-bottom: 24px;">
                        <h2 style="color: #2563eb; margin: 0; font-size: 24px;">Smart Parking System</h2>
                        <p style="color: #64748b; font-size: 14px; margin-top: 4px;">Hệ thống bãi đỗ xe thông minh</p>
                    </div>
                    <div style="padding: 20px; background-color: #f8fafc; border-radius: 8px; margin-bottom: 24px;">
                        <p style="color: #334155; font-size: 16px; margin: 0 0 12px 0;">Xin chào,</p>
                        <p style="color: #475569; font-size: 14px; margin: 0 0 20px 0;">
                            Cảm ơn bạn đã đăng ký tài khoản tại <strong>Smart Parking System</strong>. Dưới đây là mã xác thực OTP (One-Time Password) để hoàn tất kích hoạt tài khoản của bạn:
                        </p>
                        <div style="text-align: center; margin: 24px 0;">
                            <span style="display: inline-block; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #1e293b; background: #ffffff; padding: 12px 28px; border-radius: 8px; border: 2px dashed #3b82f6; box-shadow: 0 2px 4px rgba(0,0,0,0.05);">
                                {otpCode}
                            </span>
                        </div>
                        <p style="color: #ef4444; font-size: 13px; text-align: center; margin: 0;">
                            ⚠️ Mã có hiệu lực trong vòng <strong>5 phút</strong> (đến {expiresAtUtc.ToLocalTime():HH:mm:ss dd/MM/yyyy}).
                        </p>
                    </div>
                    <p style="color: #64748b; font-size: 13px; line-height: 1.5; margin: 0 0 16px 0;">
                        Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này. Tài khoản của bạn sẽ không bị ảnh hưởng.
                    </p>
                    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 20px 0;" />
                    <p style="font-size: 12px; color: #94a3b8; text-align: center; margin: 0;">
                        Email này được gửi tự động từ hệ thống Smart Parking System OJT. Vui lòng không trả lời thư này.
                    </p>
                </div>
                """,
                IsBodyHtml = true
            };

            mailMessage.To.Add(email.Trim());
            await client.SendMailAsync(mailMessage, cancellationToken);
            logger.LogInformation("Successfully sent OTP email to {Email}", email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OTP email to {Email}", email);
        }
    }
}
