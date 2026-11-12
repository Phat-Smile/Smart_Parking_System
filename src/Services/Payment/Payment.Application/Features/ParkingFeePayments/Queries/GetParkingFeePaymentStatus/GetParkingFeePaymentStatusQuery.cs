using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.ParkingFeePayments.Queries.GetParkingFeePaymentStatus;

// Truy vấn trạng thái hóa đơn thanh toán tiền xe. Người gọi lấy từ JWT để kiểm quyền theo tài nguyên (chống IDOR).
public sealed record GetParkingFeePaymentStatusQuery(
    Guid TransactionId,
    Guid? CallerAccountId,
    string? CallerRole
) : IRequest<Result<GetParkingFeePaymentStatusResponse>>;
