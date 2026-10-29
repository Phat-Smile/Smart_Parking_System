using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;

namespace ParkingSystem.Payment.Infrastructure.Adapters.Parking;

// Cổng giả lập tạm thời khi dịch vụ bãi xe chưa có hợp đồng
public sealed class StubParkingSessionGateway : IParkingSessionGateway
{
    // Tạm thời báo dịch vụ chưa sẵn sàng khi chưa có kết nối thật
    public Task<Result<ParkingSessionFeeContext>> GetFeeContextAsync(
        Guid sessionId,
        Guid? callerAccountId,
        string callerRole,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Result<ParkingSessionFeeContext>.Failure(
                ParkingFeeErrors.ParkingServiceUnavailable));
    }
}
