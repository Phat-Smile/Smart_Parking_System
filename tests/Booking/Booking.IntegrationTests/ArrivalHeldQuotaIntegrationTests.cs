using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ArrivalHeldQuotaIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Counts_only_currently_active_car_reservations_overlapping_future_arrival()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var config = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();
        var services = new ServiceCollection();
        services.AddPersistence(config);
        services.AddLogging();
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var now = DateTimeOffset.UtcNow;
        var arrivalAt = now.AddHours(1);
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
            var confirmed = Create(lotId, now, arrivalAt, now.AddMinutes(10));
            confirmed.Confirm(now);
            var temporaryHold = Create(lotId, now, arrivalAt, now.AddMinutes(10));
            var expiredHold = Create(lotId, now, arrivalAt, now.AddMinutes(-10));
            var outsideWindow = Create(lotId, now, arrivalAt.AddHours(3), now.AddMinutes(10));
            outsideWindow.Confirm(now);
            var used = Create(lotId, now, arrivalAt, now.AddMinutes(10));
            used.Confirm(now);
            used.Use(now);
            db.Reservations.AddRange(confirmed, temporaryHold, expiredHold, outsideWindow, used);
            await db.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();
            Assert.Equal(2, await reader.CountAsync(lotId, arrivalAt, cancellationToken));
            Assert.Equal(0, await reader.CountAsync(Guid.NewGuid(), arrivalAt, cancellationToken));
        }
    }

    [Fact]
    public async Task J_05_HeldQuota_CountedAt_A_Plus_15M59S_NotCountedAt_A_Plus_16M00S()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var config = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();
        var services = new ServiceCollection();
        services.AddPersistence(config);
        services.AddLogging();
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var now = DateTimeOffset.UtcNow;
        var appointmentTime = now.AddHours(2);
        var lotId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            // Create using default windows (holdEndAt defaults to A + 16:00)
            var res = Reservation.CreateTemporaryHold(
                id: Guid.NewGuid(),
                userId: Guid.NewGuid(),
                vehicleId: Guid.NewGuid(),
                plateNumber: "30A" + Guid.NewGuid().ToString("N")[..8],
                lotId: lotId,
                vehicleGroup: VehicleGroup.Car,
                reservationType: ReservationType.Scheduled,
                appointmentTime: appointmentTime,
                prepaidAmount: 50000m,
                createdAt: now,
                durationMinutes: 120,
                tempHoldExpiresAt: now.AddMinutes(10));
            res.Confirm(now);
            db.Reservations.Add(res);
            await db.SaveChangesAsync(cancellationToken);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IArrivalHeldQuotaReader>();

            // At A + 15:59 -> inside [WindowStartAt, HoldEndAt) -> count = 1
            var at15M59S = appointmentTime.AddMinutes(15).AddSeconds(59);
            var countAt15M59S = await reader.CountAsync(lotId, at15M59S, cancellationToken);
            Assert.Equal(1, countAt15M59S);

            // At A + 16:00 -> at HoldEndAt boundary -> half-open -> count = 0
            var at16M00S = appointmentTime.AddMinutes(16);
            var countAt16M00S = await reader.CountAsync(lotId, at16M00S, cancellationToken);
            Assert.Equal(0, countAt16M00S);
        }
    }

    private static Reservation Create(Guid lotId, DateTimeOffset now,
        DateTimeOffset at, DateTimeOffset holdExpiry)
    {
        return Reservation.CreateTemporaryHold(
            id: Guid.NewGuid(), userId: Guid.NewGuid(), vehicleId: Guid.NewGuid(),
            plateNumber: "30A" + Guid.NewGuid().ToString("N")[..8],
            lotId: lotId, vehicleGroup: VehicleGroup.Car,
            reservationType: ReservationType.Scheduled,
            appointmentTime: at, prepaidAmount: 0, createdAt: now,
            durationMinutes: 60, tempHoldExpiresAt: holdExpiry,
            windowStartAt: at.AddMinutes(-30), windowEndAt: at.AddHours(1),
            holdEndAt: at.AddHours(1));
    }
}
