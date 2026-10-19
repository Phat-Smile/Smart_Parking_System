namespace ParkingSystem.Payment.Application.Common.Interfaces;

/// <summary>
/// Dữ liệu đầu vào để ghi nhật ký kiểm toán.
/// </summary>
public sealed record AuditEntryInput(
    string ActionType,
    string TargetType,
    string TargetId,
    string Reason,
    Guid? ParkingLotId = null,
    Dictionary<string, object?>? Details = null);

/// <summary>
/// Cổng ghi nhật ký kiểm toán trong cùng transaction nghiệp vụ.
/// </summary>
public interface IAuditWriter
{
    Task WriteAsync(AuditEntryInput entry, CancellationToken cancellationToken = default);
}
