namespace ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;

public sealed record CustomerLotFeatures(bool SupportsCar, bool SupportsMotorbike,
    bool SupportsElectricVehicles, bool SupportsLargeVehicles,
    bool HasEvCharging, bool IsCovered, bool HasCctv,
    bool AlwaysOpen, bool Prebookable)
{
    public IReadOnlyList<string> Compatibility =>
        new[] { SupportsCar ? "car" : null, SupportsMotorbike ? "motorbike" : null,
            SupportsElectricVehicles ? "ev" : null, SupportsLargeVehicles ? "large" : null }
            .OfType<string>().ToArray();

    public IReadOnlyList<string> Amenities =>
        new[] { HasEvCharging ? "ev-charging" : null, IsCovered ? "covered" : null,
            AlwaysOpen ? "always-open" : null, HasCctv ? "cctv" : null }
            .OfType<string>().ToArray();
}

public interface ICustomerLotFeaturesReader
{
    Task<IReadOnlyDictionary<Guid, CustomerLotFeatures>> ReadAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken ct);
}
