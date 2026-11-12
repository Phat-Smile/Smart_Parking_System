using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Commands.PayParkingFeeByWallet;

// Lệnh yêu cầu trừ ví trả tiền gửi xe
public sealed record PayParkingFeeByWalletCommand(
    Guid SessionId,                     // Mã phiên đỗ xe
    long ExpectedAmountDue,             // Số tiền khách đồng ý trả
    string IdempotencyKey,              // Khóa chống trừ tiền lặp lại
    Guid CallerAccountId,               // Mã tài khoản người bấm trả
    string CallerRole                   // Quyền của người bấm trả
) : IRequest<Result<PayParkingFeeByWalletResponse>>;
