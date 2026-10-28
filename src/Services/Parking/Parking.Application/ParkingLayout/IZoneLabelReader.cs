namespace ParkingSystem.Parking.Application.ParkingLayout;

// Code and floor name of zones, shown to the customer next to the session QR (US-PARK-008, team decision 2026-10-22).
public interface IZoneLabelReader
{
    Task<IReadOnlyDictionary<Guid, ZoneLabel>> GetLabelsAsync(IReadOnlyCollection<Guid> zoneIds, CancellationToken cancellationToken);
}

public sealed record ZoneLabel(string ZoneCode, string FloorName);
