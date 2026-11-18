namespace ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;

public sealed record CustomerLotPrice(string VehicleType, decimal Price, string BillingMode,
    int? BlockMinutes = null);

public sealed record CustomerLotMetrics(int AvailableCars, int AvailableMotorbikes,
    IReadOnlyList<CustomerLotPrice> Prices);

public interface ICustomerLotMetricsReader
{
    Task<IReadOnlyDictionary<Guid, CustomerLotMetrics>> ReadAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken ct);
}
