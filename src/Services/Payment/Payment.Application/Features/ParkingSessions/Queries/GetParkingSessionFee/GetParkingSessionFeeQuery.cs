using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingSessionFee;

// Yêu cầu tính tiền xem trước cho một phiên đỗ
public sealed record GetParkingSessionFeeQuery(
    string SessionId,       // Mã phiên đỗ xe người dùng gửi lên
    Guid? CallerAccountId,  // Mã tài khoản người đang yêu cầu xem phí
    string CallerRole)      // Vai trò người đang yêu cầu xem phí
    : IRequest<Result<ParkingSessionFeeResponse>>;
