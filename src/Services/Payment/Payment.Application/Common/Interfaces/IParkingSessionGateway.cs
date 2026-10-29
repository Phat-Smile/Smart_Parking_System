using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Common.Interfaces;

// Cổng giao tiếp sang dịch vụ bãi giữ xe
public interface IParkingSessionGateway
{
    // Lấy thông tin phiên đỗ và bảng giá từ bãi xe
    Task<Result<ParkingSessionFeeContext>> GetFeeContextAsync(
        Guid sessionId,                                 // Mã phiên đỗ xe cần tính tiền
        Guid? callerAccountId,                          // Mã người gọi, có thể trống nếu chưa có tài khoản
        string callerRole,                              // Vai trò người gọi: khách hàng, nhân viên hay chủ bãi
        CancellationToken cancellationToken = default); // Tín hiệu hủy lệnh nếu người dùng ngắt kết nối
}
