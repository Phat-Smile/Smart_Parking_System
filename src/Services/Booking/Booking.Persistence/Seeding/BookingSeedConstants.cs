using System;

namespace ParkingSystem.Booking.Persistence.Seeding;

/// <summary>
/// Seed constants for Booking Service.
/// Note: TECH-04 (booking schema migration and capacity entity) must be completed before Booking seeding can execute.
/// These constants mirror the shared system seed identifiers from Parking service.
/// </summary>
public static class BookingSeedConstants
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

    // Customers / Users
    public static readonly Guid UserU1 = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    public static readonly Guid UserU2 = Guid.Parse("c0000000-0000-0000-0000-000000000002");
    public static readonly Guid UserU3 = Guid.Parse("c0000000-0000-0000-0000-000000000003");
    public static readonly Guid UserU4 = Guid.Parse("c0000000-0000-0000-0000-000000000004");

    // Vehicles
    public static readonly Guid VehicleV1 = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    public static readonly Guid VehicleV2 = Guid.Parse("e0000000-0000-0000-0000-000000000002");
    public static readonly Guid VehicleV3 = Guid.Parse("e0000000-0000-0000-0000-000000000003");
    public static readonly Guid VehicleV4 = Guid.Parse("e0000000-0000-0000-0000-000000000004");
    public static readonly Guid VehicleV5 = Guid.Parse("e0000000-0000-0000-0000-000000000005");
    public static readonly Guid VehicleV6 = Guid.Parse("e0000000-0000-0000-0000-000000000006");

    // Reservations (11)
    public static readonly Guid ReservationR1 = Guid.Parse("f0000000-0000-0000-0000-000000000001");
    public static readonly Guid ReservationR2 = Guid.Parse("f0000000-0000-0000-0000-000000000002");
    public static readonly Guid ReservationR3 = Guid.Parse("f0000000-0000-0000-0000-000000000003");
    public static readonly Guid ReservationR4 = Guid.Parse("f0000000-0000-0000-0000-000000000004");
    public static readonly Guid ReservationR5 = Guid.Parse("f0000000-0000-0000-0000-000000000005");
    public static readonly Guid ReservationR6 = Guid.Parse("f0000000-0000-0000-0000-000000000006");
    public static readonly Guid ReservationR7 = Guid.Parse("f0000000-0000-0000-0000-000000000007");
    public static readonly Guid ReservationR8 = Guid.Parse("f0000000-0000-0000-0000-000000000008");
    public static readonly Guid ReservationR9 = Guid.Parse("f0000000-0000-0000-0000-000000000009");
    public static readonly Guid ReservationR10 = Guid.Parse("f0000000-0000-0000-0000-000000000010");
    public static readonly Guid ReservationR11 = Guid.Parse("f0000000-0000-0000-0000-000000000011");

    // Sessions (referenced in booking: R7 linked to S1)
    public static readonly Guid SessionS1 = Guid.Parse("50000000-0000-0000-0000-000000000001");
}
