using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence.Auditing;

namespace ParkingSystem.Parking.Persistence.Seeding;

public static class ParkingDataSeeder
{
    public static async Task SeedAsync(
        ParkingDbContext dbContext,
        bool reset = false,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        if (reset)
        {
            logger?.LogInformation("Seeding:Reset is true. Removing existing seed data...");
            await ResetSeedDataAsync(dbContext, cancellationToken);
        }

        // 1. Parking Lots (5)
        var existingLots = await dbContext.ParkingLots
            .Where(l => ParkingSeedConstants.SeedLotIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        if (!existingLots.ContainsKey(ParkingSeedConstants.LotL1))
        {
            var l1 = ParkingLot.Register(
                ParkingSeedConstants.LotL1,
                ParkingSeedConstants.OwnerA,
                "[DEMO] Central MultiStorey Lot",
                "123 Le Loi, Ben Nghe, District 1, Ho Chi Minh City",
                10.7712m,
                106.7001m,
                ParkingLotType.MultiStorey);
            l1.Approve();
            dbContext.ParkingLots.Add(l1);
        }

        if (!existingLots.ContainsKey(ParkingSeedConstants.LotL2))
        {
            var l2 = ParkingLot.Register(
                ParkingSeedConstants.LotL2,
                ParkingSeedConstants.OwnerA,
                "[DEMO] West Basement Parking",
                "456 Nguyen Hue, Ben Nghe, District 1, Ho Chi Minh City",
                10.7750m,
                106.7020m,
                ParkingLotType.Underground);
            l2.Approve();
            dbContext.ParkingLots.Add(l2);
        }

        if (!existingLots.ContainsKey(ParkingSeedConstants.LotL3))
        {
            var l3 = ParkingLot.Register(
                ParkingSeedConstants.LotL3,
                ParkingSeedConstants.OwnerB,
                "[DEMO] East Outdoor Parking",
                "789 Ham Nghi, Nguyen Thai Binh, District 1, Ho Chi Minh City",
                10.7690m,
                106.7050m,
                ParkingLotType.Outdoor);
            l3.Approve();
            dbContext.ParkingLots.Add(l3);
        }

        if (!existingLots.ContainsKey(ParkingSeedConstants.LotL4))
        {
            var l4 = ParkingLot.Register(
                ParkingSeedConstants.LotL4,
                ParkingSeedConstants.OwnerA,
                "[DEMO] North Pending Parking",
                "101 Pasteur, Ben Nghe, District 1, Ho Chi Minh City",
                10.7780m,
                106.6990m,
                ParkingLotType.Outdoor);
            // Remains Pending
            dbContext.ParkingLots.Add(l4);
        }

        if (!existingLots.ContainsKey(ParkingSeedConstants.LotL5))
        {
            var l5 = ParkingLot.Register(
                ParkingSeedConstants.LotL5,
                ParkingSeedConstants.OwnerB,
                "[DEMO] South Rejected Parking",
                "202 Nam Ky Khoi Nghia, Vo Thi Sau, District 3, Ho Chi Minh City",
                10.7820m,
                106.6920m,
                ParkingLotType.Outdoor);
            l5.Reject("Incomplete legal documents and fire safety certificate.");
            dbContext.ParkingLots.Add(l5);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // 2. Policies (Deposit, Ratio, Availability)
        // Deposit policies (L1: 20,000, L2: 0)
        if (!await dbContext.ReservationDepositPolicies.AnyAsync(p => p.LotId == ParkingSeedConstants.LotL1, cancellationToken))
        {
            dbContext.ReservationDepositPolicies.Add(ReservationDepositPolicy.Create(ParkingSeedConstants.LotL1, 20_000m));
        }
        if (!await dbContext.ReservationDepositPolicies.AnyAsync(p => p.LotId == ParkingSeedConstants.LotL2, cancellationToken))
        {
            dbContext.ReservationDepositPolicies.Add(ReservationDepositPolicy.Create(ParkingSeedConstants.LotL2, 0m));
        }

        // Ratio policies (L1: 50, L2: 0)
        if (!await dbContext.ReservationRatioPolicies.AnyAsync(p => p.LotId == ParkingSeedConstants.LotL1, cancellationToken))
        {
            dbContext.ReservationRatioPolicies.Add(ReservationRatioPolicy.Create(ParkingSeedConstants.LotL1, 50));
        }
        if (!await dbContext.ReservationRatioPolicies.AnyAsync(p => p.LotId == ParkingSeedConstants.LotL2, cancellationToken))
        {
            dbContext.ReservationRatioPolicies.Add(ReservationRatioPolicy.Create(ParkingSeedConstants.LotL2, 0));
        }

        // Availability policies (L1: true, L2: false)
        if (!await dbContext.ReservationAvailabilityPolicies.AnyAsync(p => p.LotId == ParkingSeedConstants.LotL1, cancellationToken))
        {
            dbContext.ReservationAvailabilityPolicies.Add(ReservationAvailabilityPolicy.Create(ParkingSeedConstants.LotL1, true));
        }
        if (!await dbContext.ReservationAvailabilityPolicies.AnyAsync(p => p.LotId == ParkingSeedConstants.LotL2, cancellationToken))
        {
            dbContext.ReservationAvailabilityPolicies.Add(ReservationAvailabilityPolicy.Create(ParkingSeedConstants.LotL2, false));
        }

        // 3. Lot Assignments (Owner & Staff)
        // Owners
        await EnsureAssignmentAsync(dbContext, ParkingSeedConstants.LotL1, ParkingSeedConstants.OwnerA, ParkingLotMemberRole.Owner, ParkingSeedConstants.OwnerA, now, cancellationToken);
        await EnsureAssignmentAsync(dbContext, ParkingSeedConstants.LotL2, ParkingSeedConstants.OwnerA, ParkingLotMemberRole.Owner, ParkingSeedConstants.OwnerA, now, cancellationToken);
        await EnsureAssignmentAsync(dbContext, ParkingSeedConstants.LotL3, ParkingSeedConstants.OwnerB, ParkingLotMemberRole.Owner, ParkingSeedConstants.OwnerB, now, cancellationToken);
        await EnsureAssignmentAsync(dbContext, ParkingSeedConstants.LotL4, ParkingSeedConstants.OwnerA, ParkingLotMemberRole.Owner, ParkingSeedConstants.OwnerA, now, cancellationToken);
        await EnsureAssignmentAsync(dbContext, ParkingSeedConstants.LotL5, ParkingSeedConstants.OwnerB, ParkingLotMemberRole.Owner, ParkingSeedConstants.OwnerB, now, cancellationToken);

        // Staff (3): (L1, S1), (L1, S2), (L2, S1)
        await EnsureAssignmentAsync(dbContext, ParkingSeedConstants.LotL1, ParkingSeedConstants.StaffS1, ParkingLotMemberRole.Staff, ParkingSeedConstants.OwnerA, now, cancellationToken);
        await EnsureAssignmentAsync(dbContext, ParkingSeedConstants.LotL1, ParkingSeedConstants.StaffS2, ParkingLotMemberRole.Staff, ParkingSeedConstants.OwnerA, now, cancellationToken);
        await EnsureAssignmentAsync(dbContext, ParkingSeedConstants.LotL2, ParkingSeedConstants.StaffS1, ParkingLotMemberRole.Staff, ParkingSeedConstants.OwnerA, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        // 4. Floors (4)
        if (!await dbContext.Floors.AnyAsync(f => f.Id == ParkingSeedConstants.Floor1_L1, cancellationToken))
            dbContext.Floors.Add(Floor.Create(ParkingSeedConstants.Floor1_L1, ParkingSeedConstants.LotL1, "Tầng 1"));
        if (!await dbContext.Floors.AnyAsync(f => f.Id == ParkingSeedConstants.Floor2_L1, cancellationToken))
            dbContext.Floors.Add(Floor.Create(ParkingSeedConstants.Floor2_L1, ParkingSeedConstants.LotL1, "Tầng 2"));
        if (!await dbContext.Floors.AnyAsync(f => f.Id == ParkingSeedConstants.FloorB1_L2, cancellationToken))
            dbContext.Floors.Add(Floor.Create(ParkingSeedConstants.FloorB1_L2, ParkingSeedConstants.LotL2, "Hầm B1"));
        if (!await dbContext.Floors.AnyAsync(f => f.Id == ParkingSeedConstants.FloorGround_L3, cancellationToken))
            dbContext.Floors.Add(Floor.Create(ParkingSeedConstants.FloorGround_L3, ParkingSeedConstants.LotL3, "Mặt bằng"));

        await dbContext.SaveChangesAsync(cancellationToken);

        // 5. Zones (5)
        if (!await dbContext.Zones.AnyAsync(z => z.Id == ParkingSeedConstants.ZoneA_L1, cancellationToken))
            dbContext.Zones.Add(Zone.Create(ParkingSeedConstants.ZoneA_L1, ParkingSeedConstants.Floor1_L1, "Z-A", VehicleType.Car, 10, true));
        if (!await dbContext.Zones.AnyAsync(z => z.Id == ParkingSeedConstants.ZoneB_L1, cancellationToken))
            dbContext.Zones.Add(Zone.Create(ParkingSeedConstants.ZoneB_L1, ParkingSeedConstants.Floor2_L1, "Z-B", VehicleType.Car, 5, false));
        if (!await dbContext.Zones.AnyAsync(z => z.Id == ParkingSeedConstants.ZoneM_L1, cancellationToken))
        {
            var zm = Zone.Create(ParkingSeedConstants.ZoneM_L1, ParkingSeedConstants.Floor1_L1, "Z-M", VehicleType.Motorbike, 20, false);
            zm.UpdateCurrentVehicleCount(7);
            dbContext.Zones.Add(zm);
        }
        if (!await dbContext.Zones.AnyAsync(z => z.Id == ParkingSeedConstants.ZoneC_L2, cancellationToken))
            dbContext.Zones.Add(Zone.Create(ParkingSeedConstants.ZoneC_L2, ParkingSeedConstants.FloorB1_L2, "Z-C", VehicleType.Car, 10, true));
        if (!await dbContext.Zones.AnyAsync(z => z.Id == ParkingSeedConstants.ZoneD_L3, cancellationToken))
            dbContext.Zones.Add(Zone.Create(ParkingSeedConstants.ZoneD_L3, ParkingSeedConstants.FloorGround_L3, "Z-D", VehicleType.Car, 7, false));

        await dbContext.SaveChangesAsync(cancellationToken);

        // 6. Slots (32)
        // L1 Zone A: A-01..A-10 (10 slots)
        for (int i = 1; i <= 10; i++)
        {
            var slotId = Guid.Parse($"30000000-0000-0000-0000-0000000000{i:D2}");
            if (!await dbContext.ParkingSlots.AnyAsync(s => s.Id == slotId, cancellationToken))
            {
                var code = $"A-{i:D2}";
                OverrideState? declaredOverride = i switch
                {
                    9 => OverrideState.Maintenance,
                    10 => OverrideState.Unavailable,
                    _ => null
                };

                var slot = ParkingSlot.Create(slotId, ParkingSeedConstants.LotL1, ParkingSeedConstants.ZoneA_L1, code, declaredOverride);

                if (i == 1)
                    slot.UpdateSensorState(SensorState.Empty);
                else if (i is 2 or 3)
                    slot.UpdateSensorState(SensorState.Occupied);
                else if (i == 5)
                    slot.MarkNeedsVerification();

                dbContext.ParkingSlots.Add(slot);
            }
        }

        // L1 Zone B: B-01..B-05 (5 slots)
        for (int i = 1; i <= 5; i++)
        {
            var slotId = Guid.Parse($"30000000-0000-0000-0000-0000000000{10 + i:D2}");
            if (!await dbContext.ParkingSlots.AnyAsync(s => s.Id == slotId, cancellationToken))
            {
                var slot = ParkingSlot.Create(slotId, ParkingSeedConstants.LotL1, ParkingSeedConstants.ZoneB_L1, $"B-{i:D2}");
                dbContext.ParkingSlots.Add(slot);
            }
        }

        // L2 Zone C: C-01..C-10 (10 slots)
        for (int i = 1; i <= 10; i++)
        {
            var slotId = Guid.Parse($"30000000-0000-0000-0000-0000000000{15 + i:D2}");
            if (!await dbContext.ParkingSlots.AnyAsync(s => s.Id == slotId, cancellationToken))
            {
                var slot = ParkingSlot.Create(slotId, ParkingSeedConstants.LotL2, ParkingSeedConstants.ZoneC_L2, $"C-{i:D2}");
                dbContext.ParkingSlots.Add(slot);
            }
        }

        // L3 Zone D: D-01..D-07 (7 slots)
        for (int i = 1; i <= 7; i++)
        {
            var slotId = Guid.Parse($"30000000-0000-0000-0000-0000000000{25 + i:D2}");
            if (!await dbContext.ParkingSlots.AnyAsync(s => s.Id == slotId, cancellationToken))
            {
                var slot = ParkingSlot.Create(slotId, ParkingSeedConstants.LotL3, ParkingSeedConstants.ZoneD_L3, $"D-{i:D2}");
                dbContext.ParkingSlots.Add(slot);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // 7. Pricing Policies (5)
        if (!await dbContext.PricingPolicies.AnyAsync(p => p.Id == ParkingSeedConstants.PriceP1_L1, cancellationToken))
        {
            dbContext.PricingPolicies.Add(PricingPolicy.Create(
                ParkingSeedConstants.PriceP1_L1,
                ParkingSeedConstants.LotL1,
                "Car",
                2,
                "Block",
                60,
                15_000m,
                50_000m,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        if (!await dbContext.PricingPolicies.AnyAsync(p => p.Id == ParkingSeedConstants.PriceP2_L1_Hist, cancellationToken))
        {
            dbContext.PricingPolicies.Add(PricingPolicy.Create(
                ParkingSeedConstants.PriceP2_L1_Hist,
                ParkingSeedConstants.LotL1,
                "Car",
                1,
                "Block",
                60,
                12_000m,
                40_000m,
                new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        if (!await dbContext.PricingPolicies.AnyAsync(p => p.Id == ParkingSeedConstants.PriceP3_L1_Moto, cancellationToken))
        {
            dbContext.PricingPolicies.Add(PricingPolicy.Create(
                ParkingSeedConstants.PriceP3_L1_Moto,
                ParkingSeedConstants.LotL1,
                "Motorbike",
                1,
                "PerEntry",
                null,
                5_000m,
                10_000m,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        if (!await dbContext.PricingPolicies.AnyAsync(p => p.Id == ParkingSeedConstants.PriceP4_L2, cancellationToken))
        {
            dbContext.PricingPolicies.Add(PricingPolicy.Create(
                ParkingSeedConstants.PriceP4_L2,
                ParkingSeedConstants.LotL2,
                "Car",
                1,
                "PerEntry",
                null,
                30_000m,
                20_000m,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        if (!await dbContext.PricingPolicies.AnyAsync(p => p.Id == ParkingSeedConstants.PriceP5_L3, cancellationToken))
        {
            dbContext.PricingPolicies.Add(PricingPolicy.Create(
                ParkingSeedConstants.PriceP5_L3,
                ParkingSeedConstants.LotL3,
                "Car",
                1,
                "Block",
                30,
                8_000m,
                30_000m,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // 8. Parking Sessions (3)
        // S1: L1, Car, Active, now - 40p, R7, slot A-04
        if (!await dbContext.ParkingSessions.AnyAsync(s => s.Id == ParkingSeedConstants.SessionS1, cancellationToken))
        {
            var s1 = ParkingSession.Open(
                ParkingSeedConstants.SessionS1,
                ParkingSeedConstants.LotL1,
                "59C-345.67",
                VehicleType.Car,
                now.AddMinutes(-40),
                "0000000000000000000000000000000000000000000000000000000000000001",
                ParkingSeedConstants.UserU2,
                ParkingSeedConstants.VehicleV3,
                ParkingSeedConstants.ZoneA_L1,
                ParkingSeedConstants.ReservationR7);
            dbContext.ParkingSessions.Add(s1);
        }

        // S2: L1, Car, Completed, entry now - 1 day, exit now - 22 hours
        if (!await dbContext.ParkingSessions.AnyAsync(s => s.Id == ParkingSeedConstants.SessionS2, cancellationToken))
        {
            var s2 = ParkingSession.Open(
                ParkingSeedConstants.SessionS2,
                ParkingSeedConstants.LotL1,
                "51A-999.99",
                VehicleType.Car,
                now.AddDays(-1),
                "0000000000000000000000000000000000000000000000000000000000000002",
                ParkingSeedConstants.UserU1,
                ParkingSeedConstants.VehicleV1,
                ParkingSeedConstants.ZoneA_L1);
            s2.Complete();
            dbContext.ParkingSessions.Add(s2);
        }

        // S3: L1, Motorbike, Active, entry now - 3h
        if (!await dbContext.ParkingSessions.AnyAsync(s => s.Id == ParkingSeedConstants.SessionS3, cancellationToken))
        {
            var s3 = ParkingSession.Open(
                ParkingSeedConstants.SessionS3,
                ParkingSeedConstants.LotL1,
                "59X-123.45",
                VehicleType.Motorbike,
                now.AddHours(-3),
                "0000000000000000000000000000000000000000000000000000000000000003",
                null,
                null,
                ParkingSeedConstants.ZoneM_L1);
            dbContext.ParkingSessions.Add(s3);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Update slot A-04 to Assigned with Session S1
        var slotA04Id = Guid.Parse("30000000-0000-0000-0000-000000000004");
        var slotA04 = await dbContext.ParkingSlots.FirstOrDefaultAsync(s => s.Id == slotA04Id, cancellationToken);
        if (slotA04 != null && slotA04.AssignedSessionId != ParkingSeedConstants.SessionS1)
        {
            slotA04.AssignSession(ParkingSeedConstants.SessionS1, now.AddMinutes(-40));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // 9. Audit logs (2)
        if (!await dbContext.AuditLogEntries.AnyAsync(a => a.Id == ParkingSeedConstants.AuditA1, cancellationToken))
        {
            dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = ParkingSeedConstants.AuditA1,
                LotId = ParkingSeedConstants.LotL1,
                ActorUserId = ParkingSeedConstants.OwnerA,
                Action = "SLOT_OVERRIDE",
                TargetType = "ParkingSlot",
                TargetId = "30000000-0000-0000-0000-000000000009",
                Reason = "Periodic sensor and line-marking maintenance",
                Details = "{\"overrideState\":\"Maintenance\",\"slotCode\":\"A-09\"}",
                OccurredAt = now.AddHours(-2)
            });
        }

        if (!await dbContext.AuditLogEntries.AnyAsync(a => a.Id == ParkingSeedConstants.AuditA2, cancellationToken))
        {
            dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = ParkingSeedConstants.AuditA2,
                LotId = ParkingSeedConstants.LotL1,
                ActorUserId = ParkingSeedConstants.OwnerA,
                Action = "PRICING_UPDATED",
                TargetType = "PricingPolicy",
                TargetId = ParkingSeedConstants.PriceP1_L1.ToString(),
                Reason = "Approved rate adjustment for year 2026",
                Details = "{\"billingMode\":\"Block\",\"price\":15000,\"overnightFee\":50000}",
                OccurredAt = now.AddDays(-10)
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger?.LogInformation("Parking seed completed successfully.");
    }

    private static async Task EnsureAssignmentAsync(
        ParkingDbContext dbContext,
        Guid lotId,
        Guid accountId,
        ParkingLotMemberRole role,
        Guid assignedBy,
        DateTimeOffset assignedAt,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ParkingLotAssignments
            .FirstOrDefaultAsync(a => a.LotId == lotId && a.AccountId == accountId, cancellationToken);

        if (existing is null)
        {
            dbContext.ParkingLotAssignments.Add(
                ParkingLotAssignment.Create(lotId, accountId, role, assignedBy, assignedAt));
        }
    }

    private static async Task ResetSeedDataAsync(ParkingDbContext dbContext, CancellationToken cancellationToken)
    {
        var seedLotIds = ParkingSeedConstants.SeedLotIds;

        // 1. Audit logs
        var auditIds = new[] { ParkingSeedConstants.AuditA1, ParkingSeedConstants.AuditA2 };
        var audits = await dbContext.AuditLogEntries.Where(a => auditIds.Contains(a.Id)).ToListAsync(cancellationToken);
        dbContext.AuditLogEntries.RemoveRange(audits);

        // 2. Clear assigned sessions on slots before removing sessions
        var slots = await dbContext.ParkingSlots.Where(s => seedLotIds.Contains(s.LotId)).ToListAsync(cancellationToken);
        foreach (var slot in slots)
        {
            slot.ReleaseAssignment();
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // 3. Sessions
        var sessionIds = new[] { ParkingSeedConstants.SessionS1, ParkingSeedConstants.SessionS2, ParkingSeedConstants.SessionS3 };
        var sessions = await dbContext.ParkingSessions.Where(s => sessionIds.Contains(s.Id)).ToListAsync(cancellationToken);
        dbContext.ParkingSessions.RemoveRange(sessions);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 4. Slots
        dbContext.ParkingSlots.RemoveRange(slots);

        // 5. Zones
        var zones = await dbContext.Zones.Where(z => dbContext.Floors.Where(f => seedLotIds.Contains(f.LotId)).Select(f => f.Id).Contains(z.FloorId)).ToListAsync(cancellationToken);
        dbContext.Zones.RemoveRange(zones);

        // 6. Floors
        var floors = await dbContext.Floors.Where(f => seedLotIds.Contains(f.LotId)).ToListAsync(cancellationToken);
        dbContext.Floors.RemoveRange(floors);

        // 7. Pricing policies
        var pricing = await dbContext.PricingPolicies.Where(p => seedLotIds.Contains(p.LotId)).ToListAsync(cancellationToken);
        dbContext.PricingPolicies.RemoveRange(pricing);

        // 8. Policies
        var deposit = await dbContext.ReservationDepositPolicies.Where(p => seedLotIds.Contains(p.LotId)).ToListAsync(cancellationToken);
        dbContext.ReservationDepositPolicies.RemoveRange(deposit);

        var ratio = await dbContext.ReservationRatioPolicies.Where(p => seedLotIds.Contains(p.LotId)).ToListAsync(cancellationToken);
        dbContext.ReservationRatioPolicies.RemoveRange(ratio);

        var availability = await dbContext.ReservationAvailabilityPolicies.Where(p => seedLotIds.Contains(p.LotId)).ToListAsync(cancellationToken);
        dbContext.ReservationAvailabilityPolicies.RemoveRange(availability);

        // 9. Lot Assignments
        var assignments = await dbContext.ParkingLotAssignments.Where(a => seedLotIds.Contains(a.LotId)).ToListAsync(cancellationToken);
        dbContext.ParkingLotAssignments.RemoveRange(assignments);

        // 10. Lots
        var lots = await dbContext.ParkingLots.Where(l => seedLotIds.Contains(l.Id)).ToListAsync(cancellationToken);
        dbContext.ParkingLots.RemoveRange(lots);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
