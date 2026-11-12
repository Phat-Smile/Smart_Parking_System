using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.Incidents;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.GateReadings;
using ParkingSystem.Parking.Domain.Incidents;

namespace ParkingSystem.Parking.Application.GateReadings.ListPendingGateReadings;

// US-PARK-002 (BR-05.a, MF-08/09A/09B): the open LowConfidencePlate incidents of a lot, oldest first, so the vehicle that
// has waited longest at the gate is handled first.
public sealed record ListPendingGateReadingsQuery(Guid LotId, Guid ActorUserId) : IRequest<Result<IReadOnlyList<PendingGateReading>>>;

/// <param name="ObservedPlate">The plate as read (trimmed, upper-case); null when no plate could be read.</param>
/// <param name="Confidence">Stored with 2 decimals, rounded down (ERD plate_confidence numeric(3,2)).</param>
/// <param name="PlateFormatValid">Whether the read plate fits the Vietnamese format; false explains why it was not accepted automatically.</param>
public sealed record PendingGateReading(
    Guid IncidentId,
    string? ObservedPlate,
    string VehicleType,
    decimal Confidence,
    bool PlateFormatValid,
    DateTimeOffset OccurredAt);

public sealed class ListPendingGateReadingsQueryHandler(IIncidentRepository incidents, IParkingLotAccess lotAccess)
    : IRequestHandler<ListPendingGateReadingsQuery, Result<IReadOnlyList<PendingGateReading>>>
{
    public async Task<Result<IReadOnlyList<PendingGateReading>>> Handle(ListPendingGateReadingsQuery request, CancellationToken cancellationToken)
    {
        // TECH-12: an unknown lot also returns 403, since nobody is assigned to it.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, request.LotId, cancellationToken))
            return Result<IReadOnlyList<PendingGateReading>>.Failure(ParkingLotAccessErrors.AccessDenied);

        var pending = await incidents.ListOpenByLotAsync(request.LotId, IncidentType.LowConfidencePlate, cancellationToken);
        return Result<IReadOnlyList<PendingGateReading>>.Success(pending
            .Select(incident => new PendingGateReading(
                incident.Id,
                incident.ObservedPlate,
                incident.ObservedVehicleType!.Value.ToString(),
                incident.PlateConfidence!.Value,
                VietnamPlateFormat.IsValid(incident.ObservedPlate),
                incident.OccurredAt))
            .ToList());
    }
}
