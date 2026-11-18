using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Common.Interfaces;

namespace ParkingSystem.Parking.Application.ParkingSessions.GetSessionFeeContext;

// Contract 6: Payment computes the fee from this. Called between services only, so there is no caller check here:
// Payment decides who may pay. Price is the one in force at entry (BR-09.a), found from the price history by entry time.
public sealed record GetSessionFeeContextQuery(Guid SessionId) : IRequest<Result<SessionFeeContext>>;

public sealed record SessionFeeContext(
    Guid SessionId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string Status,
    DateTimeOffset EntryTimeUtc,
    string CustomerType,
    Guid? AccountId,
    Guid? BookingId,
    SessionPricing Pricing,
    DateTimeOffset? LastPaidAtUtc,
    DateTimeOffset? ArrivedAtExitUtc,
    int GracePeriodMinutes);

/// <param name="BlockMinutes">0 when the lot charges per entry.</param>
public sealed record SessionPricing(Guid PolicyId, string CalculationMethod, int BlockMinutes, long PricePerUnit, long OvernightFee);

public static class GetSessionFeeContextErrors
{
    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "The parking session does not exist.");

    public static readonly Error PricingNotFound =
        new("parking.session.pricing_not_found", "The lot had no price list in force when the vehicle entered.");
}

public sealed class GetSessionFeeContextQueryHandler(IParkingSessionRepository sessions, IPricingPolicyRepository pricing)
    : IRequestHandler<GetSessionFeeContextQuery, Result<SessionFeeContext>>
{
    public async Task<Result<SessionFeeContext>> Handle(GetSessionFeeContextQuery request, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<SessionFeeContext>.Failure(GetSessionFeeContextErrors.SessionNotFound);

        var policy = await pricing.GetEffectiveAtAsync(session.LotId, session.VehicleType.ToString().ToUpperInvariant(), session.EntryTime, cancellationToken);
        if (policy is null)
            return Result<SessionFeeContext>.Failure(GetSessionFeeContextErrors.PricingNotFound);

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
            // US-PAY-005: Payment charges the overstay from T + window to the exit arrival (BR-09.d).
            session.LastPaidAt,
            session.ExitArrivedAt,
            (int)session.CurrentExitWindow.TotalMinutes));
    }
}
