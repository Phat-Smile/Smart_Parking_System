using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.RegisterParkingLot;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class RegisterParkingLotIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Register_persists_owner_and_pending_status_without_exposing_lot_publicly()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var provider = services.BuildServiceProvider(validateScopes: true);

        var ownerId = Guid.NewGuid();
        Guid lotId;

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            var handler = new RegisterParkingLotCommandHandler(
                scope.ServiceProvider.GetRequiredService<IRegisterParkingLotRepository>(),
                scope.ServiceProvider.GetRequiredService<IClock>());
            var created = await handler.Handle(
                new RegisterParkingLotCommand(
                    ownerId, "New Owner Lot", "15 Demo Street",
                    10.123456m, 106.123456m, "Outdoor"),
                cancellationToken);

            lotId = created.Id;
            Assert.Equal("Pending", created.ApprovalStatus);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var persisted = await db.ParkingLots.AsNoTracking().SingleAsync(
                lot => lot.Id == lotId, cancellationToken);

            Assert.Equal(ownerId, persisted.OwnerUserId);
            Assert.Equal(ParkingLotApprovalStatus.Pending, persisted.ApprovalStatus);

            var publicReader = scope.ServiceProvider.GetRequiredService<IParkingLotCatalogReader>();
            var publicLots = await publicReader.ListApprovedAsync(cancellationToken);
            Assert.DoesNotContain(publicLots, lot => lot.Id == lotId);

            var detailReader = scope.ServiceProvider.GetRequiredService<IParkingLotDetailReader>();
            Assert.Null(await detailReader.GetApprovedByIdAsync(lotId, cancellationToken));
        }
    }

    [Fact]
    public async Task Duplicate_registration_returns_conflict_without_creating_another_row()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var ownerId = Guid.NewGuid();

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        var handler = new RegisterParkingLotCommandHandler(
            scope.ServiceProvider.GetRequiredService<IRegisterParkingLotRepository>(),
            scope.ServiceProvider.GetRequiredService<IClock>());
        var command = new RegisterParkingLotCommand(
            ownerId, "Review Duplicate", "15 Demo Street", 10m, 106m, "Outdoor");

        await handler.Handle(command, cancellationToken);
        await Assert.ThrowsAsync<DuplicateParkingLotException>(() => handler.Handle(command, cancellationToken));
        Assert.Equal(1, await db.ParkingLots.CountAsync(
            lot => lot.OwnerUserId == ownerId && lot.Name == command.Name, cancellationToken));
    }

    [Fact]
    public async Task Unique_index_rejects_a_racing_insert_even_without_a_precheck()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var ownerId = Guid.NewGuid();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            await db.Database.MigrateAsync(cancellationToken);
            var repository = scope.ServiceProvider.GetRequiredService<IRegisterParkingLotRepository>();
            await AddLotAsync(repository, ParkingLot.Register(
                Guid.NewGuid(), ownerId, "Race Condition Lot", "15 Demo Street",
                10m, 106m, ParkingLotType.Outdoor), cancellationToken);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRegisterParkingLotRepository>();
            await Assert.ThrowsAsync<DuplicateParkingLotException>(() => AddLotAsync(
                repository,
                ParkingLot.Register(Guid.NewGuid(), ownerId, "Race Condition Lot",
                    "15 Demo Street", 10m, 106m, ParkingLotType.Outdoor), cancellationToken));
        }
    }

    private static Task AddLotAsync(IRegisterParkingLotRepository repository, ParkingLot lot, CancellationToken cancellationToken) =>
        repository.AddAsync(
            lot,
            ParkingLotAssignment.Create(lot.Id, lot.OwnerUserId!.Value, ParkingLotMemberRole.Owner, lot.OwnerUserId.Value, DateTimeOffset.UtcNow),
            cancellationToken);
}
