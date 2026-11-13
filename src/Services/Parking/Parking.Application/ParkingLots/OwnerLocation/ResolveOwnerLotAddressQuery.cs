using FluentValidation;
using MediatR;

namespace ParkingSystem.Parking.Application.ParkingLots.OwnerLocation;

// A single best-effort suggestion, NOT a guarantee of the entrance location.
// The Owner must still confirm a pin on the map, including when geocoding finds nothing.
public sealed record ResolveOwnerLotAddressQuery(string Address) : IRequest<OwnerLotAddressLookupResult>;

public sealed record OwnerLotAddressLookupResult(
    string Address,
    IReadOnlyList<GeocodedDestination> Candidates,
    bool ManualPinAllowed);

public sealed class ResolveOwnerLotAddressQueryValidator : AbstractValidator<ResolveOwnerLotAddressQuery>
{
    public ResolveOwnerLotAddressQueryValidator()
    {
        RuleFor(query => query.Address)
            .Must(address => !string.IsNullOrWhiteSpace(address)
                && address.Trim().Length is >= 3 and <= 200)
            .WithMessage("Address must contain 3-200 characters.");
    }
}

public sealed class ResolveOwnerLotAddressQueryHandler(IAddressGeocoder geocoder)
    : IRequestHandler<ResolveOwnerLotAddressQuery, OwnerLotAddressLookupResult>
{
    public async Task<OwnerLotAddressLookupResult> Handle(
        ResolveOwnerLotAddressQuery request,
        CancellationToken cancellationToken)
    {
        var address = request.Address.Trim();
        var candidate = await geocoder.GeocodeAsync(address, cancellationToken);

        // An empty result means "not found", not "invalid address". Manual pin placement remains possible.
        return new OwnerLotAddressLookupResult(
            address,
            candidate is null ? [] : [candidate],
            ManualPinAllowed: true);
    }
}
