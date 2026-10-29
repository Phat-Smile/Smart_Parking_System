using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Pricing;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetParkingSessionFee;

// Xử lý xem trước phí đỗ xe
public sealed class GetParkingSessionFeeQueryHandler(
    IParkingSessionGateway parkingGateway,
    IPrepaymentReader prepaymentReader,
    IClock clock)
    : IRequestHandler<GetParkingSessionFeeQuery, Result<ParkingSessionFeeResponse>>
{
    public async Task<Result<ParkingSessionFeeResponse>> Handle(
        GetParkingSessionFeeQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra định dạng mã phiên gửi lên
        if (!Guid.TryParse(request.SessionId, out var sessionGuid)
            || sessionGuid == Guid.Empty)
        {
            return Result<ParkingSessionFeeResponse>.Failure(
                ParkingFeeErrors.SessionIdInvalid);
        }

        // 2. Gọi dịch vụ bãi xe lấy dữ liệu phiên và bảng giá
        var ctxResult = await parkingGateway.GetFeeContextAsync(
            sessionGuid,
            request.CallerAccountId,
            request.CallerRole,
            cancellationToken);

        if (ctxResult.IsFailure)
        {
            return Result<ParkingSessionFeeResponse>.Failure(ctxResult.Error!);
        }

        var ctx = ctxResult.Value!;

        // 3. Chỉ tính tiền xem trước khi xe đang đỗ
        if (!string.Equals(ctx.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Result<ParkingSessionFeeResponse>.Failure(
                ParkingFeeErrors.SessionNotActive);
        }

        // 4. Tính phí qua dịch vụ của tầng nghiệp vụ
        var nowUtc = clock.UtcNow;

        var method = string.Equals(
            ctx.Pricing.CalculationMethod, "Block", StringComparison.OrdinalIgnoreCase)
            ? ParkingMethod.PerBlock
            : ParkingMethod.PerTurn;

        var fee = ParkingFeeCalculator.Calculate(
            method,
            ctx.Pricing.BlockMinutes,
            ctx.Pricing.PricePerUnit,
            ctx.EntryTimeUtc,
            nowUtc);

        // 5. Trừ tiền cọc nếu có đặt chỗ trước
        long depositAmount = 0;
        if (ctx.BookingId.HasValue && ctx.AccountId.HasValue)
        {
            depositAmount = await prepaymentReader.GetPrepaidAmountAsync(
                ctx.AccountId.Value,
                ctx.BookingId.Value,
                cancellationToken);
        }

        var amountDue = Math.Max(0, fee.TotalFee - depositAmount);

        // 6. Trả về kết quả tính tiền thành công
        return Result<ParkingSessionFeeResponse>.Success(
            new ParkingSessionFeeResponse(
                SessionId: ctx.SessionId,
                PlateNumber: ctx.PlateNumber,
                VehicleType: ctx.VehicleType,
                EntryTimeUtc: ctx.EntryTimeUtc,
                CalculatedAtUtc: nowUtc,
                DurationMinutes: fee.DurationMinutes,
                PricingPolicyId: ctx.Pricing.PolicyId,
                CalculationMethod: ctx.Pricing.CalculationMethod,
                TotalFee: fee.TotalFee,
                DepositAmount: depositAmount,
                AmountDue: amountDue));
    }
}
