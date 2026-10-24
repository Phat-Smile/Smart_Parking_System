using System;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Features.DepositPolicy.Commands.ConfigureDepositPolicy;

public sealed record ConfigureDepositPolicyCommand(
    Guid LotId,
    decimal HoldDepositVnd,
    Guid CallerId) : IRequest<Result<DepositPolicyResponse>>;
