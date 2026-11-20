using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingSystem.Booking.Application.Common.Messaging.Outbox;
using ParkingSystem.Booking.Application.Reservations;
using ParkingSystem.Booking.Application.Reservations.Commands.CheckInReservation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Infrastructure.Workers;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

/// <summary>
/// E2E Integration tests for the Booking Arrival Chain (US-RES-007, US-RES-008, US-PARK-007, US-PARK-009):
/// Scenario 1: Confirmed reservation -> vehicle arrives at gate -> Check-in succeeds -> transitions to Used with session link.
/// Scenario 2: Confirmed reservation -> vehicle does not arrive within 15-minute grace period -> No-show worker cancels reservation, forfeits deposit, and publishes cancellation outbox event.
/// </summary>
public sealed class BookingArrivalChainIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly DateTimeOffset AppointmentTime = new(2026, 11, 20, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ConfirmedAtTime = AppointmentTime.AddHours(-2);

    private sealed class FakeClock(DateTimeOffset initial) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }

    private ServiceProvider CreateProvider(FakeClock clock)
    {
        var configuration = new ConfigurationBuilder()
            .AddConfiguration(PostgreSqlTestConfiguration.Create(postgres.ConnectionString))
            .Build();

        var services = new ServiceCollection();
        services.AddPersistence(configuration);
        services.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IClock>(clock);
        services.AddScoped<CheckInReservationCommandHandler>();
        services.Configure<ReservationNoShowOptions>(opt =>
        {
            opt.BatchSize = 50;
            opt.IntervalSeconds = 60;
        });
        services.AddScoped<ReservationNoShowWorker>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task EnsureMigratedAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    [Fact]
    public async Task Scenario1_ConfirmedReservation_ArrivalWithinWindow_CheckInSucceeds_TransitionsToUsed_WithSessionLink()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(AppointmentTime.AddMinutes(-5));
        await using var provider = CreateProvider(clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var plateNumber = "51A-888.99";

        // 1. Seed Confirmed reservation in PostgreSQL database
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var reservation = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                vehicleId,
                plateNumber,
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                AppointmentTime,
                50000m,
                ConfirmedAtTime.AddMinutes(-10),
                durationMinutes: 120,
                tempHoldExpiresAt: ConfirmedAtTime.AddMinutes(15));

            reservation.Confirm(ConfirmedAtTime);
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);
        }

        // 2. Vehicle arrives at the gate within valid arrival window (5 minutes before appointment)
        var arrivalTime = AppointmentTime.AddMinutes(-5);
        var gateSessionId = Guid.NewGuid();

        // 3. Gate invokes check-in command handler
        await using (var checkInScope = provider.CreateAsyncScope())
        {
            var handler = checkInScope.ServiceProvider.GetRequiredService<CheckInReservationCommandHandler>();
            var result = await handler.Handle(
                new CheckInReservationCommand(lotId, plateNumber, arrivalTime, gateSessionId),
                ct);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value!.HasReservation);
            Assert.Equal(reservationId, result.Value.ReservationId);
        }

        // 4. Verify reservation is now in Used status with linked session ID in database
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var loaded = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reservationId, ct);

            Assert.NotNull(loaded);
            Assert.Equal(ReservationStatus.Used, loaded.Status);
            Assert.Equal(gateSessionId, loaded.SessionId);
            Assert.NotNull(loaded.UsedAt);
            Assert.False(loaded.IsNoShow);
            Assert.Null(loaded.CancelReason);
        }
    }

    [Fact]
    public async Task Scenario2_ConfirmedReservation_Past15MinuteGracePeriod_NoShowWorkerCancels_ForfeitsDeposit_AndEmitsOutboxEvent()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeClock(AppointmentTime.AddMinutes(20));
        await using var provider = CreateProvider(clock);
        await EnsureMigratedAsync(provider, ct);

        var reservationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var plateNumber = "51A-999.11";
        const decimal prepaidAmount = 60000m;

        // 1. Seed Confirmed reservation with prepaid deposit
        await using (var seedScope = provider.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var reservation = Reservation.CreateTemporaryHold(
                reservationId,
                userId,
                vehicleId,
                plateNumber,
                lotId,
                VehicleGroup.Car,
                ReservationType.Scheduled,
                AppointmentTime,
                prepaidAmount,
                ConfirmedAtTime.AddMinutes(-10),
                durationMinutes: 120,
                tempHoldExpiresAt: ConfirmedAtTime.AddMinutes(15));

            reservation.Confirm(ConfirmedAtTime);
            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);
        }

        // 2. Advance time past 15-minute grace period (+ 1m technical tolerance, cutoff is 14:16). Current time is 14:20.
        clock.UtcNow = AppointmentTime.AddMinutes(20);

        // 3. Run No-Show Background Worker pass
        await using (var workerScope = provider.CreateAsyncScope())
        {
            var worker = workerScope.ServiceProvider.GetRequiredService<ReservationNoShowWorker>();
            var processed = await worker.ProcessNoShowsAsync(ct);
            Assert.True(processed >= 1);
        }

        // 4. Verify reservation status in PostgreSQL database: Cancelled as NoShow, prepaid deposit forfeited
        await using (var verifyScope = provider.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<BookingDbContext>();
            var loaded = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reservationId, ct);

            Assert.NotNull(loaded);
            Assert.Equal(ReservationStatus.Cancelled, loaded.Status);
            Assert.Equal(CancelReason.NoShow, loaded.CancelReason);
            Assert.True(loaded.IsNoShow);
            Assert.Equal(PrepaidOutcome.Forfeited, loaded.PrepaidOutcome);
            Assert.NotNull(loaded.CancelledAt);

            // 5. Verify Outbox integration event: booking.reservation-cancelled.v1 with forfeited deposit
            var outboxMessage = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.CorrelationId == reservationId && m.EventType == "booking.reservation-cancelled.v1")
                .FirstOrDefaultAsync(ct);

            Assert.NotNull(outboxMessage);
            Assert.Contains(reservationId.ToString(), outboxMessage.Payload);
            Assert.Contains("NoShow", outboxMessage.Payload);

            var eventPayload = JsonSerializer.Deserialize<ReservationCancelledIntegrationEvent>(outboxMessage.Payload);
            Assert.NotNull(eventPayload);
            Assert.Equal(reservationId, eventPayload.ReservationId);
            Assert.Equal(userId, eventPayload.UserId);
            Assert.Equal(lotId, eventPayload.LotId);
            Assert.Equal("NoShow", eventPayload.CancelReason);
            Assert.Equal(0m, eventPayload.RefundAmount);
            Assert.Equal(prepaidAmount, eventPayload.ForfeitedAmount);
        }
    }
}
