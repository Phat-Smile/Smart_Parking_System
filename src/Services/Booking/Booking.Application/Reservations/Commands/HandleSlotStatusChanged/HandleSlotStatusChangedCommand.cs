using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Booking.Application.Reservations.Commands.HandleSlotStatusChanged;

public sealed record HandleSlotStatusChangedCommand(
    Guid LotId,
    Guid ZoneId,
    Guid SlotId,
    string SlotCode,
    string EffectiveStatus,
    string? PreviousStatus,
    string Source,
    DateTimeOffset OccurredAtUtc,
    long Version) : IRequest<Result<SlotStatusChangedOutcomeDto>>;

public sealed record SlotStatusChangedOutcomeDto(
    Guid LotId,
    Guid SlotId,
    string SlotCode,
    string EffectiveStatus,
    int CancelledReservationsCount,
    string Message);
