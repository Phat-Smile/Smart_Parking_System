using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public static class OwnerTypeParser
{
    /// <summary>
    /// Accepts only the names of <see cref="OwnerType"/>, in any casing. <c>Enum.TryParse</c> would also accept "1" or "99",
    /// which are not part of the contract and would reach the domain as a value that has no checklist.
    /// </summary>
    public static bool TryParse(string? value, out OwnerType ownerType)
    {
        ownerType = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var name = Enum.GetNames<OwnerType>()
            .FirstOrDefault(candidate => string.Equals(candidate, value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name is null)
            return false;

        ownerType = Enum.Parse<OwnerType>(name);
        return true;
    }
}
