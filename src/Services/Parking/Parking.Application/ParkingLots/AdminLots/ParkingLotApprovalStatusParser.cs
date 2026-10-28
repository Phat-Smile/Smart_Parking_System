using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.AdminLots;

public static class ParkingLotApprovalStatusParser
{
    // Only a status name selects a status. Enum.TryParse also takes numbers, undefined values and comma separated lists
    // ("Pending,Rejected" parses as 0 | 2 = Rejected), which filtered the admin list wrongly without any error.
    public static bool TryParse(string? value, out ParkingLotApprovalStatus status)
    {
        status = default;
        var wanted = value?.Trim();
        if (string.IsNullOrEmpty(wanted))
            return false;

        foreach (var name in Enum.GetNames<ParkingLotApprovalStatus>())
        {
            if (!string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase))
                continue;

            status = Enum.Parse<ParkingLotApprovalStatus>(name);
            return true;
        }

        return false;
    }
}
