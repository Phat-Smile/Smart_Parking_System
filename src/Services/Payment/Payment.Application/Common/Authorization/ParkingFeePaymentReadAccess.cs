using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Entities;

namespace ParkingSystem.Payment.Application.Common.Authorization;

// Who may read a parking-fee payment (status or receipt). A role claim alone is never enough for staff or owners:
// Parking confirms that the caller works at the payment's lot (NFR-07). Any doubt, including Parking being down, denies.
public static class ParkingFeePaymentReadAccess
{
    private const string AdminRole = "SystemAdmin";
    private const string StaffRole = "Staff";
    private static readonly string[] OwnerRoles = ["ParkingLotOwner", "LotOwner"];

    public static async Task<bool> CanReadAsync(
        ParkingFeePayment payment,
        Guid? callerAccountId,
        string? callerRole,
        IParkingLotMembershipGateway membershipGateway,
        CancellationToken cancellationToken)
    {
        if (string.Equals(callerRole, AdminRole, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Walk-in / anonymous session payment (payment has no linked account):
        // Allow anyone with the valid session or payment identifier to view the receipt.
        if (payment.AccountId is null)
        {
            return true;
        }

        if (callerAccountId is not { } accountId || accountId == Guid.Empty)
        {
            return false;
        }

        if (payment.AccountId == accountId)
        {
            return true;
        }

        var isStaff = string.Equals(callerRole, StaffRole, StringComparison.OrdinalIgnoreCase);
        var isOwner = OwnerRoles.Any(role => string.Equals(callerRole, role, StringComparison.OrdinalIgnoreCase));
        if (!isStaff && !isOwner)
        {
            return false;
        }

        var membership = await membershipGateway.GetMembershipAsync(payment.ParkingLotId, accountId, cancellationToken);
        if (!membership.IsSuccess || membership.Value is not { IsMember: true } info)
        {
            return false;
        }

        var memberIsOwner = string.Equals(info.Role, "Owner", StringComparison.OrdinalIgnoreCase);
        var memberIsStaff = string.Equals(info.Role, "Staff", StringComparison.OrdinalIgnoreCase);
        return isOwner ? memberIsOwner : memberIsStaff || memberIsOwner;
    }
}
