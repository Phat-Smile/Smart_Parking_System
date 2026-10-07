namespace ParkingSystem.Identity.Domain.Accounts;

/// <summary>
/// Defines the system authorization roles.
/// Public registration defaults to Customer.
/// </summary>
public enum AccountRole
{
    Customer = 1,
    Staff = 2,
    ParkingLotOwner = 3,
    SystemAdmin = 4
}
