using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.CreateVnPayParkingFeePayment;

// Lệnh tạo thanh toán tiền gửi xe qua VNPay
public sealed record CreateVnPayParkingFeePaymentCommand(
    Guid SessionId, // Mã phiên đỗ xe cần thanh toán
    long ExpectedAmountDue, // Số tiền khách đồng ý trả
    string? IdempotencyKey, // Khóa chống gửi trùng yêu cầu
    Guid? CallerAccountId, // Mã tài khoản người gửi yêu cầu
    string? CallerRole, // Quyền hạn của người gọi
    string? StaffLotId = null, // Mã bãi xe của nhân viên
    string? ClientIp = null // Địa chỉ IP máy người dùng
) : IRequest<Result<CreateVnPayParkingFeePaymentResponse>>;
