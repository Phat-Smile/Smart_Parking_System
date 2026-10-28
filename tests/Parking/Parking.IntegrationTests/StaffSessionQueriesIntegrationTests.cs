using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingSessions;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence;
using ParkingSystem.Testing;
using Xunit;

namespace ParkingSystem.Tests.Parking.IntegrationTests;

// Lead task S2 (2026-10-27) on real PostgreSQL: the read queries behind the staff lot list, the admin assignment list
// and the lot session page.
public sealed class StaffSessionQueriesIntegrationTests(PostgreSqlContainerFixture postgres) : IClassFixture<PostgreSqlContainerFixture>
{
    private static readonly Guid AdminId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 27, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Staff_lots_are_only_the_approved_lots_the_account_is_assigned_to_sorted_by_name()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var staffId = Guid.NewGuid();
        var lotB = await CreateLotAsync(provider, "B Lot", "Approved", cancellationToken);
        var lotA = await CreateLotAsync(provider, "A Lot", "Approved", cancellationToken);
        var pending = await CreateLotAsync(provider, "C Pending", "Pending", cancellationToken);
        var notAssigned = await CreateLotAsync(provider, "D Other", "Approved", cancellationToken);
        await AssignAsync(provider, lotB, staffId, ParkingLotMemberRole.Staff, BaseTime, cancellationToken);
        await AssignAsync(provider, lotA, staffId, ParkingLotMemberRole.Owner, BaseTime, cancellationToken);
        await AssignAsync(provider, pending, staffId, ParkingLotMemberRole.Staff, BaseTime, cancellationToken);
        await AssignAsync(provider, notAssigned, Guid.NewGuid(), ParkingLotMemberRole.Staff, BaseTime, cancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var lots = await scope.ServiceProvider.GetRequiredService<IParkingLotAssignmentRepository>()
            .ListApprovedLotsOfMemberAsync(staffId, cancellationToken);

        Assert.Equal([lotA, lotB], lots.Select(lot => lot.Id));
        Assert.Equal("Outdoor", lots[0].Type);
        Assert.Equal("1 Demo Street", lots[0].Address);
    }

    [Fact]
    public async Task Lot_assignments_are_listed_oldest_first()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var lot = await CreateLotAsync(provider, "Assignments", "Approved", cancellationToken);
        var staffId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await AssignAsync(provider, lot, staffId, ParkingLotMemberRole.Staff, BaseTime.AddHours(1), cancellationToken);
        await AssignAsync(provider, lot, ownerId, ParkingLotMemberRole.Owner, BaseTime, cancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var members = await scope.ServiceProvider.GetRequiredService<IParkingLotAssignmentRepository>()
            .ListByLotAsync(lot, cancellationToken);

        Assert.Equal([ownerId, staffId], members.Select(member => member.AccountId));
        Assert.Equal(ParkingLotMemberRole.Owner, members[0].Role);
    }

    [Fact]
    public async Task Lot_sessions_page_newest_first_with_status_filter_and_total_count()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var provider = await CreateProviderAsync(cancellationToken);
        var lot = await CreateLotAsync(provider, "Sessions", "Approved", cancellationToken);
        var otherLot = await CreateLotAsync(provider, "Other Sessions", "Approved", cancellationToken);
        var first = await OpenSessionAsync(provider, lot, "51A-000.01", BaseTime, cancellationToken);
        var second = await OpenSessionAsync(provider, lot, "51A-000.02", BaseTime.AddMinutes(1), cancellationToken);
        var third = await OpenSessionAsync(provider, lot, "51A-000.03", BaseTime.AddMinutes(2), cancellationToken);
        await OpenSessionAsync(provider, otherLot, "51A-000.04", BaseTime.AddMinutes(3), cancellationToken);
        await CancelAsync(provider, second, cancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();

        var page1 = await sessions.ListByLotAsync(lot, status: null, skip: 0, take: 2, cancellationToken);
        var page2 = await sessions.ListByLotAsync(lot, status: null, skip: 2, take: 2, cancellationToken);
        var active = await sessions.ListByLotAsync(lot, ParkingSessionStatus.Active, skip: 0, take: 20, cancellationToken);

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal([third, second], page1.Items.Select(session => session.Id));
        Assert.Equal([first], page2.Items.Select(session => session.Id));
        Assert.Equal(2, active.TotalCount);
        Assert.Equal([third, first], active.Items.Select(session => session.Id));
    }

    private async Task<ServiceProvider> CreateProviderAsync(CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        services.AddPersistence(PostgreSqlTestConfiguration.Create(postgres.ConnectionString));
        var provider = services.BuildServiceProvider(validateScopes: true);

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.MigrateAsync(cancellationToken);
        return provider;
    }

    private static async Task<Guid> CreateLotAsync(ServiceProvider provider, string name, string approvalStatus, CancellationToken cancellationToken)
    {
        var lotId = Guid.NewGuid();
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO parking.parking_lots (id, name, address, latitude, longitude, type, approval_status)
            VALUES ({lotId}, {name}, '1 Demo Street', 10.776530, 106.700981, 'Outdoor', {approvalStatus})
            """,
            cancellationToken);
        return lotId;
    }

    private static async Task AssignAsync(
        ServiceProvider provider, Guid lotId, Guid accountId, ParkingLotMemberRole role, DateTimeOffset assignedAt, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var assignments = scope.ServiceProvider.GetRequiredService<IParkingLotAssignmentRepository>();
        assignments.Add(ParkingLotAssignment.Create(lotId, accountId, role, AdminId, assignedAt));
        await assignments.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Guid> OpenSessionAsync(ServiceProvider provider, Guid lotId, string plate, DateTimeOffset entryTime, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();
        var session = ParkingSession.Open(Guid.NewGuid(), lotId, plate, VehicleType.Car, entryTime, Guid.NewGuid().ToString("N"), null, null);
        sessions.Add(session);
        Assert.True(await sessions.TrySaveChangesAsync(cancellationToken));
        return session.Id;
    }

    private static async Task CancelAsync(ServiceProvider provider, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IParkingSessionRepository>();
        var session = await sessions.FindByIdAsync(sessionId, cancellationToken);
        Assert.True(session!.Cancel(AdminId, "Vehicle did not enter", BaseTime.AddHours(1)));
        Assert.True(await sessions.TrySaveChangesAsync(cancellationToken));
    }
}
