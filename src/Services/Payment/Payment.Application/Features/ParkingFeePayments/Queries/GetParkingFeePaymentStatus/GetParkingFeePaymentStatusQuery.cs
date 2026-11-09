using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.ParkingFeePayments.Queries.GetParkingFeePaymentStatus;

// Truy vấn trạng thái hóa đơn thanh toán tiền xe
public sealed record GetParkingFeePaymentStatusQuery(
    Guid TransactionId // Mã hóa đơn cần tra cứu
) : IRequest<Result<GetParkingFeePaymentStatusResponse>>;
