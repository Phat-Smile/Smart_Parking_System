using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.ParkingSessions.Queries.GetPublicParkingSessionFee;

/// <summary>
/// Q-V2c: Anonymous public query to preview fee for an active parking session by QR token.
/// </summary>
public sealed record GetPublicParkingSessionFeeQuery(string QrToken)
    : IRequest<Result<PublicParkingSessionFeeResponse>>;
