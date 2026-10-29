using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Commands.ConfigureReservationRatioPolicy;
using ParkingSystem.Parking.Application.Features.ReservationRatioPolicy.Queries.GetReservationRatioPolicy;
using ParkingSystem.Parking.Domain.Entities;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ReservationRatioPolicyIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Configure_stores_policy_and_get_returns_stored_values()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);

        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(provider, "Approved", token);
        await AssignOwnerAsync(provider, lotId, ownerId, token);

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new ConfigureReservationRatioPolicyCommandHandler(
                services.GetRequiredService<IReservationRatioPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var result = await handler.Handle(new ConfigureReservationRatioPolicyCommand(lotId, 30, ownerId), token);
            Assert.True(result.IsSuccess);
            Assert.Equal(30, result.Value!.ReservationRatioPercent);
            Assert.True(result.Value.IsConfigured);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var queryHandler = new GetReservationRatioPolicyQueryHandler(
                services.GetRequiredService<IReservationRatioPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var getResult = await queryHandler.Handle(new GetReservationRatioPolicyQuery(lotId, ownerId), token);
            Assert.True(getResult.IsSuccess);
            Assert.True(getResult.Value!.IsConfigured);
            Assert.Equal(30, getResult.Value.ReservationRatioPercent);
            Assert.NotNull(getResult.Value.UpdatedAtUtc);
        }
    }

    [Fact]
    public async Task Configure_updating_existing_policy_modifies_record_in_place()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);

        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(provider, "Approved", token);
        await AssignOwnerAsync(provider, lotId, ownerId, token);

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new ConfigureReservationRatioPolicyCommandHandler(
                services.GetRequiredService<IReservationRatioPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var first = await handler.Handle(new ConfigureReservationRatioPolicyCommand(lotId, 40, ownerId), token);
            Assert.True(first.IsSuccess);

            var second = await handler.Handle(new ConfigureReservationRatioPolicyCommand(lotId, 80, ownerId), token);
            Assert.True(second.IsSuccess);
            Assert.Equal(80, second.Value!.ReservationRatioPercent);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var count = await db.ReservationRatioPolicies.CountAsync(p => p.LotId == lotId, token);
            Assert.Equal(1, count);

            var policy = await db.ReservationRatioPolicies.FirstAsync(p => p.LotId == lotId, token);
            Assert.Equal(80, policy.ReservationRatioPercent);
        }
    }

    [Fact]
    public async Task Configure_supports_boundary_values_0_and_100()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);

        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(provider, "Approved", token);
        await AssignOwnerAsync(provider, lotId, ownerId, token);

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new ConfigureReservationRatioPolicyCommandHandler(
                services.GetRequiredService<IReservationRatioPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var res0 = await handler.Handle(new ConfigureReservationRatioPolicyCommand(lotId, 0, ownerId), token);
            Assert.True(res0.IsSuccess);
            Assert.Equal(0, res0.Value!.ReservationRatioPercent);

            var res100 = await handler.Handle(new ConfigureReservationRatioPolicyCommand(lotId, 100, ownerId), token);
            Assert.True(res100.IsSuccess);
            Assert.Equal(100, res100.Value!.ReservationRatioPercent);
        }
    }

    [Fact]
    public async Task Two_lots_keep_different_ratios_independently()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);

        var owner1 = Guid.NewGuid();
        var owner2 = Guid.NewGuid();
        var lot1 = await InsertLotAsync(provider, "Approved", token);
        var lot2 = await InsertLotAsync(provider, "Approved", token);

        await AssignOwnerAsync(provider, lot1, owner1, token);
        await AssignOwnerAsync(provider, lot2, owner2, token);

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new ConfigureReservationRatioPolicyCommandHandler(
                services.GetRequiredService<IReservationRatioPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            await handler.Handle(new ConfigureReservationRatioPolicyCommand(lot1, 25, owner1), token);
            await handler.Handle(new ConfigureReservationRatioPolicyCommand(lot2, 75, owner2), token);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var queryHandler = new GetReservationRatioPolicyQueryHandler(
                services.GetRequiredService<IReservationRatioPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var res1 = await queryHandler.Handle(new GetReservationRatioPolicyQuery(lot1, owner1), token);
            var res2 = await queryHandler.Handle(new GetReservationRatioPolicyQuery(lot2, owner2), token);

            Assert.True(res1.IsSuccess);
            Assert.True(res2.IsSuccess);
            Assert.Equal(25, res1.Value!.ReservationRatioPercent);
            Assert.Equal(75, res2.Value!.ReservationRatioPercent);
        }
    }

    [Fact]
    public async Task Unconfigured_lot_returns_is_configured_false_and_default_50_ratio()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);

        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(provider, "Approved", token);
        await AssignOwnerAsync(provider, lotId, ownerId, token);

        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queryHandler = new GetReservationRatioPolicyQueryHandler(
            services.GetRequiredService<IReservationRatioPolicyRepository>(),
            services.GetRequiredService<ILotOwnershipService>());

        var result = await queryHandler.Handle(new GetReservationRatioPolicyQuery(lotId, ownerId), token);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsConfigured);
        Assert.Equal(50, result.Value.ReservationRatioPercent);
        Assert.Null(result.Value.UpdatedAtUtc);
    }

    [Fact]
    public async Task Concurrent_configure_requests_on_new_lot_do_not_create_duplicates()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);

        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(provider, "Approved", token);
        await AssignOwnerAsync(provider, lotId, ownerId, token);

        var task1 = Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = new ConfigureReservationRatioPolicyCommandHandler(
                scope.ServiceProvider.GetRequiredService<IReservationRatioPolicyRepository>(),
                scope.ServiceProvider.GetRequiredService<ILotOwnershipService>());
            return await handler.Handle(new ConfigureReservationRatioPolicyCommand(lotId, 30, ownerId), token);
        });

        var task2 = Task.Run(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = new ConfigureReservationRatioPolicyCommandHandler(
                scope.ServiceProvider.GetRequiredService<IReservationRatioPolicyRepository>(),
                scope.ServiceProvider.GetRequiredService<ILotOwnershipService>());
            return await handler.Handle(new ConfigureReservationRatioPolicyCommand(lotId, 70, ownerId), token);
        });

        var results = await Task.WhenAll(task1, task2);
        Assert.All(results, r => Assert.True(r.IsSuccess));

        await using var verifyScope = provider.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        var count = await db.ReservationRatioPolicies.CountAsync(p => p.LotId == lotId, token);
        Assert.Equal(1, count);
    }

    private async Task<ServiceProvider> NewProviderAsync(CancellationToken token)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(token);
        return provider;
    }

    private static async Task<Guid> InsertLotAsync(ServiceProvider provider, string approvalStatus, CancellationToken token)
    {
        var id = Guid.NewGuid();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({id}, 'Ratio Policy Lot', '123 Ratio Street', 10.77, 106.70, 'Outdoor', {approvalStatus})
            """,
            token);
        return id;
    }

    private static async Task AssignOwnerAsync(
        ServiceProvider provider,
        Guid lotId,
        Guid accountId,
        CancellationToken token)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
        db.ParkingLotAssignments.Add(ParkingLotAssignment.Create(lotId, accountId, ParkingLotMemberRole.Owner, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(token);
    }
}
