using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ReservationPersistenceIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();

        var services = new ServiceCollection();
        services.AddPersistence(configuration);
        services.AddLogging();
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task Reservation_round_trips_enums_nullable_fields_and_persists_to_postgresql()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var dbContext = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var reservation = Reservation.CreateTemporaryHold(
                id: reservationId,
                userId: userId,
                vehicleId: vehicleId,
                plateNumber: "51F-999.99",
                lotId: lotId,
                vehicleGroup: VehicleGroup.Car,
                reservationType: ReservationType.HoldWhileDriving,
                appointmentTime: Now.AddMinutes(30),
                prepaidAmount: 25000m,
                createdAt: Now,
                durationMinutes: 60,
                tempHoldExpiresAt: Now.AddMinutes(10),
                priceSnapshot: """{"rate": 25000}""");

            reservation.Confirm(Now.AddMinutes(2));
            reservation.Cancel(Now.AddMinutes(45), CancelReason.NoShow, PrepaidOutcome.Forfeited);

            dbContext.Reservations.Add(reservation);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using (var assertScope = provider.CreateAsyncScope())
        {
            var dbContext = assertScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var loaded = await dbContext.Reservations.AsNoTracking()
                .SingleAsync(r => r.Id == reservationId, cancellationToken);

            Assert.Equal(reservationId, loaded.Id);
            Assert.Equal(userId, loaded.UserId);
            Assert.Equal(vehicleId, loaded.VehicleId);
            Assert.Equal(lotId, loaded.LotId);
            Assert.Equal("51F-999.99", loaded.PlateNumber);
            Assert.Equal(VehicleGroup.Car, loaded.VehicleGroup);
            Assert.Equal(ReservationType.HoldWhileDriving, loaded.ReservationType);
            Assert.Equal(ReservationStatus.Cancelled, loaded.Status);
            Assert.Equal(CancelReason.NoShow, loaded.CancelReason);
            Assert.Equal(PrepaidOutcome.Forfeited, loaded.PrepaidOutcome);
            Assert.True(loaded.IsNoShow);
            Assert.Null(loaded.UsedAt);
            Assert.NotNull(loaded.ConfirmedAt);
            Assert.NotNull(loaded.CancelledAt);
            Assert.Equal(2, loaded.RowVersion);
        }
    }

    [Fact]
    public async Task VehicleGroup_and_PrepaidOutcome_enums_round_trip_cleanly_with_postgresql_native_types()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var dbContext = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        // 1. Verify VehicleGroup.Car and VehicleGroup.Motorbike round-trip against booking.vehicle_group
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT $1::booking.vehicle_group";
            cmd.Parameters.Add(new NpgsqlParameter { Value = VehicleGroup.Car });
            var result = (VehicleGroup)(await cmd.ExecuteScalarAsync(cancellationToken))!;
            Assert.Equal(VehicleGroup.Car, result);

            cmd.Parameters[0].Value = VehicleGroup.Motorbike;
            var resultMotorbike = (VehicleGroup)(await cmd.ExecuteScalarAsync(cancellationToken))!;
            Assert.Equal(VehicleGroup.Motorbike, resultMotorbike);
        }

        // 2. Verify PrepaidOutcome (Applied, Refunded, Forfeited) round-trip against booking.prepaid_outcome
        foreach (var outcome in new[] { PrepaidOutcome.Applied, PrepaidOutcome.Refunded, PrepaidOutcome.Forfeited })
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT $1::booking.prepaid_outcome";
            cmd.Parameters.Add(new NpgsqlParameter { Value = outcome });
            var result = (PrepaidOutcome)(await cmd.ExecuteScalarAsync(cancellationToken))!;
            Assert.Equal(outcome, result);
        }
    }

    [Fact]
    public async Task All_five_statuses_persist_and_round_trip_cleanly_via_ef_core()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var dbContext = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        // Create 5 reservations to test all 5 statuses
        var resHold = Reservation.CreateTemporaryHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-100.01", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(30), 10000m, Now);
        var resConfirmed = Reservation.CreateTemporaryHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-100.02", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(30), 10000m, Now);
        resConfirmed.Confirm(Now.AddMinutes(2));

        var resUsed = Reservation.CreateTemporaryHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-100.03", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(30), 10000m, Now);
        resUsed.Confirm(Now.AddMinutes(2));
        resUsed.Use(Now.AddMinutes(15));

        var resCancelled = Reservation.CreateTemporaryHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-100.04", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(30), 10000m, Now);
        resCancelled.Cancel(Now.AddMinutes(5), CancelReason.CustomerCancelled);

        var resExpired = Reservation.CreateTemporaryHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-100.05", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(30), 10000m, Now);
        resExpired.Expire();

        await using (var saveScope = provider.CreateAsyncScope())
        {
            var db = saveScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            db.Reservations.AddRange(resHold, resConfirmed, resUsed, resCancelled, resExpired);
            await db.SaveChangesAsync(cancellationToken);
        }

        await using (var readScope = provider.CreateAsyncScope())
        {
            var db = readScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var loadedHold = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == resHold.Id, cancellationToken);
            var loadedConfirmed = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == resConfirmed.Id, cancellationToken);
            var loadedUsed = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == resUsed.Id, cancellationToken);
            var loadedCancelled = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == resCancelled.Id, cancellationToken);
            var loadedExpired = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == resExpired.Id, cancellationToken);

            Assert.Equal(ReservationStatus.TemporaryHold, loadedHold.Status);
            Assert.Equal(ReservationStatus.Confirmed, loadedConfirmed.Status);
            Assert.Equal(ReservationStatus.Used, loadedUsed.Status);
            Assert.Equal(ReservationStatus.Cancelled, loadedCancelled.Status);
            Assert.Equal(ReservationStatus.Expired, loadedExpired.Status);
        }
    }

    [Fact]
    public async Task Reservation_types_cancel_reasons_and_nullable_enums_persist_correctly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var dbContext = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        // Test ReservationType.Scheduled & ReservationType.HoldWhileDriving
        var resScheduled = Reservation.CreateTemporaryHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-200.01", Guid.NewGuid(), VehicleGroup.Car, ReservationType.Scheduled, Now.AddHours(2), 20000m, Now);
        var resDriving = Reservation.CreateTemporaryHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-200.02", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(30), 20000m, Now);

        // Test CancelReason values and PrepaidOutcome values
        var resCancelPayFail = Reservation.CreateTemporaryHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-200.03", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(30), 20000m, Now);
        resCancelPayFail.Cancel(Now.AddMinutes(5), CancelReason.PaymentFailed, PrepaidOutcome.Refunded);

        var resCancelSlot = Reservation.CreateTemporaryHold(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "51F-200.04", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving, Now.AddMinutes(30), 20000m, Now);
        resCancelSlot.Cancel(Now.AddMinutes(5), CancelReason.NoSuitableSlot, PrepaidOutcome.Refunded);

        await using (var saveScope = provider.CreateAsyncScope())
        {
            var db = saveScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            db.Reservations.AddRange(resScheduled, resDriving, resCancelPayFail, resCancelSlot);
            await db.SaveChangesAsync(cancellationToken);
        }

        await using (var readScope = provider.CreateAsyncScope())
        {
            var db = readScope.ServiceProvider.GetRequiredService<BookingDbContext>();

            var loadedScheduled = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == resScheduled.Id, cancellationToken);
            Assert.Equal(ReservationType.Scheduled, loadedScheduled.ReservationType);
            Assert.Null(loadedScheduled.CancelReason);
            Assert.Null(loadedScheduled.PrepaidOutcome);

            var loadedDriving = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == resDriving.Id, cancellationToken);
            Assert.Equal(ReservationType.HoldWhileDriving, loadedDriving.ReservationType);

            var loadedPayFail = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == resCancelPayFail.Id, cancellationToken);
            Assert.Equal(CancelReason.PaymentFailed, loadedPayFail.CancelReason);
            Assert.Equal(PrepaidOutcome.Refunded, loadedPayFail.PrepaidOutcome);
            Assert.False(loadedPayFail.IsNoShow);

            var loadedSlot = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == resCancelSlot.Id, cancellationToken);
            Assert.Equal(CancelReason.NoSuitableSlot, loadedSlot.CancelReason);
            Assert.Equal(PrepaidOutcome.Refunded, loadedSlot.PrepaidOutcome);
            Assert.False(loadedSlot.IsNoShow);
        }
    }

    [Fact]
    public async Task Postgres_catalog_verifies_all_five_enum_types_and_labels_match_domain_models()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var dbContext = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT t.typname, e.enumlabel
            FROM pg_type t
            JOIN pg_namespace n ON n.oid = t.typnamespace
            JOIN pg_enum e ON e.enumtypid = t.oid
            WHERE n.nspname = 'booking'
            ORDER BY t.typname, e.enumsortorder;
            """;

        var enumMap = new Dictionary<string, List<string>>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var typeName = reader.GetString(0);
            var label = reader.GetString(1);
            if (!enumMap.TryGetValue(typeName, out var list))
            {
                list = new List<string>();
                enumMap[typeName] = list;
            }
            list.Add(label);
        }

        Assert.Equal(new[] { "car", "motorbike" }, enumMap["vehicle_group"]);
        Assert.Equal(new[] { "hold_while_driving", "scheduled" }, enumMap["reservation_type"]);
        Assert.Equal(new[] { "temporary_hold", "confirmed", "used", "cancelled", "expired" }, enumMap["reservation_status"]);
        Assert.Equal(new[] { "customer_cancelled", "payment_failed", "no_show", "no_suitable_slot" }, enumMap["cancel_reason"]);
        Assert.Equal(new[] { "applied", "refunded", "forfeited" }, enumMap["prepaid_outcome"]);
    }

    [Fact]
    public async Task Conflicting_concurrent_updates_on_same_reservation_throws_DbUpdateConcurrencyException()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var dbContext = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        var reservationId = Guid.NewGuid();
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var reservation = Reservation.CreateTemporaryHold(
                id: reservationId,
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "51F-888.88",
                lotId: Guid.NewGuid(),
                vehicleGroup: VehicleGroup.Car,
                reservationType: ReservationType.Scheduled,
                appointmentTime: Now.AddMinutes(60),
                prepaidAmount: 15000m,
                createdAt: Now,
                tempHoldExpiresAt: Now.AddMinutes(10));

            dbContext.Reservations.Add(reservation);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using var scope1 = provider.CreateAsyncScope();
        await using var scope2 = provider.CreateAsyncScope();

        var db1 = scope1.ServiceProvider.GetRequiredService<BookingDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<BookingDbContext>();

        var reservation1 = await db1.Reservations.SingleAsync(r => r.Id == reservationId, cancellationToken);
        var reservation2 = await db2.Reservations.SingleAsync(r => r.Id == reservationId, cancellationToken);

        Assert.Equal(0, reservation1.RowVersion);
        Assert.Equal(0, reservation2.RowVersion);

        // Context 1 confirms the reservation and saves first -> RowVersion becomes 1.
        reservation1.Confirm(Now.AddMinutes(1));
        await db1.SaveChangesAsync(cancellationToken);

        // Context 2 tries to expire the stale reservation loaded at version 0.
        reservation2.Expire();

        var ex = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync(cancellationToken));
        Assert.NotNull(ex);

        // Verify that database record still has Context 1's values and version 1 (not overwritten by Context 2).
        await using var verifyScope = provider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var latest = await verifyDb.Reservations.AsNoTracking().SingleAsync(r => r.Id == reservationId, cancellationToken);

        Assert.Equal(ReservationStatus.Confirmed, latest.Status);
        Assert.Equal(1, latest.RowVersion);
    }

    [Fact]
    public async Task Database_catalog_verifies_indexes_and_nullable_used_at()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var dbContext = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);

        // 1. Verify used_at column nullability from information_schema
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT is_nullable
                FROM information_schema.columns
                WHERE table_schema = 'booking'
                  AND table_name = 'reservations'
                  AND column_name = 'used_at';
                """;
            var isNullable = (string)(await cmd.ExecuteScalarAsync(cancellationToken))!;
            Assert.Equal("YES", isNullable);
        }

        // 2. Verify the 3 indexes exist in pg_indexes
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT indexname, indexdef
                FROM pg_indexes
                WHERE schemaname = 'booking' AND tablename = 'reservations'
                ORDER BY indexname;
                """;
            var indexes = new Dictionary<string, string>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                indexes[reader.GetString(0)] = reader.GetString(1);
            }

            Assert.True(indexes.ContainsKey("ix_reservations_user_id"), "Missing ix_reservations_user_id index");
            Assert.True(indexes.ContainsKey("ix_reservations_lot_id_status"), "Missing ix_reservations_lot_id_status index");
            Assert.True(indexes.ContainsKey("ix_reservations_status_temp_hold_expires_at"), "Missing ix_reservations_status_temp_hold_expires_at index");

            // Verify column order from definition
            Assert.Contains("(user_id)", indexes["ix_reservations_user_id"]);
            Assert.Contains("(lot_id, status)", indexes["ix_reservations_lot_id_status"]);
            Assert.Contains("(status, temp_hold_expires_at)", indexes["ix_reservations_status_temp_hold_expires_at"]);
        }
    }

    [Fact]
    public async Task DateTimeOffset_persists_as_utc_and_preserves_vietnam_time_instant_against_postgresql()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var dbContext = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        var reservationId = Guid.NewGuid();
        var vnOffset = TimeSpan.FromHours(7);
        var inputAppointmentTime = new DateTimeOffset(2026, 10, 20, 10, 0, 0, vnOffset);
        var inputCreatedAt = new DateTimeOffset(2026, 10, 20, 9, 0, 0, vnOffset);

        await using (var scope = provider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var reservation = Reservation.CreateTemporaryHold(
                id: reservationId,
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "51F-777.77",
                lotId: Guid.NewGuid(),
                vehicleGroup: VehicleGroup.Car,
                reservationType: ReservationType.HoldWhileDriving,
                appointmentTime: inputAppointmentTime,
                prepaidAmount: 20000m,
                createdAt: inputCreatedAt);

            dbContext.Reservations.Add(reservation);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using (var assertScope = provider.CreateAsyncScope())
        {
            var dbContext = assertScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var loaded = await dbContext.Reservations.AsNoTracking()
                .SingleAsync(r => r.Id == reservationId, cancellationToken);

            // 1. Verify stored instant in UTC
            var expectedUtcAppointment = new DateTimeOffset(2026, 10, 20, 3, 0, 0, TimeSpan.Zero);
            Assert.Equal(expectedUtcAppointment, loaded.AppointmentTime);
            Assert.Equal(TimeSpan.Zero, loaded.AppointmentTime.Offset);

            // 2. Verify conversion back to Asia/Ho_Chi_Minh yields 10:00:00 +07:00
            var reloadedVnTime = loaded.AppointmentTime.ToOffset(vnOffset);
            Assert.Equal(inputAppointmentTime, reloadedVnTime);
            Assert.Equal(10, reloadedVnTime.Hour);

            // 3. Verify nullable timestamps are null
            Assert.Null(loaded.ConfirmedAt);
            Assert.Null(loaded.UsedAt);
            Assert.Null(loaded.CancelledAt);
            Assert.Null(loaded.TempHoldExpiresAt);
        }
    }

    [Theory]
    [InlineData("51A-123.45")]
    [InlineData("51a 123.45")]
    [InlineData("51A12345")]
    [Trait("Requirement", "N1")]
    public async Task FindActiveConfirmedByLotAndPlateAsync_Matches_Unnormalized_Plates_Against_Canonical_Storage(string inputPlate)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = CreateProvider();
        await using (var migrateScope = provider.CreateAsyncScope())
        {
            var dbContext = migrateScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        var lotId = Guid.NewGuid();
        var apptTime = Now.AddHours(1);
        const string canonicalPlate = "51A12345";

        var reservation = Reservation.CreateTemporaryHold(
            id: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            vehicleId: Guid.NewGuid(),
            plateNumber: canonicalPlate,
            lotId: lotId,
            vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.Scheduled,
            appointmentTime: apptTime,
            prepaidAmount: 20000m,
            createdAt: Now);
        reservation.Confirm(Now);

        await using (var seedScope = provider.CreateAsyncScope())
        {
            var repo = seedScope.ServiceProvider.GetRequiredService<IReservationRepository>();
            repo.Add(reservation);
            await repo.SaveChangesAsync(cancellationToken);
        }

        await using (var queryScope = provider.CreateAsyncScope())
        {
            var repo = queryScope.ServiceProvider.GetRequiredService<IReservationRepository>();
            var found = await repo.FindActiveConfirmedByLotAndPlateAsync(lotId, inputPlate, apptTime, cancellationToken);

            Assert.NotNull(found);
            Assert.Equal(reservation.Id, found.Id);
            Assert.Equal(canonicalPlate, found.PlateNumber);
        }
    }
}
