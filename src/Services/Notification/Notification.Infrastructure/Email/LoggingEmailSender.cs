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

    public Task SendOwnerApplicationApprovedEmailAsync(
        string email,
        DateTimeOffset approvedAtUtc,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            """

            ======================= [DEV EMAIL DISPATCH] =======================
            To: {Email}
            Subject: [Smart Parking] Hồ sơ đăng ký Chủ bãi đã được phê duyệt!
            Status: APPROVED
            Approved At: {ApprovedAtLocal:HH:mm:ss dd/MM/yyyy}
            Message: Chúc mừng! Hồ sơ đăng ký đối tác Chủ bãi đỗ xe của bạn đã được phê duyệt thành công. Vui lòng đăng nhập lại để bắt đầu quản lý bãi xe.
            ====================================================================
            """,
            email,
            approvedAtUtc.ToLocalTime());

        return Task.CompletedTask;
    }

    public Task SendOwnerApplicationRejectedEmailAsync(
        string email,
        string reason,
        DateTimeOffset rejectedAtUtc,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            """

            ======================= [DEV EMAIL DISPATCH] =======================
            To: {Email}
            Subject: [Smart Parking] Thông báo về hồ sơ đăng ký Chủ bãi
            Status: REJECTED
            Reason: {Reason}
            Rejected At: {RejectedAtLocal:HH:mm:ss dd/MM/yyyy}
            Message: Rất tiếc, hồ sơ đăng ký đối tác Chủ bãi của bạn chưa được duyệt vì lý do: {Reason}. Bạn có thể cập nhật thông tin và nộp lại hồ sơ.
            ====================================================================
            """,
            email,
            reason,
            rejectedAtUtc.ToLocalTime(),
            reason);

        return Task.CompletedTask;
    }

    public Task SendReservationArrivalReminderEmailAsync(
        string email,
        string reminderType,
        DateTime scheduledArrivalTimeUtc,
        DateTime cutoffTimeUtc,
        string? maskedPlateNumber,
        CancellationToken cancellationToken = default)
    {
        var subject = reminderType == "FiveMinutesBeforeNoShow"
            ? "[Smart Parking] Cảnh báo: Sắp hết hạn nhận chỗ đỗ xe"
            : "[Smart Parking] Nhắc nhở: Sắp đến giờ nhận chỗ đỗ xe";

        logger.LogInformation(
            """

            ======================= [DEV EMAIL DISPATCH] =======================
            To: {Email}
            Subject: {Subject}
            Reminder Type: {ReminderType}
            Plate: {MaskedPlate}
            Scheduled Arrival: {ArrivalTimeUtc:HH:mm:ss dd/MM/yyyy} UTC
            Cutoff Time: {CutoffTimeUtc:HH:mm:ss dd/MM/yyyy} UTC
            ====================================================================
            """,
            email,
            subject,
            reminderType,
            maskedPlateNumber ?? "N/A",
            scheduledArrivalTimeUtc,
            cutoffTimeUtc);

        return Task.CompletedTask;
    }

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

    public Task SendReservationConfirmedEmailAsync(
        string email,
        Guid reservationId,
        DateTimeOffset appointmentTime,
        decimal prepaidAmount,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            """

            ======================= [DEV EMAIL DISPATCH] =======================
            To: {Email}
            Subject: [Smart Parking] Xác nhận đặt chỗ thành công!
            ReservationId: {ReservationId}
            Appointment: {AppointmentTimeLocal:HH:mm:ss dd/MM/yyyy}
            Prepaid: {PrepaidAmount:N0} VND
            ====================================================================
            """,
            email,
            reservationId,
            appointmentTime.ToLocalTime(),
            prepaidAmount);

        return Task.CompletedTask;
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
        logger.LogInformation(
            """

            ======================= [DEV EMAIL DISPATCH] =======================
            To: {Email}
            Subject: [Smart Parking] Hóa đơn điện tử quyết toán phí đỗ xe
            SessionId: {SessionId}
            Plate: {MaskedPlate}
            PaymentTime: {PaymentTimeLocal:HH:mm:ss dd/MM/yyyy}
            ExitExpires: {ExitWindowExpiresLocal:HH:mm:ss dd/MM/yyyy}
            Total: {TotalFee:N0} VND
            Paid: {PaidAmount:N0} VND
            Due: {AmountDue:N0} VND
            ====================================================================
            """,
            email,
            sessionId,
            maskedPlateNumber,
            paymentTimeUtc.ToLocalTime(),
            exitWindowExpiresUtc.ToLocalTime(),
            totalFee,
            paidAmount,
            amountDue);

        return Task.CompletedTask;
    }
}
