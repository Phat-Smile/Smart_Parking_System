using FluentValidation;
using MediatR;

namespace ParkingSystem.Parking.Application.ParkingLots.SearchParkingLotsByAddress;

public sealed record SearchParkingLotsByAddressQuery(
    string Address,
    double RadiusKm = 5,
    int Limit = 20) : IRequest<AddressParkingSearchResult?>;

public sealed record AddressParkingSearchResult(
    GeocodedDestination Destination,
    IReadOnlyList<AddressParkingLotItem> ParkingLots);

public sealed record AddressParkingLotItem(
    Guid Id,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string Type,
    double DistanceKm);

public sealed class SearchParkingLotsByAddressQueryValidator : AbstractValidator<SearchParkingLotsByAddressQuery>
{
    public SearchParkingLotsByAddressQueryValidator()
    {
        RuleFor(request => request.Address)
            .Must(address => !string.IsNullOrWhiteSpace(address) && address.Trim().Length >= 3)
            .WithMessage("Vui lòng nhập địa chỉ hoặc điểm đến (ít nhất 3 ký tự).")
            .Must(address => address is null || address.Length <= 200)
            .WithMessage("Địa chỉ không được dài quá 200 ký tự.");
        RuleFor(request => request.RadiusKm)
            .Must(double.IsFinite)
            .WithMessage("Bán kính phải là số hữu hạn.")
            .GreaterThan(0)
            .LessThanOrEqualTo(50);
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
    }
}

public sealed class SearchParkingLotsByAddressQueryHandler(
    IAddressGeocoder geocoder,
    IParkingLotCatalogReader parkingLots)
    : IRequestHandler<SearchParkingLotsByAddressQuery, AddressParkingSearchResult?>
{
    private const double EarthRadiusKm = 6371.0088;
    private const double RadiansPerDegree = Math.PI / 180;

    public async Task<AddressParkingSearchResult?> Handle(
        SearchParkingLotsByAddressQuery request,
        CancellationToken cancellationToken)
    {
        var destination = await geocoder.GeocodeAsync(request.Address.Trim(), cancellationToken);
        if (destination is null)
            return null;

        // Independent of US-SEARCH-002. The existing catalog reader excludes
        // Pending/Rejected lots. After merging Nearby, consolidate the two
        // distance searches into a single bounded database reader.
        var approvedLots = await parkingLots.ListApprovedAsync(cancellationToken);
        var nearby = approvedLots
            .Select(lot => new
            {
                Lot = lot,
                DistanceKm = HaversineKm(
                    (double)destination.Latitude, (double)destination.Longitude,
                    (double)lot.Latitude, (double)lot.Longitude)
            })
            .Where(candidate => candidate.DistanceKm <= request.RadiusKm)
            .OrderBy(candidate => candidate.DistanceKm)
            .ThenBy(candidate => candidate.Lot.Name, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Lot.Id)
            .Take(request.Limit)
            .Select(candidate => new AddressParkingLotItem(
                candidate.Lot.Id,
                candidate.Lot.Name,
                candidate.Lot.Address,
                candidate.Lot.Latitude,
                candidate.Lot.Longitude,
                candidate.Lot.Type,
                Math.Round(candidate.DistanceKm, 3)))
            .ToList();

        return new AddressParkingSearchResult(destination, nearby);
    }

    private static double HaversineKm(
        double latA, double lonA, double latB, double lonB)
    {
        var deltaLat = (latB - latA) * RadiansPerDegree;
        var deltaLon = (lonB - lonA) * RadiansPerDegree;
        var a = Math.Pow(Math.Sin(deltaLat / 2), 2)
            + Math.Cos(latA * RadiansPerDegree)
            * Math.Cos(latB * RadiansPerDegree)
            * Math.Pow(Math.Sin(deltaLon / 2), 2);
        var clamped = Math.Clamp(a, 0, 1);
        return 2 * EarthRadiusKm * Math.Atan2(Math.Sqrt(clamped), Math.Sqrt(1 - clamped));
    }
}
