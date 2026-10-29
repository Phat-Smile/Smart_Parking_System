using System;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.Features.ReservationAvailabilityPolicy.Commands.ConfigureReservationAvailabilityPolicy;

public sealed record ConfigureReservationAvailabilityPolicyCommand(
    Guid LotId,
    bool? ReservationEnabled,
    Guid CallerId) : IRequest<Result<ReservationAvailabilityPolicyResponse>>;
