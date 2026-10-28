using ParkingSystem.Payment.Application.Common.Interfaces;

namespace ParkingSystem.Payment.Persistence.Repositories;

// Cổng đọc tiền cọc tạm thời khi chưa có bảng ví tiền
public sealed class PrepaymentReader : IPrepaymentReader
{
    // Tạm thời trả về không đồng khi chưa có dữ liệu cọc
    public Task<long> GetPrepaidAmountAsync(
        Guid accountId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0L);
    }
}
