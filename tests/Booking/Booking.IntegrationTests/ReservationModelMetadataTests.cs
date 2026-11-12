using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Persistence;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ReservationModelMetadataTests
{
    [Fact]
    public void EF_model_metadata_verifies_nullable_UsedAt_and_expected_indexes()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql("Host=localhost;Database=dummy")
            .Options;

        using var dbContext = new BookingDbContext(options);
        var entityType = dbContext.Model.FindEntityType(typeof(Reservation));
        Assert.NotNull(entityType);

        // 1. Verify UsedAt is nullable in EF Core metadata
        var usedAtProperty = entityType.FindProperty(nameof(Reservation.UsedAt));
        Assert.NotNull(usedAtProperty);
        Assert.True(usedAtProperty.IsNullable);

        // 2. Verify RowVersion is configured as concurrency token
        var rowVersionProperty = entityType.FindProperty(nameof(Reservation.RowVersion));
        Assert.NotNull(rowVersionProperty);
        Assert.True(rowVersionProperty.IsConcurrencyToken);

        // 3. Verify ix_reservations_user_id index
        var userIdIndex = entityType.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "ix_reservations_user_id");
        Assert.NotNull(userIdIndex);
        var userIdCols = userIdIndex.Properties.Select(p => p.GetColumnName()).ToArray();
        Assert.Equal(["user_id"], userIdCols);

        // 4. Verify ix_reservations_lot_id_status index (exact column order)
        var lotStatusIndex = entityType.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "ix_reservations_lot_id_status");
        Assert.NotNull(lotStatusIndex);
        var lotStatusCols = lotStatusIndex.Properties.Select(p => p.GetColumnName()).ToArray();
        Assert.Equal(["lot_id", "status"], lotStatusCols);

        // 5. Verify ix_reservations_status_temp_hold_expires_at index (exact column order)
        var holdExpIndex = entityType.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "ix_reservations_status_temp_hold_expires_at");
        Assert.NotNull(holdExpIndex);
        var holdExpCols = holdExpIndex.Properties.Select(p => p.GetColumnName()).ToArray();
        Assert.Equal(["status", "temp_hold_expires_at"], holdExpCols);

        // 6. Verify ix_reservations_capacity_check index
        var capacityCheckIndex = entityType.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "ix_reservations_capacity_check");
        Assert.NotNull(capacityCheckIndex);
        var capacityCols = capacityCheckIndex.Properties.Select(p => p.GetColumnName()).ToArray();
        Assert.Equal(["lot_id", "vehicle_group", "status", "window_start_at", "window_end_at"], capacityCols);

        // 7. Verify ix_reservations_active_hold index
        var activeHoldIndex = entityType.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == "ix_reservations_active_hold");
        Assert.NotNull(activeHoldIndex);
        var activeHoldCols = activeHoldIndex.Properties.Select(p => p.GetColumnName()).ToArray();
        Assert.Equal(["lot_id", "vehicle_group", "status", "window_start_at", "hold_end_at"], activeHoldCols);

        // 8. Verify ReservationCapacity entity and composite primary key
        var capacityEntityType = dbContext.Model.FindEntityType(typeof(ReservationCapacity));
        Assert.NotNull(capacityEntityType);
        var pk = capacityEntityType.FindPrimaryKey();
        Assert.NotNull(pk);
        var pkCols = pk.Properties.Select(p => p.GetColumnName()).ToArray();
        Assert.Equal(["lot_id", "vehicle_group"], pkCols);
    }
}
