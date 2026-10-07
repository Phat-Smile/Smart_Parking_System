namespace ParkingSystem.Parking.Application.Common.Identity;

// Contract 1 in the master sheet (plate -> account and vehicle, owner BE-1).
public interface IPlateOwnerLookup
{
    /// <summary>Returns null when the plate belongs to no registered vehicle (BR-05.e).</summary>
    Task<PlateOwner?> FindByPlateAsync(string normalizedPlate, CancellationToken cancellationToken);
}

public sealed record PlateOwner(Guid UserId, Guid VehicleId);
