using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Repositories;

// Kho lưu trữ khóa chống trừ tiền lặp lại
public sealed class PaymentIdempotencyStore(PaymentDbContext dbContext) : IIdempotencyStore
{
    public async Task<PaymentIdempotencyRecord?> FindAsync(Guid accountId, string operation, string key, CancellationToken ct)
    {
        return await dbContext.Set<PaymentIdempotencyRecord>()
            .FirstOrDefaultAsync(r => r.AccountId == accountId && r.Operation == operation && r.IdempotencyKey == key, ct);
    }

    public async Task<bool> TryCreatePendingAsync(PaymentIdempotencyRecord record, CancellationToken ct)
    {
        try
        {
            await dbContext.Set<PaymentIdempotencyRecord>().AddAsync(record, ct);
            await dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Trùng khóa trong database
            return false;
        }
    }

    public async Task UpdateAsync(PaymentIdempotencyRecord record, CancellationToken ct)
    {
        dbContext.Set<PaymentIdempotencyRecord>().Update(record);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task ReleasePendingAsync(Guid id, CancellationToken ct)
    {
        var record = await dbContext.Set<PaymentIdempotencyRecord>().FindAsync([id], ct);
        if (record != null && record.Status == "Pending")
        {
            dbContext.Set<PaymentIdempotencyRecord>().Remove(record);
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
