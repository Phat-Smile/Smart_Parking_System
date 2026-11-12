using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.GetLotMembership;
using ParkingSystem.Parking.Application.ParkingLots.ListStaffLots;
using ParkingSystem.Parking.Domain.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class GetLotMembershipQueryTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherLotId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid AccountId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid AssignedBy = Guid.Parse("40000000-0000-0000-0000-000000000004");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Member_with_staff_role_returns_is_member_true_and_staff_role()
    {
        var assignment = ParkingLotAssignment.Create(LotId, AccountId, ParkingLotMemberRole.Staff, AssignedBy, Now);
        var repo = new FakeAssignmentRepository(assignment);
        var handler = new GetLotMembershipQueryHandler(repo);

        var result = await handler.Handle(new GetLotMembershipQuery(LotId, AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsMember);
        Assert.Equal("Staff", result.Value.Role);
    }

    [Fact]
    public async Task Member_with_owner_role_returns_is_member_true_and_owner_role()
    {
        var assignment = ParkingLotAssignment.Create(LotId, AccountId, ParkingLotMemberRole.Owner, AssignedBy, Now);
        var repo = new FakeAssignmentRepository(assignment);
        var handler = new GetLotMembershipQueryHandler(repo);

        var result = await handler.Handle(new GetLotMembershipQuery(LotId, AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsMember);
        Assert.Equal("Owner", result.Value.Role);
    }

    [Fact]
    public async Task Non_member_returns_is_member_false_and_empty_role()
    {
        var repo = new FakeAssignmentRepository(null);
        var handler = new GetLotMembershipQueryHandler(repo);

        var result = await handler.Handle(new GetLotMembershipQuery(LotId, AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsMember);
        Assert.Equal(string.Empty, result.Value.Role);
    }

    [Fact]
    public async Task Member_of_other_lot_is_not_member_of_this_lot()
    {
        var assignment = ParkingLotAssignment.Create(OtherLotId, AccountId, ParkingLotMemberRole.Staff, AssignedBy, Now);
        var repo = new FakeAssignmentRepository(assignment);
        var handler = new GetLotMembershipQueryHandler(repo);

        var result = await handler.Handle(new GetLotMembershipQuery(LotId, AccountId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsMember);
        Assert.Equal(string.Empty, result.Value.Role);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "30000000-0000-0000-0000-000000000003")]
    [InlineData("10000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000000")]
    public async Task Empty_ids_return_invalid_arguments_failure(string lotIdStr, string accountIdStr)
    {
        var repo = new FakeAssignmentRepository(null);
        var handler = new GetLotMembershipQueryHandler(repo);

        var result = await handler.Handle(
            new GetLotMembershipQuery(Guid.Parse(lotIdStr), Guid.Parse(accountIdStr)),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(GetLotMembershipErrors.InvalidArguments, result.Error);
    }

    private sealed class FakeAssignmentRepository(ParkingLotAssignment? assignment) : IParkingLotAssignmentRepository
    {
        public Task<ParkingLotAssignment?> FindAsync(Guid lotId, Guid accountId, CancellationToken cancellationToken)
        {
            if (assignment != null && assignment.LotId == lotId && assignment.AccountId == accountId)
            {
                return Task.FromResult<ParkingLotAssignment?>(assignment);
            }
            return Task.FromResult<ParkingLotAssignment?>(null);
        }

        public Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<StaffLot>> ListApprovedLotsOfMemberAsync(Guid accountId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ParkingLotAssignment>> ListByLotAsync(Guid lotId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(ParkingLotAssignment a) => throw new NotSupportedException();
        public void Remove(ParkingLotAssignment a) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
