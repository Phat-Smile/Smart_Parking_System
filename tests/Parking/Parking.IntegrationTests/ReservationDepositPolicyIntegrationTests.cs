using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.Common.Constants;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.Features.DepositPolicy.Commands.ConfigureDepositPolicy;
using ParkingSystem.Parking.Application.Features.DepositPolicy.Queries.GetDepositPolicy;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

public sealed class ReservationDepositPolicyIntegrationTests(PostgreSqlContainerFixture postgres)
    : IClassFixture<PostgreSqlContainerFixture>
{
    [Fact]
    public async Task Owner_can_configure_deposit_and_read_it_back()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);

        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(provider, "Approved", token);
        await AssignOwnerAsync(provider, lotId, ownerId, token);

        // Act 1: Configure deposit
        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new ConfigureDepositPolicyCommandHandler(
                services.GetRequiredService<IReservationDepositPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var result = await handler.Handle(
                new ConfigureDepositPolicyCommand(lotId, 25000m, ownerId),
                token);

            Assert.True(result.IsSuccess);
            Assert.Equal(25000m, result.Value!.HoldDepositVnd);
            Assert.True(result.Value.IsConfigured);
        }

        // Act 2: Read it back
        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var queryHandler = new GetDepositPolicyQueryHandler(
                services.GetRequiredService<IReservationDepositPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var getResult = await queryHandler.Handle(
                new GetDepositPolicyQuery(lotId, ownerId),
                token);

            Assert.True(getResult.IsSuccess);
            Assert.Equal(lotId, getResult.Value!.LotId);
            Assert.True(getResult.Value.IsConfigured);
            Assert.Equal(25000m, getResult.Value.HoldDepositVnd);
        }
    }

    [Fact]
    public async Task Non_owner_cannot_configure_another_owners_lot_and_database_remains_unchanged()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);

        var realOwnerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(provider, "Approved", token);
        await AssignOwnerAsync(provider, lotId, realOwnerId, token);

        // Pre-configure by real owner
        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new ConfigureDepositPolicyCommandHandler(
                services.GetRequiredService<IReservationDepositPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            await handler.Handle(new ConfigureDepositPolicyCommand(lotId, 10000m, realOwnerId), token);
        }

        // Attacker attempts to modify deposit
        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new ConfigureDepositPolicyCommandHandler(
                services.GetRequiredService<IReservationDepositPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var result = await handler.Handle(
                new ConfigureDepositPolicyCommand(lotId, 50000m, attackerId),
                token);

            Assert.False(result.IsSuccess);
            Assert.Equal(ParkingErrors.DepositPolicy.NotLotOwner.Code, result.Error!.Code);
        }

        // Verify database is unchanged
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ParkingDbContext>();
            var policy = await db.ReservationDepositPolicies.FirstOrDefaultAsync(p => p.LotId == lotId, token);
            Assert.NotNull(policy);
            Assert.Equal(10000m, policy.HoldDepositVnd);
        }
    }

    [Fact]
    public async Task Zero_VND_deposit_is_persisted_and_returned_correctly()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);

        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(provider, "Approved", token);
        await AssignOwnerAsync(provider, lotId, ownerId, token);

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var handler = new ConfigureDepositPolicyCommandHandler(
                services.GetRequiredService<IReservationDepositPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var result = await handler.Handle(new ConfigureDepositPolicyCommand(lotId, 0m, ownerId), token);
            Assert.True(result.IsSuccess);
            Assert.Equal(0m, result.Value!.HoldDepositVnd);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var queryHandler = new GetDepositPolicyQueryHandler(
                services.GetRequiredService<IReservationDepositPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var getResult = await queryHandler.Handle(new GetDepositPolicyQuery(lotId, ownerId), token);
            Assert.True(getResult.IsSuccess);
            Assert.True(getResult.Value!.IsConfigured);
            Assert.Equal(0m, getResult.Value.HoldDepositVnd);
        }
    }

    [Fact]
    public async Task Two_lots_keep_different_deposits_independently()
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
            var handler = new ConfigureDepositPolicyCommandHandler(
                services.GetRequiredService<IReservationDepositPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            await handler.Handle(new ConfigureDepositPolicyCommand(lot1, 15000m, owner1), token);
            await handler.Handle(new ConfigureDepositPolicyCommand(lot2, 35000m, owner2), token);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var queryHandler = new GetDepositPolicyQueryHandler(
                services.GetRequiredService<IReservationDepositPolicyRepository>(),
                services.GetRequiredService<ILotOwnershipService>());

            var res1 = await queryHandler.Handle(new GetDepositPolicyQuery(lot1, owner1), token);
            var res2 = await queryHandler.Handle(new GetDepositPolicyQuery(lot2, owner2), token);

            Assert.True(res1.IsSuccess);
            Assert.True(res2.IsSuccess);
            Assert.Equal(15000m, res1.Value!.HoldDepositVnd);
            Assert.Equal(35000m, res2.Value!.HoldDepositVnd);
        }
    }

    [Fact]
    public async Task Unconfigured_lot_returns_is_configured_false_and_default_20000_deposit()
    {
        var token = TestContext.Current.CancellationToken;
        await using var provider = await NewProviderAsync(token);

        var ownerId = Guid.NewGuid();
        var lotId = await InsertLotAsync(provider, "Approved", token);
        await AssignOwnerAsync(provider, lotId, ownerId, token);

        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queryHandler = new GetDepositPolicyQueryHandler(
            services.GetRequiredService<IReservationDepositPolicyRepository>(),
            services.GetRequiredService<ILotOwnershipService>());

        var result = await queryHandler.Handle(new GetDepositPolicyQuery(lotId, ownerId), token);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsConfigured);
        Assert.Equal(20000m, result.Value.HoldDepositVnd);
        Assert.Null(result.Value.UpdatedAtUtc);
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
            VALUES ({id}, 'Deposit Policy Lot', '123 Park Street', 10.77, 106.70, 'Outdoor', {approvalStatus})
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
