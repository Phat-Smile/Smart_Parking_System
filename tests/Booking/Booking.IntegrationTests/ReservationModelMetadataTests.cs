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
    }
}
