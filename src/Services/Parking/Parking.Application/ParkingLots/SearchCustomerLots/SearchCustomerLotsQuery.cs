using FluentValidation;
using MediatR;
using ParkingSystem.Parking.Application.ParkingLots.ListParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.SearchNearbyParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;

public sealed record SearchCustomerLotsQuery(decimal? Latitude, decimal? Longitude, double RadiusKm,
    VehicleType? VehicleType, decimal? MaxPrice, bool AvailableOnly, string Sort, int Limit = 50,
    bool PrebookableOnly = false, IReadOnlyList<string>? Compatibility = null, IReadOnlyList<string>? Amenities = null)
    : IRequest<IReadOnlyList<CustomerLotSearchItem>>;

public sealed record CustomerLotSearchItem(Guid Id, string Name, string Address, decimal Latitude,
    decimal Longitude, string Type, double? DistanceKm, decimal? StartingPrice,
    string? BillingMode, int AvailableSpaces, bool Prebookable,
    IReadOnlyList<string> Compatibility, IReadOnlyList<string> Amenities,
    double? DrivingTimeMinutes = null,
    int WalkInPlaces = 0);

public sealed class SearchCustomerLotsQueryValidator : AbstractValidator<SearchCustomerLotsQuery>
{
    public SearchCustomerLotsQueryValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).When(x => x.Longitude.HasValue);
        RuleFor(x => x).Must(x => x.Latitude.HasValue == x.Longitude.HasValue)
            .WithMessage("latitude and longitude must be supplied together.");
        RuleFor(x => x.RadiusKm).GreaterThan(0).LessThanOrEqualTo(50);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0).When(x => x.MaxPrice.HasValue);
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
        RuleFor(x => x.Sort).Must(x => x is "recommended" or "name" or "price" or "distance");
        RuleForEach(x => x.Compatibility).Must(x => x is "car" or "motorbike" or "ev" or "large");
        RuleForEach(x => x.Amenities).Must(x => x is "ev-charging" or "covered" or "always-open" or "cctv");
        RuleFor(x => x).Must(x => x.Sort != "distance" || x.Latitude.HasValue)
            .WithMessage("distance sort requires coordinates.");
    }
}

public sealed class SearchCustomerLotsQueryHandler(ISender sender, ICustomerLotFeaturesReader featureReader,
    ICustomerLotMetricsReader metricsReader)
    : IRequestHandler<SearchCustomerLotsQuery, IReadOnlyList<CustomerLotSearchItem>>
{
    public async Task<IReadOnlyList<CustomerLotSearchItem>> Handle(SearchCustomerLotsQuery request, CancellationToken ct)
    {
        IReadOnlyList<(ParkingLotCatalogItem Lot, double? Distance)> candidates;
        if (request.Latitude is { } latitude && request.Longitude is { } longitude)
        {
            var nearby = await sender.Send(new SearchNearbyParkingLotsQuery(latitude, longitude, request.RadiusKm, 100, request.VehicleType), ct);
            candidates = nearby.Select(x => (new ParkingLotCatalogItem(x.Id, x.Name, x.Address,
                x.Latitude, x.Longitude, x.Type), (double?)x.DistanceKm)).ToList();
        }
        else
        {
            var catalog = await sender.Send(new ListParkingLotsQuery(request.VehicleType), ct);
            candidates = catalog.Select(x => (x, (double?)null)).ToList();
        }

        var features = await featureReader.ReadAsync(candidates.Select(x => x.Lot.Id).ToArray(), ct);
        var metrics = await metricsReader.ReadAsync(candidates.Select(x => x.Lot.Id).ToArray(), ct);

        var results = new List<CustomerLotSearchItem>();
        foreach (var (lot, distance) in candidates)
        {
            if (!features.TryGetValue(lot.Id, out var feature)) continue;
            if (request.PrebookableOnly && !feature.Prebookable) continue;
            if (request.Compatibility is { Count: > 0 } && !request.Compatibility.Any(feature.Compatibility.Contains)) continue;
            if (request.Amenities is { Count: > 0 } && !request.Amenities.All(feature.Amenities.Contains)) continue;
            if (!metrics.TryGetValue(lot.Id, out var metric)) continue;
            var spaces = request.VehicleType == VehicleType.Car
                ? metric.AvailableCars
                : request.VehicleType == VehicleType.Motorbike
                    ? metric.AvailableMotorbikes
                    : metric.AvailableCars + metric.AvailableMotorbikes;
            if (request.AvailableOnly && spaces <= 0) continue;

            var prices = metric.Prices.Where(x =>
                    (request.VehicleType is null ||
                        x.VehicleType.Equals(request.VehicleType.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                    && (!request.AvailableOnly ||
                        (x.VehicleType.Equals("Car", StringComparison.OrdinalIgnoreCase) && metric.AvailableCars > 0) ||
                        (x.VehicleType.Equals("Motorbike", StringComparison.OrdinalIgnoreCase) && metric.AvailableMotorbikes > 0)))
                    .OrderBy(x => x.Price).ToList();
            var cheapest = prices.FirstOrDefault();
            if (request.MaxPrice.HasValue && (cheapest is null || cheapest.Price > request.MaxPrice.Value)) continue;

            results.Add(new CustomerLotSearchItem(lot.Id, lot.Name, lot.Address, lot.Latitude,
                lot.Longitude, lot.Type, distance, cheapest?.Price, cheapest?.BillingMode, spaces,
                feature.Prebookable, feature.Compatibility, feature.Amenities,
                DrivingTimeMinutes: null,
                WalkInPlaces: 0));
        }

        var sorted = request.Sort switch
        {
            "name" => results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase),
            "price" => results.OrderBy(x => x.StartingPrice ?? decimal.MaxValue).ThenBy(x => x.Name),
            "distance" => results.OrderBy(x => x.DistanceKm).ThenBy(x => x.Name),
            _ => request.Latitude.HasValue ? results.OrderBy(x => x.DistanceKm).ThenBy(x => x.Name)
                : results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
        };
        return sorted.Take(request.Limit).ToList();
    }
}
