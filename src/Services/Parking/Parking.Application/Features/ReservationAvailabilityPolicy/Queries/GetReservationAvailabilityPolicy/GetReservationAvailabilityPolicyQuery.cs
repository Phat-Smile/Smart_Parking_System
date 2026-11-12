using System;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy.Queries.GetReservationAvailabilityPolicy;

public sealed record GetReservationAvailabilityPolicyQuery(
    Guid LotId,
    Guid CallerId) : IRequest<Result<ReservationAvailabilityPolicyResponse>>;
