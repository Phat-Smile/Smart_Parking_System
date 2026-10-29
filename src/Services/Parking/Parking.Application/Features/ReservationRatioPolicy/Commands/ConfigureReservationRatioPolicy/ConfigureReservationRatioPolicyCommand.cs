using System;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Commands.ConfigureReservationRatioPolicy;

public sealed record ConfigureReservationRatioPolicyCommand(
    Guid LotId,
    int? ReservationRatioPercent,
    Guid CallerId) : IRequest<Result<ReservationRatioPolicyResponse>>;
