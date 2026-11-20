using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Interfaces;

namespace ParkingSystem.Parking.Application.ParkingSessions.GetSessionFeeContext;

public sealed record GetSessionFeeContextByQrQuery(string QrToken) : IRequest<Result<SessionFeeContext>>;

public static class GetSessionFeeContextByQrErrors
{
    public static readonly Error InvalidQr =
        new("parking.session.qr_invalid", "The QR token is empty or invalid.");

    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "No open parking session found for the provided QR token.");

    public static readonly Error PricingNotFound =
        new("parking.session.pricing_not_found", "The lot had no price list in force when the vehicle entered.");
}

public sealed class GetSessionFeeContextByQrQueryHandler(
    IParkingSessionRepository sessions,
    IPricingPolicyRepository pricing)
    : IRequestHandler<GetSessionFeeContextByQrQuery, Result<SessionFeeContext>>
{
    public async Task<Result<SessionFeeContext>> Handle(GetSessionFeeContextByQrQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QrToken))
            return Result<SessionFeeContext>.Failure(GetSessionFeeContextByQrErrors.InvalidQr);

        // FindOpenByQrTokenAsync searches only for sessions with OpenStatuses (Active or Paid).
        // If the session does not exist, or has been Completed or Cancelled, it returns null.
        var session = await sessions.FindOpenByQrTokenAsync(request.QrToken, cancellationToken);
        if (session is null)
            return Result<SessionFeeContext>.Failure(GetSessionFeeContextByQrErrors.SessionNotFound);

        var policy = await pricing.GetEffectiveAtAsync(
            session.LotId,
            session.VehicleType.ToString().ToUpperInvariant(),
            session.EntryTime,
            cancellationToken);

        if (policy is null)
            return Result<SessionFeeContext>.Failure(GetSessionFeeContextByQrErrors.PricingNotFound);

        return Result<SessionFeeContext>.Success(new SessionFeeContext(
            session.Id,
            session.LotId,
            session.PlateNumber,
            session.VehicleType.ToString(),
            session.Status.ToString(),
            session.EntryTime,
            session.CustomerType.ToString(),
            session.UserId,
            session.ReservationId,
            new SessionPricing(policy.Id, policy.BillingMode, policy.BlockMinutes ?? 0, (long)policy.Price, (long)policy.OvernightFee),
            session.LastPaidAt,
            session.ExitArrivedAt,
            (int)session.CurrentExitWindow.TotalMinutes));
    }
}
