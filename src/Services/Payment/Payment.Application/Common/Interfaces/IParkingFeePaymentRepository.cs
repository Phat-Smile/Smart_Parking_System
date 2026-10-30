using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

// Cổng lưu hóa đơn thanh toán phí đỗ
public interface IParkingFeePaymentRepository
{
    Task<bool> ExistsForSessionAsync(Guid sessionId, CancellationToken ct);
    Task AddAsync(ParkingFeePayment payment, CancellationToken ct);
}
