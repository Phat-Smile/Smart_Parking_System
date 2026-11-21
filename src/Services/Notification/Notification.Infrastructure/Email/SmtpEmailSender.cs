using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ParkingSystem.Notification.Application.Common.Email;

namespace ParkingSystem.Notification.Infrastructure.Email;

/// <summary>
/// Dispatches verification OTP emails via standard SMTP (supports Mailpit and external SMTP providers).
/// Re-throws exceptions on dispatch failure to ensure MassTransit consumer retry policy and DLQ are activated.
/// </summary>
public sealed class SmtpEmailSender(
    IConfiguration configuration,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task SendVerificationOtpEmailAsync(
        string email,
        string otpCode,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default) =>
        SendOtpEmailAsync(
            email,
            otpCode,
            expiresAtUtc,
            "[Smart Parking] Mã xác thực tài khoản của bạn",
            "Cảm ơn bạn đã đăng ký tài khoản tại <strong>Smart Parking System</strong>. Dưới đây là mã xác thực OTP (One-Time Password) để hoàn tất kích hoạt tài khoản của bạn:",
            cancellationToken);

    public Task SendPasswordResetOtpEmailAsync(
        string email,
        string otpCode,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default) =>
        SendOtpEmailAsync(
            email,
            otpCode,
            expiresAtUtc,
            "[Smart Parking] Mã đặt lại mật khẩu",
            "Bạn vừa yêu cầu đặt lại mật khẩu tại <strong>Smart Parking System</strong>. Nhập mã dưới đây cùng mật khẩu mới; sau khi đặt lại, mọi thiết bị đang đăng nhập sẽ bị đăng xuất:",
            cancellationToken);

    public Task SendOwnerApplicationApprovedEmailAsync(
        string email,
        DateTimeOffset approvedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var subject = "[Smart Parking] Hồ sơ đăng ký Chủ bãi đã được phê duyệt";
        var body = $"""
        <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; max-width: 580px; margin: 0 auto; padding: 32px 24px; background-color: #0c0d0e; color: #f4f4f5; border-radius: 16px; border: 1px solid #27272a;">
            <!-- Brand Header -->
            <div style="text-align: center; margin-bottom: 32px;">
                <h1 style="color: #ffffff; margin: 0; font-size: 24px; font-weight: 800; letter-spacing: 2px;">PARKWISE</h1>
                <p style="color: #a1a1aa; font-size: 12px; margin-top: 6px; text-transform: uppercase; letter-spacing: 1.5px;">Hệ thống bãi đỗ xe thông minh</p>
            </div>

            <!-- Main Content Card -->
            <div style="padding: 28px 24px; background-color: #18181b; border: 1px solid #27272a; border-radius: 12px; margin-bottom: 24px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);">
                <p style="color: #fafafa; font-size: 16px; font-weight: 600; margin: 0 0 12px 0;">Xin chào,</p>
                <p style="color: #d4d4d8; font-size: 14px; line-height: 1.6; margin: 0 0 24px 0;">
                    Chúc mừng bạn! Hồ sơ đăng ký trở thành đối tác <strong>Chủ bãi đỗ xe (Parking Lot Owner)</strong> của bạn đã được Ban quản trị phê duyệt thành công vào lúc <strong>{approvedAtUtc.ToLocalTime():HH:mm:ss dd/MM/yyyy}</strong>.
                </p>

                <!-- Success Box -->
                <div style="text-align: center; margin: 24px 0;">
                    <div style="display: inline-block; padding: 16px 24px; background: rgba(34, 197, 94, 0.1); border: 1px solid rgba(34, 197, 94, 0.3); border-radius: 10px;">
                        <span style="color: #4ade80; font-size: 15px; font-weight: 600;">
                            🎉 Quyền Chủ bãi đỗ xe đã được kích hoạt thành công
                        </span>
                    </div>
                </div>

                <p style="color: #a1a1aa; font-size: 13px; line-height: 1.6; margin: 0; text-align: center;">
                    Vui lòng <strong>đăng nhập lại</strong> vào ứng dụng hoặc trang quản trị ParkWise để hệ thống cấp mới phiên làm việc với đầy đủ quyền hạn Chủ bãi.
                </p>
            </div>

            <hr style="border: none; border-top: 1px solid #27272a; margin: 24px 0;" />

            <!-- Footer -->
            <p style="font-size: 11px; color: #52525b; text-align: center; margin: 0; line-height: 1.5;">
                Email được gửi tự động từ <strong>ParkWise Smart Parking System</strong>.<br/>
                Vui lòng không trả lời trực tiếp email này.
            </p>
        </div>
        """;

        return SendMailCoreAsync(email, subject, body, "OwnerApplicationApproved", cancellationToken);
    }

    public Task SendOwnerApplicationRejectedEmailAsync(
        string email,
        string reason,
        DateTimeOffset rejectedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var encodedReason = WebUtility.HtmlEncode(reason);
        var subject = "[Smart Parking] Thông báo kết quả xét duyệt hồ sơ Chủ bãi";
        var body = $"""
        <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; max-width: 580px; margin: 0 auto; padding: 32px 24px; background-color: #0c0d0e; color: #f4f4f5; border-radius: 16px; border: 1px solid #27272a;">
            <!-- Brand Header -->
            <div style="text-align: center; margin-bottom: 32px;">
                <h1 style="color: #ffffff; margin: 0; font-size: 24px; font-weight: 800; letter-spacing: 2px;">PARKWISE</h1>
                <p style="color: #a1a1aa; font-size: 12px; margin-top: 6px; text-transform: uppercase; letter-spacing: 1.5px;">Hệ thống bãi đỗ xe thông minh</p>
            </div>

            <!-- Main Content Card -->
            <div style="padding: 28px 24px; background-color: #18181b; border: 1px solid #27272a; border-radius: 12px; margin-bottom: 24px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);">
                <p style="color: #fafafa; font-size: 16px; font-weight: 600; margin: 0 0 12px 0;">Xin chào,</p>
                <p style="color: #d4d4d8; font-size: 14px; line-height: 1.6; margin: 0 0 20px 0;">
                    Ban quản trị <strong>Smart Parking System</strong> đã hoàn tất xem xét hồ sơ đăng ký đối tác Chủ bãi đỗ xe của bạn vào lúc <strong>{rejectedAtUtc.ToLocalTime():HH:mm:ss dd/MM/yyyy}</strong>.
                </p>

                <!-- Rejection Notice Box -->
                <div style="padding: 16px 20px; background: rgba(239, 68, 68, 0.08); border: 1px solid rgba(239, 68, 68, 0.25); border-radius: 10px; margin: 20px 0;">
                    <p style="color: #f87171; font-size: 13px; font-weight: 600; margin: 0 0 6px 0; text-transform: uppercase; letter-spacing: 0.5px;">
                        Lý do từ chối:
                    </p>
                    <p style="color: #fca5a5; font-size: 14px; line-height: 1.5; margin: 0;">
                        {encodedReason}
                    </p>
                </div>

                <p style="color: #a1a1aa; font-size: 13px; line-height: 1.6; margin: 0; text-align: center;">
                    Bạn có thể cập nhật thông tin, chuẩn bị lại các chứng từ cần thiết và gửi lại yêu cầu xét duyệt trên hệ thống bất cứ lúc nào.
                </p>
            </div>

            <hr style="border: none; border-top: 1px solid #27272a; margin: 24px 0;" />

            <!-- Footer -->
            <p style="font-size: 11px; color: #52525b; text-align: center; margin: 0; line-height: 1.5;">
                Email được gửi tự động từ <strong>ParkWise Smart Parking System</strong>.<br/>
                Vui lòng không trả lời trực tiếp email này.
            </p>
        </div>
        """;

        return SendMailCoreAsync(email, subject, body, "OwnerApplicationRejected", cancellationToken);
    }

    public Task SendReservationConfirmedEmailAsync(
        string email,
        Guid reservationId,
        DateTimeOffset appointmentTime,
        decimal prepaidAmount,
        CancellationToken cancellationToken = default)
    {
        var subject = "[Smart Parking] Xác nhận đặt chỗ đỗ xe thành công";
        var body = $"""
        <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; max-width: 580px; margin: 0 auto; padding: 32px 24px; background-color: #0c0d0e; color: #f4f4f5; border-radius: 16px; border: 1px solid #27272a;">
            <div style="text-align: center; margin-bottom: 32px;">
                <h1 style="color: #ffffff; margin: 0; font-size: 24px; font-weight: 800; letter-spacing: 2px;">PARKWISE</h1>
                <p style="color: #a1a1aa; font-size: 12px; margin-top: 6px; text-transform: uppercase; letter-spacing: 1.5px;">Hệ thống bãi đỗ xe thông minh</p>
            </div>
            <div style="padding: 28px 24px; background-color: #18181b; border: 1px solid #27272a; border-radius: 12px; margin-bottom: 24px;">
                <p style="color: #fafafa; font-size: 16px; font-weight: 600; margin: 0 0 12px 0;">Xin chào,</p>
                <p style="color: #d4d4d8; font-size: 14px; line-height: 1.6; margin: 0 0 16px 0;">
                    Yêu cầu đặt chỗ của bạn tại Smart Parking System đã được xác nhận thành công.
                </p>
                <div style="padding: 16px; background-color: #09090b; border-radius: 8px; border: 1px solid #3f3f46; margin-bottom: 16px;">
                    <p style="margin: 4px 0; color: #a1a1aa; font-size: 13px;">Mã đặt chỗ: <strong style="color: #ffffff;">{reservationId}</strong></p>
                    <p style="margin: 4px 0; color: #a1a1aa; font-size: 13px;">Thời gian hẹn: <strong style="color: #ffffff;">{appointmentTime.ToLocalTime():HH:mm dd/MM/yyyy}</strong></p>
                    <p style="margin: 4px 0; color: #a1a1aa; font-size: 13px;">Tiền cọc giữ chỗ: <strong style="color: #4ade80;">{prepaidAmount:N0} VND</strong></p>
                </div>
            </div>
            <hr style="border: none; border-top: 1px solid #27272a; margin: 24px 0;" />
            <p style="font-size: 11px; color: #52525b; text-align: center; margin: 0; line-height: 1.5;">
                Email được gửi tự động từ <strong>ParkWise Smart Parking System</strong>.<br/>
                Vui lòng không trả lời trực tiếp email này.
            </p>
        </div>
        """;

        return SendMailCoreAsync(email, subject, body, "ReservationConfirmed", cancellationToken);
    }

    public Task SendElectronicReceiptEmailAsync(
        string email,
        Guid sessionId,
        string maskedPlateNumber,
        long totalFee,
        long paidAmount,
        long amountDue,
        DateTimeOffset paymentTimeUtc,
        DateTimeOffset exitWindowExpiresUtc,
        CancellationToken cancellationToken = default)
    {
        var subject = "[Smart Parking] Hóa đơn điện tử quyết toán phí đỗ xe";
        var body = $"""
        <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; max-width: 580px; margin: 0 auto; padding: 32px 24px; background-color: #0c0d0e; color: #f4f4f5; border-radius: 16px; border: 1px solid #27272a;">
            <div style="text-align: center; margin-bottom: 32px;">
                <h1 style="color: #ffffff; margin: 0; font-size: 24px; font-weight: 800; letter-spacing: 2px;">PARKWISE</h1>
                <p style="color: #a1a1aa; font-size: 12px; margin-top: 6px; text-transform: uppercase; letter-spacing: 1.5px;">Hóa đơn điện tử quyết toán</p>
            </div>
            <div style="padding: 28px 24px; background-color: #18181b; border: 1px solid #27272a; border-radius: 12px; margin-bottom: 24px;">
                <p style="color: #fafafa; font-size: 16px; font-weight: 600; margin: 0 0 12px 0;">Xin chào,</p>
                <p style="color: #d4d4d8; font-size: 14px; line-height: 1.6; margin: 0 0 16px 0;">
                    Phiên đỗ xe của bạn đã hoàn tất quyết toán. Chi tiết hóa đơn điện tử:
                </p>
                <div style="padding: 16px; background-color: #09090b; border-radius: 8px; border: 1px solid #3f3f46; margin-bottom: 16px;">
                    <p style="margin: 4px 0; color: #a1a1aa; font-size: 13px;">Biển số xe: <strong style="color: #ffffff;">{maskedPlateNumber}</strong></p>
                    <p style="margin: 4px 0; color: #a1a1aa; font-size: 13px;">Giờ thanh toán: <strong style="color: #ffffff;">{paymentTimeUtc.ToLocalTime():HH:mm dd/MM/yyyy}</strong></p>
                    <p style="margin: 4px 0; color: #a1a1aa; font-size: 13px;">Hạn ra cổng: <strong style="color: #ffffff;">{exitWindowExpiresUtc.ToLocalTime():HH:mm dd/MM/yyyy}</strong></p>
                    <p style="margin: 4px 0; color: #a1a1aa; font-size: 13px;">Tổng phí đỗ: <strong style="color: #ffffff;">{totalFee:N0} VND</strong></p>
                    <p style="margin: 4px 0; color: #a1a1aa; font-size: 13px;">Đã thanh toán: <strong style="color: #4ade80;">{paidAmount:N0} VND</strong></p>
                    <p style="margin: 4px 0; color: #a1a1aa; font-size: 13px;">Còn phải nạp: <strong style="color: #f87171;">{amountDue:N0} VND</strong></p>
                </div>
            </div>
            <hr style="border: none; border-top: 1px solid #27272a; margin: 24px 0;" />
            <p style="font-size: 11px; color: #52525b; text-align: center; margin: 0; line-height: 1.5;">
                Email được gửi tự động từ <strong>ParkWise Smart Parking System</strong>.<br/>
                Vui lòng không trả lời trực tiếp email này.
            </p>
        </div>
        """;

        return SendMailCoreAsync(email, subject, body, "ElectronicReceipt", cancellationToken);
    }

    private Task SendOtpEmailAsync(
        string email,
        string otpCode,
        DateTimeOffset expiresAtUtc,
        string subject,
        string introHtml,
        CancellationToken cancellationToken)
    {
        var body = $"""
        <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; max-width: 580px; margin: 0 auto; padding: 32px 24px; background-color: #0c0d0e; color: #f4f4f5; border-radius: 16px; border: 1px solid #27272a;">
            <!-- Brand Header -->
            <div style="text-align: center; margin-bottom: 32px;">
                <h1 style="color: #ffffff; margin: 0; font-size: 24px; font-weight: 800; letter-spacing: 2px;">PARKWISE</h1>
                <p style="color: #a1a1aa; font-size: 12px; margin-top: 6px; text-transform: uppercase; letter-spacing: 1.5px;">Hệ thống bãi đỗ xe thông minh</p>
            </div>

            <!-- Main Content Card -->
            <div style="padding: 28px 24px; background-color: #18181b; border: 1px solid #27272a; border-radius: 12px; margin-bottom: 24px; box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);">
                <p style="color: #fafafa; font-size: 16px; font-weight: 600; margin: 0 0 12px 0;">Xin chào,</p>
                <p style="color: #d4d4d8; font-size: 14px; line-height: 1.6; margin: 0 0 24px 0;">
                    {introHtml}
                </p>

                <!-- OTP Box -->
                <div style="text-align: center; margin: 28px 0;">
                    <div style="display: inline-block; padding: 14px 32px; background: #09090b; border: 1px solid #3f3f46; border-radius: 10px; box-shadow: inset 0 2px 4px rgba(0,0,0,0.6);">
                        <span style="font-family: 'SF Mono', Monaco, Inconsolata, 'Fira Code', monospace; font-size: 34px; font-weight: 800; letter-spacing: 10px; color: #ffffff;">
                            {otpCode}
                        </span>
                    </div>
                </div>

                <!-- Notice -->
                <div style="text-align: center; margin-top: 16px;">
                    <p style="display: inline-block; margin: 0; padding: 6px 14px; background-color: rgba(239, 68, 68, 0.1); border: 1px solid rgba(239, 68, 68, 0.2); border-radius: 6px; color: #f87171; font-size: 13px; font-weight: 500;">
                        ⏱ Mã có hiệu lực trong vòng <strong>5 phút</strong> (đến {expiresAtUtc.ToLocalTime():HH:mm:ss dd/MM/yyyy})
                    </p>
                </div>
            </div>

            <!-- Security Reminder -->
            <p style="color: #71717a; font-size: 13px; line-height: 1.6; margin: 0 0 24px 0; text-align: center;">
                Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email. Tuyệt đối không chia sẻ mã này cho bất kỳ ai khác để bảo vệ an toàn tài khoản.
            </p>

            <hr style="border: none; border-top: 1px solid #27272a; margin: 24px 0;" />

            <!-- Footer -->
            <p style="font-size: 11px; color: #52525b; text-align: center; margin: 0; line-height: 1.5;">
                Email được gửi tự động từ <strong>ParkWise Smart Parking System</strong>.<br/>
                Vui lòng không trả lời trực tiếp email này.
            </p>
        </div>
        """;

        return SendMailCoreAsync(email, subject, body, "OTP", cancellationToken);
    }

    private async Task SendMailCoreAsync(
        string email,
        string subject,
        string bodyHtml,
        string emailTypeDescription,
        CancellationToken cancellationToken)
    {
        var host = configuration["SMTP_HOST"] ?? configuration["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new InvalidOperationException("SMTP host must be configured when SmtpEmailSender is registered.");
        }

        var portStr = configuration["SMTP_PORT"] ?? configuration["Smtp:Port"] ?? "587";
        var port = int.TryParse(portStr, out var p) ? p : 587;
        var username = configuration["SMTP_USERNAME"] ?? configuration["Smtp:Username"];
        var password = configuration["SMTP_PASSWORD"] ?? configuration["Smtp:Password"];
        var fromAddress = configuration["SMTP_FROM_ADDRESS"] ?? configuration["Smtp:FromAddress"] ?? (!string.IsNullOrWhiteSpace(username) ? username : "noreply@smartparking.local");

        var hasCredentials = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
        var isLocalOrMailpit = host.Equals("mailpit", StringComparison.OrdinalIgnoreCase)
                               || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                               || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                               || port == 1025;

        if (!hasCredentials && !isLocalOrMailpit)
        {
            throw new InvalidOperationException($"External SMTP host '{host}' specified without credentials.");
        }

        try
        {
            using var client = new SmtpClient(host, port);
            if (hasCredentials)
            {
                var sanitizedSecret = host.Contains("gmail", StringComparison.OrdinalIgnoreCase)
                    ? password!.Trim().Replace(" ", "")
                    : password!.Trim();
                client.Credentials = new NetworkCredential(username!.Trim(), sanitizedSecret);
                client.EnableSsl = port != 1025;
            }
            else
            {
                client.UseDefaultCredentials = false;
                client.EnableSsl = false;
            }

            var mailMessage = new MailMessage
            {
                From = new MailAddress(fromAddress!.Trim(), "Smart Parking System"),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };

            mailMessage.To.Add(email.Trim());
            await client.SendMailAsync(mailMessage, cancellationToken);
            logger.LogInformation("Successfully sent {EmailType} email to {MaskedEmail} via SMTP ({Host}:{Port})", emailTypeDescription, MaskEmail(email), host, port);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {EmailType} email to {MaskedEmail} via SMTP ({Host}:{Port}). Re-throwing for consumer retry.", emailTypeDescription, MaskEmail(email), host, port);
            throw;
        }
    }

    // The recipient address is personal data: keep the first character and the domain only, e.g. "a***@example.com".
    public static string MaskEmail(string email)
    {
        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@');
        return at <= 0 ? "***" : $"{trimmed[0]}***{trimmed[at..]}";
    }
}
