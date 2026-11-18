using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations.Commands.CancelForNoSuitableSlot;

public sealed record CancelForNoSuitableSlotCommand(
    Guid LotId,
    string PlateNumber,
    DateTimeOffset ArrivedAt) : IRequest<Result<CancelForNoSuitableSlotResult>>;
