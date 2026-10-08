using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Identity.Application.Vehicles.Lookup;

public sealed record LookupVehicleByPlateQuery(string LicensePlate) : IRequest<Result<VehicleLookupResult>>;
