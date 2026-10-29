using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Parking.Persistence.Seeding;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ParkingDataSeederIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Seeder_RunTwice_IsIdempotent_And_YieldsExactExpectedCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(ct);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();

            // First run
            await ParkingDataSeeder.SeedAsync(db, reset: false, cancellationToken: ct);

            // Second run (idempotency verification)
            await ParkingDataSeeder.SeedAsync(db, reset: false, cancellationToken: ct);

            // Verify row counts for seeded lots
            var lotCount = await db.ParkingLots.CountAsync(l => ParkingSeedConstants.SeedLotIds.Contains(l.Id), ct);
            var depositCount = await db.ReservationDepositPolicies.CountAsync(p => ParkingSeedConstants.SeedLotIds.Contains(p.LotId), ct);
            var ratioCount = await db.ReservationRatioPolicies.CountAsync(p => ParkingSeedConstants.SeedLotIds.Contains(p.LotId), ct);
            var availabilityCount = await db.ReservationAvailabilityPolicies.CountAsync(p => ParkingSeedConstants.SeedLotIds.Contains(p.LotId), ct);
            var assignmentCount = await db.ParkingLotAssignments.CountAsync(a => ParkingSeedConstants.SeedLotIds.Contains(a.LotId), ct);
            var floorCount = await db.Floors.CountAsync(f => ParkingSeedConstants.SeedLotIds.Contains(f.LotId), ct);
            var zoneCount = await db.Zones.CountAsync(z => ParkingSeedConstants.SeedZoneIds.Contains(z.Id), ct);
            var slotCount = await db.ParkingSlots.CountAsync(s => ParkingSeedConstants.SeedLotIds.Contains(s.LotId), ct);
            var pricingCount = await db.PricingPolicies.CountAsync(p => ParkingSeedConstants.SeedPricingPolicyIds.Contains(p.Id), ct);
            var sessionCount = await db.ParkingSessions.CountAsync(s => ParkingSeedConstants.SeedLotIds.Contains(s.LotId), ct);
            var auditCount = await db.AuditLogEntries.CountAsync(a => a.LotId.HasValue && ParkingSeedConstants.SeedLotIds.Contains(a.LotId.Value), ct);
            var outboxCount = await db.OutboxMessages.CountAsync(ct);

            Assert.Equal(5, lotCount);
            Assert.Equal(2, depositCount);
            Assert.Equal(2, ratioCount);
            Assert.Equal(2, availabilityCount);
            Assert.Equal(8, assignmentCount); // 5 owners + 3 staff
            Assert.Equal(4, floorCount);
            Assert.Equal(5, zoneCount);
            Assert.Equal(32, slotCount);
            Assert.Equal(5, pricingCount);
            Assert.Equal(3, sessionCount);
            Assert.Equal(2, auditCount);
            Assert.Equal(0, outboxCount); // Outbox must never be seeded
        }
    }

    [Fact]
    public async Task Seeder_Reset_RestoresDataCorrectly_WithoutDeletingUnrelatedRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(ct);

        var unrelatedLotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await ParkingDataSeeder.SeedAsync(db, reset: false, cancellationToken: ct);

            // Add an unrelated lot (not part of seed IDs)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
                VALUES ({unrelatedLotId}, 'Unrelated Custom Lot', '999 Custom Street', 10.80, 106.75, 'Outdoor', 'Approved')
                """,
                ct);

            try
            {
                // Run Reset
                await ParkingDataSeeder.SeedAsync(db, reset: true, cancellationToken: ct);

                // Seeded lots exist
                Assert.True(await db.ParkingLots.AnyAsync(l => l.Id == unrelatedLotId, ct));
                Assert.Equal(5, await db.ParkingLots.CountAsync(l => ParkingSeedConstants.SeedLotIds.Contains(l.Id), ct));

                // Seeded slots are still 32
                Assert.Equal(32, await db.ParkingSlots.CountAsync(s => ParkingSeedConstants.SeedLotIds.Contains(s.LotId), ct));
            }
            finally
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM parking.parking_lots WHERE id = {unrelatedLotId}",
                    ct);
            }
        }
    }

    [Fact]
    public async Task Seeder_CapacityAndReservedSlots_CalculationMatchesExpectedValues()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(ct);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await ParkingDataSeeder.SeedAsync(db, reset: false, cancellationToken: ct);

        // L1 Eligible car capacity = total car slots in L1 minus Maintenance & Unavailable (15 - 2 = 13)
        var l1CarSlots = await db.ParkingSlots
            .Where(s => s.LotId == ParkingSeedConstants.LotL1)
            .ToListAsync(ct);
        var l1UsableCapacity = l1CarSlots.Count(s =>
            s.EffectiveStatus != SlotEffectiveStatus.Maintenance &&
            s.EffectiveStatus != SlotEffectiveStatus.Unavailable);
        Assert.Equal(13, l1UsableCapacity);

        // L1 ratio policy = 50% -> 13 * 50 / 100 = 6 slots
        var l1Ratio = await db.ReservationRatioPolicies.FirstAsync(p => p.LotId == ParkingSeedConstants.LotL1, ct);
        var l1ReservedSlots = l1Ratio.CalculateReservedSlots(l1UsableCapacity);
        Assert.Equal(6, l1ReservedSlots);

        // L2 Eligible car capacity = 10, ratio = 0% -> 0 slots
        var l2CarSlots = await db.ParkingSlots
            .Where(s => s.LotId == ParkingSeedConstants.LotL2)
            .ToListAsync(ct);
        var l2UsableCapacity = l2CarSlots.Count(s =>
            s.EffectiveStatus != SlotEffectiveStatus.Maintenance &&
            s.EffectiveStatus != SlotEffectiveStatus.Unavailable);
        Assert.Equal(10, l2UsableCapacity);

        var l2Ratio = await db.ReservationRatioPolicies.FirstAsync(p => p.LotId == ParkingSeedConstants.LotL2, ct);
        var l2ReservedSlots = l2Ratio.CalculateReservedSlots(l2UsableCapacity);
        Assert.Equal(0, l2ReservedSlots);

        // L3 Eligible car capacity = 7, unconfigured defaults to 50% -> 7 * 50 / 100 = 3 slots
        var l3CarSlots = await db.ParkingSlots
            .Where(s => s.LotId == ParkingSeedConstants.LotL3)
            .ToListAsync(ct);
        var l3UsableCapacity = l3CarSlots.Count(s =>
            s.EffectiveStatus != SlotEffectiveStatus.Maintenance &&
            s.EffectiveStatus != SlotEffectiveStatus.Unavailable);
        Assert.Equal(7, l3UsableCapacity);

        var l3ReservedSlots = ReservationRatioPolicy.CalculateReservedSlots(
            l3UsableCapacity,
            ReservationRatioPolicy.DefaultReservationRatioPercent);
        Assert.Equal(3, l3ReservedSlots);
    }

    [Fact]
    public async Task Seeder_AvailabilityFlags_MatchExpectedDefaultsAndOverrides()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(ct);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await ParkingDataSeeder.SeedAsync(db, reset: false, cancellationToken: ct);

        // L1 availability policy = true
        var l1Avail = await db.ReservationAvailabilityPolicies.FirstOrDefaultAsync(p => p.LotId == ParkingSeedConstants.LotL1, ct);
        Assert.NotNull(l1Avail);
        Assert.True(l1Avail.ReservationEnabled);

        // L2 availability policy = false
        var l2Avail = await db.ReservationAvailabilityPolicies.FirstOrDefaultAsync(p => p.LotId == ParkingSeedConstants.LotL2, ct);
        Assert.NotNull(l2Avail);
        Assert.False(l2Avail.ReservationEnabled);

        // L3 unconfigured -> no row in table
        var l3Avail = await db.ReservationAvailabilityPolicies.FirstOrDefaultAsync(p => p.LotId == ParkingSeedConstants.LotL3, ct);
        Assert.Null(l3Avail);
    }

    private async Task<ServiceProvider> NewProviderAsync(System.Threading.CancellationToken ct)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(ct);
        return provider;
    }
}
