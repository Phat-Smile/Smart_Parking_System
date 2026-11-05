using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.ConfirmCashParkingFeePayment;

// Yêu cầu nhân viên xác nhận thu tiền mặt
public sealed record ConfirmCashParkingFeePaymentCommand(
    Guid SessionId,
    Guid StaffAccountId,
    string StaffRole,
    Guid? StaffLotId,
    string IdempotencyKey,
    long ExpectedAmountDue,
    string? Reason = null) : IRequest<Result<ConfirmCashParkingFeePaymentResponse>>;
