using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;

namespace ParkingSystem.Parking.Application.GateReadings.ListPendingGateReadings;

// US-PARK-002 (BR-05.a, MF-08/09A/09B): the readings of a lot that staff must confirm, oldest first, so the vehicle that
// has waited longest at the gate is handled first.
public sealed record ListPendingGateReadingsQuery(Guid LotId, Guid ActorUserId) : IRequest<Result<IReadOnlyList<PendingGateReading>>>;

/// <param name="PlateFormatValid">Whether the read plate fits the Vietnamese format; false explains why it was not accepted automatically.</param>
public sealed record PendingGateReading(
    Guid ReadingId,
    string RawPlate,
    string PlateNumber,
    string VehicleType,
    decimal Confidence,
    bool PlateFormatValid,
    DateTimeOffset ReceivedAt);

public sealed class ListPendingGateReadingsQueryHandler(IGateReadingRepository readings, IParkingLotAccess lotAccess)
    : IRequestHandler<ListPendingGateReadingsQuery, Result<IReadOnlyList<PendingGateReading>>>
{
    public async Task<Result<IReadOnlyList<PendingGateReading>>> Handle(ListPendingGateReadingsQuery request, CancellationToken cancellationToken)
    {
        // TECH-12: an unknown lot also returns 403, since nobody is assigned to it.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, request.LotId, cancellationToken))
            return Result<IReadOnlyList<PendingGateReading>>.Failure(ParkingLotAccessErrors.AccessDenied);

        var pending = await readings.ListPendingByLotAsync(request.LotId, cancellationToken);
        return Result<IReadOnlyList<PendingGateReading>>.Success(pending
            .Select(reading => new PendingGateReading(
                reading.Id,
                reading.RawPlate,
                reading.PlateNumber,
                reading.VehicleType.ToString(),
                reading.Confidence,
                VietnamPlateFormat.IsValid(reading.PlateNumber),
                reading.ReceivedAt))
            .ToList());
    }
}
