using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application;
using ParkingSystem.Parking.Application.ParkingLots.SearchCustomerLots;
using ParkingSystem.Parking.Application.ParkingLots.OpeningHours;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class CustomerLotSearchIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Search_filters_vehicle_price_and_live_motorbike_capacity()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddApplicationServices();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.MigrateAsync(ct);
        var lot = Guid.NewGuid();
        var floor = Guid.NewGuid();
        var zone = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status,
                supports_electric_vehicles, supports_large_vehicles, has_ev_charging, is_covered, has_cctv)
            VALUES ({lot}, 'Customer search', 'Demo', 10.77, 106.70, 'Outdoor', 'Approved',
                true, true, true, true, true)
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.floors (id, lot_id, name) VALUES ({floor}, {lot}, 'F1')
            """, ct);
        var carZone = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.zones (id, floor_id, code, vehicle_group, capacity, current_vehicle_count, has_slot_sensor)
            VALUES ({carZone}, {floor}, 'C1', 'Car', 10, 0, false)
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.zones (id, floor_id, code, vehicle_group, capacity, current_vehicle_count, has_slot_sensor)
            VALUES ({zone}, {floor}, 'M1', 'Motorbike', 10, 2, false)
            """, ct);

        var motorbikeOnlyLot = Guid.NewGuid();
        var mbFloor = Guid.NewGuid();
        var mbZone = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status,
                supports_electric_vehicles, supports_large_vehicles, has_ev_charging, is_covered, has_cctv)
            VALUES ({motorbikeOnlyLot}, 'Motorbike only', 'Demo', 10.77, 106.70, 'Outdoor', 'Approved',
                true, true, true, true, true)
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.floors (id, lot_id, name) VALUES ({mbFloor}, {motorbikeOnlyLot}, 'F1')
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO parking.zones (id, floor_id, code, vehicle_group, capacity, current_vehicle_count, has_slot_sensor)
            VALUES ({mbZone}, {mbFloor}, 'M1', 'Motorbike', 10, 0, false)
            """, ct);
        db.PricingPolicies.Add(PricingPolicy.Create(motorbikeOnlyLot, "MOTORBIKE", 1, "PerHour", null, 25000, 0));
        db.PricingPolicies.Add(PricingPolicy.Create(lot, "MOTORBIKE", 1, "PerHour", null, 15000, 0));
        var savedLot = await db.ParkingLots.SingleAsync(x => x.Id == lot, ct);
        savedLot.SetOpeningHours(OpeningHoursSchedule.Serialize(new WeeklyOpeningHours(
            Enumerable.Range(0, 7).Select(day => new DailyOpeningHours(day, false, true, null, null)).ToArray())));
        await db.SaveChangesAsync(ct);

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var matches = await sender.Send(new SearchCustomerLotsQuery(null, null, 5,
            VehicleType.Motorbike, 20000, true, "price"), ct);
        var tooCheap = await sender.Send(new SearchCustomerLotsQuery(null, null, 5,
            VehicleType.Motorbike, 10000, true, "price"), ct);
        var wrongType = await sender.Send(new SearchCustomerLotsQuery(10.77m, 106.70m, 2,
            VehicleType.Car, null, false, "distance"), ct);
        var withFeatures = await sender.Send(new SearchCustomerLotsQuery(null, null, 5,
            VehicleType.Motorbike, null, false, "name", Compatibility: ["ev"],
            Amenities: ["ev-charging", "always-open", "cctv"]), ct);

        var result = Assert.Single(matches, x => x.Id == lot);
        Assert.Equal(15000, result.StartingPrice);
        Assert.Equal(8, result.AvailableSpaces);
        Assert.DoesNotContain(tooCheap, x => x.Id == lot);
        Assert.DoesNotContain(wrongType, x => x.Id == motorbikeOnlyLot);
        Assert.Contains(wrongType, x => x.Id == lot);
        Assert.Contains(withFeatures, x => x.Id == lot && x.Compatibility.Contains("ev")
            && x.Amenities.Contains("always-open") && x.Prebookable);

        db.ReservationAvailabilityPolicies.Add(ReservationAvailabilityPolicy.Create(lot, false));
        await db.SaveChangesAsync(ct);
        var prebookable = await sender.Send(new SearchCustomerLotsQuery(null, null, 5,
            VehicleType.Motorbike, null, false, "name", PrebookableOnly: true), ct);
        Assert.DoesNotContain(prebookable, x => x.Id == lot);
    }
}
