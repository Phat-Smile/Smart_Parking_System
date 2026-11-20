namespace ParkingSystem.Parking.Application.Common.Identity;

public interface IStaffAccountLookup
{
    Task<(bool IsFound, Guid AccountId)> LookupStaffByEmailAsync(string email, CancellationToken cancellationToken);
}
