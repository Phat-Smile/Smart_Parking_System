namespace ParkingSystem.Notification.Application.Common.Email;

public interface IEmailSender
{
    Task SendVerificationOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);

    Task SendPasswordResetOtpEmailAsync(string email, string otpCode, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken = default);

    Task SendOwnerApplicationApprovedEmailAsync(string email, DateTimeOffset approvedAtUtc, CancellationToken cancellationToken = default);

    Task SendOwnerApplicationRejectedEmailAsync(string email, string reason, DateTimeOffset rejectedAtUtc, CancellationToken cancellationToken = default);

    Task SendReservationConfirmedEmailAsync(
        string email,
        Guid reservationId,
        DateTimeOffset appointmentTime,
        decimal prepaidAmount,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task SendElectronicReceiptEmailAsync(
        string email,
        Guid sessionId,
        string maskedPlateNumber,
        long totalFee,
        long paidAmount,
        long amountDue,
        DateTimeOffset paymentTimeUtc,
        DateTimeOffset exitWindowExpiresUtc,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task SendReservationArrivalReminderEmailAsync(
        string email,
        string reminderType,
        DateTime scheduledArrivalTimeUtc,
        DateTime cutoffTimeUtc,
        string? maskedPlateNumber,
        CancellationToken cancellationToken = default);
}
