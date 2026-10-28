namespace ParkingSystem.Payment.Domain.Entities;

/// <summary>
/// Bản ghi nhật ký kiểm toán bất biến (Append-Only).
/// Lưu lại ai làm gì, lúc nào, tại bãi nào, lý do và chi tiết nghiệp vụ.
/// </summary>
public sealed class AuditLog
{
    // Id do ứng dụng tự sinh
    public Guid Id { get; private set; }

    // Số thứ tự tự tăng trong DB dùng làm tie-break khi sắp xếp
    public long Seq { get; private set; }

    // Thời điểm sự kiện thực tế xảy ra (UTC)
    public DateTime OccurredAtUtc { get; private set; }

    // Thời điểm lưu vào DB (UTC)
    public DateTime RecordedAtUtc { get; private set; }

    // Service phát sinh log (payment, parking, identity)
    public string SourceService { get; private set; } = string.Empty;

    // Id gốc từ service nguồn (bằng Id với Payment)
    public Guid SourceEntryId { get; private set; }

    // Loại hành động (ví dụ: CashPaymentConfirmed, WalletEntryReversed, PricingPolicyChanged)
    public string ActionType { get; private set; } = string.Empty;

    // Id tài khoản thực hiện (null nếu do hệ thống)
    public Guid? ActorId { get; private set; }

    // Snapshot email tại thời điểm thao tác
    public string? ActorEmail { get; private set; }

    // Snapshot vai trò (Staff, ParkingLotOwner, SystemAdmin, Customer, System)
    public string ActorRole { get; private set; } = string.Empty;

    // Bãi đỗ xe liên quan (null nếu là hành động toàn hệ thống)
    public Guid? ParkingLotId { get; private set; }

    // Loại đối tượng chịu tác động (ParkingSession, WalletLedgerEntry...)
    public string TargetType { get; private set; } = string.Empty;

    // Id của đối tượng chịu tác động
    public string TargetId { get; private set; } = string.Empty;

    // Lý do thực hiện hành động (bắt buộc, tối đa 500 ký tự)
    public string Reason { get; private set; } = string.Empty;

    // IP người gọi nhận diện qua Gateway
    public string? IpAddress { get; private set; }

    // Dữ liệu JSON chi tiết (đã kiểm tra an toàn, tối đa 4KB)
    public string? Details { get; private set; }

    // EF Core constructor
    private AuditLog() { }

    // Factory method tạo mới audit log từ Payment service
    public static AuditLog CreateFromPayment(
        Guid id,
        DateTime occurredAtUtc,
        DateTime recordedAtUtc,
        string actionType,
        Guid? actorId,
        string? actorEmail,
        string actorRole,
        Guid? parkingLotId,
        string targetType,
        string targetId,
        string reason,
        string? ipAddress,
        string? details)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Audit log id cannot be empty.", nameof(id));

        if (string.IsNullOrWhiteSpace(actionType))
            throw new ArgumentException("Action type cannot be empty.", nameof(actionType));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is mandatory for audit log.", nameof(reason));

        if (reason.Length > 500)
            throw new ArgumentException("Reason cannot exceed 500 characters.", nameof(reason));

        if (string.IsNullOrWhiteSpace(targetType) || string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("Target type and Target id are required.", nameof(targetType));

        return new AuditLog
        {
            Id = id,
            OccurredAtUtc = occurredAtUtc,
            RecordedAtUtc = recordedAtUtc,
            SourceService = "payment",
            SourceEntryId = id,
            ActionType = actionType,
            ActorId = actorId,
            ActorEmail = actorEmail,
            ActorRole = string.IsNullOrWhiteSpace(actorRole) ? "System" : actorRole,
            ParkingLotId = parkingLotId,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason.Trim(),
            IpAddress = ipAddress,
            Details = details
        };
    }
}
