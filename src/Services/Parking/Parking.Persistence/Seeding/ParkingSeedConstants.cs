using System;

namespace ParkingSystem.Parking.Persistence.Seeding;

public static class ParkingSeedConstants
{
    // Owners
    public static readonly Guid OwnerA = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid OwnerB = Guid.Parse("a0000000-0000-0000-0000-000000000002");

    // Lots
    public static readonly Guid LotL1 = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    public static readonly Guid LotL2 = Guid.Parse("b0000000-0000-0000-0000-000000000002");
    public static readonly Guid LotL3 = Guid.Parse("b0000000-0000-0000-0000-000000000003");
    public static readonly Guid LotL4 = Guid.Parse("b0000000-0000-0000-0000-000000000004");
    public static readonly Guid LotL5 = Guid.Parse("b0000000-0000-0000-0000-000000000005");

    public static readonly Guid[] SeedLotIds = [LotL1, LotL2, LotL3, LotL4, LotL5];

    // Staff
    public static readonly Guid StaffS1 = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    public static readonly Guid StaffS2 = Guid.Parse("d0000000-0000-0000-0000-000000000002");

    // Customers / Users
    public static readonly Guid UserU1 = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    public static readonly Guid UserU2 = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    public static readonly Guid UserU3 = Guid.Parse("c0000000-0000-0000-0000-000000000003");
    public static readonly Guid UserU4 = Guid.Parse("c0000000-0000-0000-0000-000000000004");

    // Vehicles
    public static readonly Guid VehicleV1 = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    public static readonly Guid VehicleV2 = Guid.Parse("e0000000-0000-0000-0000-000000000002");
    public static readonly Guid VehicleV3 = Guid.Parse("e0000000-0000-0000-0000-000000000003");

    // Reservations
    public static readonly Guid ReservationR7 = Guid.Parse("f0000000-0000-0000-0000-000000000007");

    // Floors (4)
    public static readonly Guid Floor1_L1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid Floor2_L1 = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid FloorB1_L2 = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid FloorGround_L3 = Guid.Parse("10000000-0000-0000-0000-000000000004");

    public static readonly Guid[] SeedFloorIds = [Floor1_L1, Floor2_L1, FloorB1_L2, FloorGround_L3];

    // Zones (5)
    public static readonly Guid ZoneA_L1 = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid ZoneB_L1 = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid ZoneM_L1 = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid ZoneC_L2 = Guid.Parse("20000000-0000-0000-0000-000000000004");
    public static readonly Guid ZoneD_L3 = Guid.Parse("20000000-0000-0000-0000-000000000005");

    public static readonly Guid[] SeedZoneIds = [ZoneA_L1, ZoneB_L1, ZoneM_L1, ZoneC_L2, ZoneD_L3];

    // Price rules (5)
    public static readonly Guid PriceP1_L1 = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid PriceP2_L1_Hist = Guid.Parse("40000000-0000-0000-0000-000000000002");
    public static readonly Guid PriceP3_L1_Moto = Guid.Parse("40000000-0000-0000-0000-000000000003");
    public static readonly Guid PriceP4_L2 = Guid.Parse("40000000-0000-0000-0000-000000000004");
    public static readonly Guid PriceP5_L3 = Guid.Parse("40000000-0000-0000-0000-000000000005");

    public static readonly Guid[] SeedPricingPolicyIds = [PriceP1_L1, PriceP2_L1_Hist, PriceP3_L1_Moto, PriceP4_L2, PriceP5_L3];

    // Sessions (3)
    public static readonly Guid SessionS1 = Guid.Parse("50000000-0000-0000-0000-000000000001");
    public static readonly Guid SessionS2 = Guid.Parse("50000000-0000-0000-0000-000000000002");
    public static readonly Guid SessionS3 = Guid.Parse("50000000-0000-0000-0000-000000000003");

    public static readonly Guid[] SeedSessionIds = [SessionS1, SessionS2, SessionS3];

    // Audit logs (2)
    public static readonly Guid AuditA1 = Guid.Parse("80000000-0000-0000-0000-000000000001");
    public static readonly Guid AuditA2 = Guid.Parse("80000000-0000-0000-0000-000000000002");

    public static readonly Guid[] SeedAuditIds = [AuditA1, AuditA2];
}
