using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Application.Common.Utilities;
using ParkingSystem.Payment.Domain.Pricing;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetPublicParkingSessionFee;

/// <summary>
/// Handles anonymous public parking fee previews by Session QR token (Q-V2c).
/// Returns a uniform 404 (payment.session.not_found) for invalid QR, missing session,
/// or non-active sessions to protect against IDOR/enumeration attacks.
/// Does NOT generate or return any paymentUrl (Tin 200 Condition 4).
/// </summary>
public sealed class GetPublicParkingSessionFeeQueryHandler(
    IParkingSessionGateway parkingGateway,
    IPrepaymentReader prepaymentReader,
    IClock clock)
    : IRequestHandler<GetPublicParkingSessionFeeQuery, Result<PublicParkingSessionFeeResponse>>
{
    public async Task<Result<PublicParkingSessionFeeResponse>> Handle(
        GetPublicParkingSessionFeeQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Uniform 404 for empty or whitespace QR token
        if (string.IsNullOrWhiteSpace(request.QrToken))
        {
            return Result<PublicParkingSessionFeeResponse>.Failure(ParkingFeeErrors.SessionNotFound);
        }

        // 2. Fetch fee context from Parking service via gRPC
        var ctxResult = await parkingGateway.GetFeeContextByQrAsync(
            request.QrToken.Trim(),
            cancellationToken);

        if (ctxResult.IsFailure)
        {
            // Propagate service unavailability as 503; all other errors become uniform 404
            if (ctxResult.Error!.Code == ParkingFeeErrors.ParkingServiceUnavailable.Code)
            {
                return Result<PublicParkingSessionFeeResponse>.Failure(ctxResult.Error);
            }

            return Result<PublicParkingSessionFeeResponse>.Failure(ParkingFeeErrors.SessionNotFound);
        }

        var ctx = ctxResult.Value!;

        // 3. Uniform 404 if session is not Active (e.g. Completed, Cancelled, or Paid)
        if (!string.Equals(ctx.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Result<PublicParkingSessionFeeResponse>.Failure(ParkingFeeErrors.SessionNotFound);
        }

        // 4. Calculate current fee
        var nowUtc = clock.UtcNow;
        var method = string.Equals(ctx.Pricing.CalculationMethod, "Block", StringComparison.OrdinalIgnoreCase)
            ? ParkingMethod.PerBlock
            : ParkingMethod.PerTurn;

        var fee = ParkingFeeCalculator.Calculate(
            method,
            ctx.Pricing.BlockMinutes,
            ctx.Pricing.PricePerUnit,
            ctx.EntryTimeUtc,
            nowUtc);

        // 5. Account for prepayment / deposit if booking exists
        long depositAmount = 0;
        if (ctx.BookingId.HasValue && ctx.AccountId.HasValue)
        {
            depositAmount = await prepaymentReader.GetPrepaidAmountAsync(
                ctx.AccountId.Value,
                ctx.BookingId.Value,
                cancellationToken);
        }

        var amountDue = Math.Max(0, fee.TotalFee - depositAmount);

        // 6. Anonymize plate per NFR-15 and build response without paymentUrl
        var maskedPlate = LicensePlateMasker.MaskPlateNumber(ctx.PlateNumber);

        return Result<PublicParkingSessionFeeResponse>.Success(
            new PublicParkingSessionFeeResponse(
                SessionId: ctx.SessionId,
                MaskedPlateNumber: maskedPlate,
                Status: ctx.Status,
                EntryTimeUtc: ctx.EntryTimeUtc,
                CalculatedAtUtc: nowUtc,
                DurationMinutes: fee.DurationMinutes,
                CalculationMethod: ctx.Pricing.CalculationMethod,
                TotalFee: fee.TotalFee,
                PaidAmount: depositAmount,
                AmountDue: amountDue));
    }
}
