using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Vehicles.Lookup;

public sealed class LookupVehicleByPlateQueryHandler : IRequestHandler<LookupVehicleByPlateQuery, Result<VehicleLookupResult>>
{
    private readonly IVehicleLookupReader _reader;
    private readonly ILicensePlateNormalizer _normalizer;

    public LookupVehicleByPlateQueryHandler(
        IVehicleLookupReader reader,
        ILicensePlateNormalizer normalizer)
    {
        _reader = reader;
        _normalizer = normalizer;
    }

    public async Task<Result<VehicleLookupResult>> Handle(
        LookupVehicleByPlateQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LicensePlate))
        {
            return Result<VehicleLookupResult>.Failure(LookupVehicleErrors.EmptyPlate);
        }

        var normalized = _normalizer.Normalize(request.LicensePlate);
        if (!_normalizer.IsValidFormat(normalized))
        {
            return Result<VehicleLookupResult>.Failure(LookupVehicleErrors.InvalidPlateFormat);
        }

        var lookupResult = await _reader.FindByPlateAsync(normalized, cancellationToken);
        return Result<VehicleLookupResult>.Success(lookupResult);
    }
}
