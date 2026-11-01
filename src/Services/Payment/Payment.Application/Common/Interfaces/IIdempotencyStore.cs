using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

// Cổng tra cứu và lưu khóa chống trùng
public interface IIdempotencyStore
{
    Task<PaymentIdempotencyRecord?> FindAsync(Guid accountId, string operation, string key, CancellationToken ct);
    Task<bool> TryCreatePendingAsync(PaymentIdempotencyRecord record, CancellationToken ct);
    Task UpdateAsync(PaymentIdempotencyRecord record, CancellationToken ct);
    Task ReleasePendingAsync(Guid id, CancellationToken ct);
}
