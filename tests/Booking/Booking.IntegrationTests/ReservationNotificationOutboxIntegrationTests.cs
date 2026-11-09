using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Booking.Application;
using ParkingSystem.Booking.Application.Reservations.Commands.ConfirmReservation;
using ParkingSystem.Booking.Domain.Reservations;
using ParkingSystem.Booking.Persistence;
using ParkingSystem.Contracts.Events.Booking;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Booking.IntegrationTests;

public sealed class ReservationNotificationOutboxIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Confirming_a_free_hold_commits_one_customer_event_with_reservation()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        services.AddApplicationServices();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await db.Database.MigrateAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var reservation = Reservation.CreateTemporaryHold(Guid.NewGuid(), userId, Guid.NewGuid(),
            "51A12345", Guid.NewGuid(), VehicleGroup.Car, ReservationType.HoldWhileDriving,
            now.AddMinutes(20), 0m, now, tempHoldExpiresAt: now.AddMinutes(5));
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Assert.True((await sender.Send(new ConfirmReservationCommand(reservation.Id, userId, null, 0), ct)).IsSuccess);
        Assert.True((await sender.Send(new ConfirmReservationCommand(reservation.Id, userId, null, 0), ct)).IsSuccess);
        var message = Assert.Single(await db.OutboxMessages.AsNoTracking()
            .Where(x => x.EventType == "booking.reservation-confirmed.v1").ToListAsync(ct));
        var payload = JsonSerializer.Deserialize<ReservationConfirmedIntegrationEvent>(message.Payload);
        Assert.Equal(userId, payload!.UserId);
        Assert.Equal(reservation.Id, payload.ReservationId);
    }
}
