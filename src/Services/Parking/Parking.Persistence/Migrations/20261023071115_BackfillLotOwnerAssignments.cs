using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingSystem.Parking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillLotOwnerAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MOD-PK-04. The owner of a lot is whoever holds its Owner assignment, but lots registered before this
            // migration recorded the registrant only in owner_user_id. Give each registrant the Owner row:
            // - no row yet: insert one (assigned_by is the registrant, who registered the lot);
            // - a Staff row for the registrant: promote it to Owner;
            // - an Owner row (the registrant's or anyone else's): leave it exactly as it is.
            // Lots with a NULL owner_user_id (seed data from before owner registration) get nothing.
            migrationBuilder.Sql(
                """
                INSERT INTO parking.parking_lot_assignments AS assignment (lot_id, account_id, role, assigned_by, assigned_at_utc)
                SELECT lot.id, lot.owner_user_id, 'Owner', lot.owner_user_id, now()
                FROM parking.parking_lots AS lot
                WHERE lot.owner_user_id IS NOT NULL
                ON CONFLICT (lot_id, account_id) DO UPDATE
                    SET role = 'Owner',
                        assigned_by = EXCLUDED.assigned_by,
                        assigned_at_utc = EXCLUDED.assigned_at_utc
                    WHERE assignment.role <> 'Owner';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only change. The rows it created cannot be told apart from rows created later by registrations
            // or by admins, so there is nothing safe to remove.
        }
    }
}
