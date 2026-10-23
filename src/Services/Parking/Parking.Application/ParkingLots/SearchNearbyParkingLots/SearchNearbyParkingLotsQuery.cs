using FluentValidation;
using MediatR;

namespace ParkingSystem.Parking.Application.ParkingLots.SearchNearbyParkingLots;

public sealed record SearchNearbyParkingLotsQuery(
    decimal Latitude,
    decimal Longitude,
    double RadiusKm = 5,
    int Limit = 20) : IRequest<IReadOnlyList<NearbyParkingLotItem>>;

public sealed class SearchNearbyParkingLotsQueryValidator : AbstractValidator<SearchNearbyParkingLotsQuery>
{
    public SearchNearbyParkingLotsQueryValidator()
    {
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m);
        RuleFor(x => x.RadiusKm).GreaterThan(0).LessThanOrEqualTo(50);
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
    }
}

public sealed class SearchNearbyParkingLotsQueryHandler(INearbyParkingLotReader reader)
    : IRequestHandler<SearchNearbyParkingLotsQuery, IReadOnlyList<NearbyParkingLotItem>>
{
    public Task<IReadOnlyList<NearbyParkingLotItem>> Handle(
        SearchNearbyParkingLotsQuery request,
        CancellationToken cancellationToken) =>
        reader.SearchAsync(
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            request.Limit,
            cancellationToken);
}
