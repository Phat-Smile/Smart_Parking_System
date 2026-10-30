using Microsoft.EntityFrameworkCore;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Persistence.Repositories;

// Kho lưu trữ hóa đơn thanh toán tiền xe
public sealed class ParkingFeePaymentRepository(PaymentDbContext dbContext) : IParkingFeePaymentRepository
{
    public async Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct)
    {
        return await dbContext.Set<ParkingFeePayment>()
            .AnyAsync(p => p.SessionId == sessionId && p.Status == "Succeeded", ct);
    }

    public async Task AddAsync(ParkingFeePayment payment, CancellationToken ct)
    {
        await dbContext.Set<ParkingFeePayment>().AddAsync(payment, ct);
    }
}
