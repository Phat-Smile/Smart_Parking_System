using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotStaff;
using ParkingSystem.Parking.Application.ParkingLots.ListLotAssignments;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.WebAPI.Grpc;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class OwnerStaffEnrichmentTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OwnerId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid Staff1Id = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid Staff2Id = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handler_enriches_assignments_in_a_single_batch_call_without_calling_n_times()
    {
        var staff1 = ParkingLotAssignment.Create(LotId, Staff1Id, ParkingLotMemberRole.Staff, OwnerId, Now);
        var staff2 = ParkingLotAssignment.Create(LotId, Staff2Id, ParkingLotMemberRole.Staff, OwnerId, Now);
        var ownership = new FakeLotOwnership(isOwner: true, lotExists: true);
        var assignments = new FakeAssignmentRepository([staff1, staff2], lotExists: true);

        var enricher = new FakeStaffEnricher(new Dictionary<Guid, (string, string)>
        {
            [Staff1Id] = ("Nguyen Van A", "n***@parkwise.vn"),
            [Staff2Id] = ("Tran Thi B", "t***@parkwise.vn")
        });

        var handler = new ListOwnerLotAssignmentsQueryHandler(ownership, assignments, enricher);
        var result = await handler.Handle(new ListOwnerLotAssignmentsQuery(LotId, OwnerId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var items = result.Value!;
        Assert.Equal(2, items.Count);

        // Verify single batch call
        Assert.Equal(1, enricher.BatchCallCount);
        Assert.Equal(2, enricher.LastRequestedIds?.Count);

        var item1 = items.Single(i => i.AccountId == Staff1Id);
        Assert.Equal("Nguyen Van A", item1.DisplayName);
        Assert.Equal("n***@parkwise.vn", item1.MaskedEmail);

        var item2 = items.Single(i => i.AccountId == Staff2Id);
        Assert.Equal("Tran Thi B", item2.DisplayName);
        Assert.Equal("t***@parkwise.vn", item2.MaskedEmail);
    }

    [Fact]
    public async Task Handler_returns_assignments_with_null_details_when_enricher_is_null_or_fails()
    {
        var staff1 = ParkingLotAssignment.Create(LotId, Staff1Id, ParkingLotMemberRole.Staff, OwnerId, Now);
        var ownership = new FakeLotOwnership(isOwner: true, lotExists: true);
        var assignments = new FakeAssignmentRepository([staff1], lotExists: true);

        // Without enricher
        var handler = new ListOwnerLotAssignmentsQueryHandler(ownership, assignments, staffEnricher: null);
        var result = await handler.Handle(new ListOwnerLotAssignmentsQuery(LotId, OwnerId), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!);
        Assert.Equal(Staff1Id, item.AccountId);
        Assert.Null(item.DisplayName);
        Assert.Null(item.MaskedEmail);
    }

    [Fact]
    public async Task Handler_does_not_call_enricher_when_caller_is_not_lot_owner()
    {
        var staff1 = ParkingLotAssignment.Create(LotId, Staff1Id, ParkingLotMemberRole.Staff, OwnerId, Now);
        var ownership = new FakeLotOwnership(isOwner: false, lotExists: true);
        var assignments = new FakeAssignmentRepository([staff1], lotExists: true);
        var enricher = new FakeStaffEnricher([]);

        var handler = new ListOwnerLotAssignmentsQueryHandler(ownership, assignments, enricher);
        var result = await handler.Handle(new ListOwnerLotAssignmentsQuery(LotId, OwnerId), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssignLotStaffErrors.NotLotOwner.Code, result.Error!.Code);
        Assert.Equal(0, enricher.BatchCallCount);
    }

    [Theory]
    [InlineData("user@domain.com", "u***@domain.com")]
    [InlineData("nguyen.van.a@gmail.com", "n***@gmail.com")]
    [InlineData("admin@parkwise.vn", "a***@parkwise.vn")]
    [InlineData("", "***@***")]
    public void IdentityStaffGrpcClient_MaskEmail_formats_masked_email_properly(string input, string expected)
    {
        var result = IdentityStaffGrpcClient.MaskEmail(input);
        Assert.Equal(expected, result);
    }

    private sealed class FakeLotOwnership(bool isOwner, bool lotExists) : ILotOwnershipService
    {
        public Task<bool> LotExistsAsync(Guid lotId) => Task.FromResult(lotExists);
        public Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId) => Task.FromResult(isOwner);
        public Task<bool> IsLotApprovedAsync(Guid lotId) => Task.FromResult(true);
    }

    private sealed class FakeAssignmentRepository(IEnumerable<ParkingLotAssignment>? initial = null, bool lotExists = true) : IParkingLotAssignmentRepository
    {
        public List<ParkingLotAssignment> Items { get; } = initial != null ? [..initial] : [];

        public Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(lotExists);
        public Task<ParkingLotAssignment?> FindAsync(Guid lotId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(a => a.LotId == lotId && a.AccountId == accountId));
        public Task<IReadOnlyList<ParkingLotAssignment>> ListByLotAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ParkingLotAssignment>>(Items.Where(a => a.LotId == lotId).ToList());
        public Task<IReadOnlyList<ParkingSystem.Parking.Application.ParkingLots.ListStaffLots.StaffLot>> ListApprovedLotsOfMemberAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ParkingSystem.Parking.Application.ParkingLots.ListStaffLots.StaffLot>>([]);
        public void Add(ParkingLotAssignment assignment) => Items.Add(assignment);
        public void Remove(ParkingLotAssignment assignment) => Items.Remove(assignment);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeStaffEnricher(IReadOnlyDictionary<Guid, (string DisplayName, string MaskedEmail)> lookup) : IStaffAccountEnricher
    {
        public int BatchCallCount { get; private set; }
        public IReadOnlyList<Guid>? LastRequestedIds { get; private set; }

        public Task<IReadOnlyDictionary<Guid, (string DisplayName, string MaskedEmail)>> BatchLookupAccountsAsync(
            IReadOnlyList<Guid> accountIds,
            CancellationToken cancellationToken)
        {
            BatchCallCount++;
            LastRequestedIds = accountIds;
            return Task.FromResult(lookup);
        }
    }
}
