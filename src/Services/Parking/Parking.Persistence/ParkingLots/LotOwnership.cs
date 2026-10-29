using Microsoft.EntityFrameworkCore;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLots;

// The one definition of "owner of a lot" (MOD-PK-05): an assignment row with Role = Owner in parking_lot_assignments.
// Pricing, opening hours and the layout all ask through here so they cannot drift apart again.
// parking_lots.owner_user_id only records who registered the lot; it never authorizes anything by itself.
internal static class LotOwnership
{
    public static IQueryable<ParkingLotAssignment> OwnerAssignments(this ParkingDbContext dbContext) =>
        dbContext.ParkingLotAssignments.Where(assignment => assignment.Role == ParkingLotMemberRole.Owner);

    public static Task<bool> IsOwnerAsync(
        this ParkingDbContext dbContext,
        Guid lotId,
        Guid accountId,
        CancellationToken cancellationToken = default) =>
        dbContext.OwnerAssignments().AsNoTracking().AnyAsync(
            assignment => assignment.LotId == lotId && assignment.AccountId == accountId,
            cancellationToken);
}
