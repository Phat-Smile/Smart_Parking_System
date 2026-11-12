using System;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Queries.GetReservationRatioPolicy;

public sealed record GetReservationRatioPolicyQuery(
    Guid LotId,
    Guid CallerId) : IRequest<Result<ReservationRatioPolicyResponse>>;
