using System;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Features.DepositPolicy.Queries.GetDepositPolicy;

public sealed record GetDepositPolicyQuery(
    Guid LotId,
    Guid CallerId) : IRequest<Result<DepositPolicyResponse>>;
