using Microsoft.Extensions.DependencyInjection;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.WebAPI.Security;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Interfaces;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotMember;
using ParkingSystem.Parking.Application.ParkingLots.AssignLotStaff;
using ParkingSystem.Parking.Application.ParkingLots.ListLotAssignments;
using ParkingSystem.Parking.Application.ParkingLots.RemoveLotMember;
using ParkingSystem.Parking.Application.ParkingLots.RemoveLotStaff;
using ParkingSystem.Parking.Domain.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class OwnerStaffAssignmentTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid OwnerId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid OtherOwnerId = Guid.Parse("90000000-0000-0000-0000-000000000009");
    private static readonly Guid StaffId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validator_rejects_empty_fields_or_invalid_email()
    {
        var validator = new AssignLotStaffByEmailCommandValidator();

        Assert.False(validator.Validate(new AssignLotStaffByEmailCommand(Guid.Empty, "staff@test.com", OwnerId)).IsValid);
        Assert.False(validator.Validate(new AssignLotStaffByEmailCommand(LotId, "", OwnerId)).IsValid);
        Assert.False(validator.Validate(new AssignLotStaffByEmailCommand(LotId, "not-an-email", OwnerId)).IsValid);
        Assert.False(validator.Validate(new AssignLotStaffByEmailCommand(LotId, "staff@test.com", Guid.Empty)).IsValid);
        Assert.True(validator.Validate(new AssignLotStaffByEmailCommand(LotId, "staff@parkwise.vn", OwnerId)).IsValid);
    }

    [Fact]
    public async Task AssignStaff_returns_403_when_caller_is_not_the_lot_owner()
    {
        var ownership = new FakeLotOwnership(isOwner: false, lotExists: true);
        var assignments = new FakeAssignmentRepository();
        var staffLookup = new FakeStaffLookup(isFound: true, StaffId);
        var audit = new FakeAuditLog();
        var clock = new FixedClock(Now);

        var handler = new AssignLotStaffByEmailCommandHandler(ownership, assignments, staffLookup, audit, clock);

        var result = await handler.Handle(
            new AssignLotStaffByEmailCommand(LotId, "staff@parkwise.vn", OtherOwnerId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssignLotStaffErrors.NotLotOwner.Code, result.Error!.Code);
        Assert.Equal("parking.not_lot_owner", result.Error.Code);
        Assert.Empty(assignments.Items);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task AssignStaff_returns_404_when_lot_does_not_exist()
    {
        var ownership = new FakeLotOwnership(isOwner: true, lotExists: false);
        var assignments = new FakeAssignmentRepository(lotExists: false);
        var staffLookup = new FakeStaffLookup(isFound: true, StaffId);
        var audit = new FakeAuditLog();
        var clock = new FixedClock(Now);

        var handler = new AssignLotStaffByEmailCommandHandler(ownership, assignments, staffLookup, audit, clock);

        var result = await handler.Handle(
            new AssignLotStaffByEmailCommand(LotId, "staff@parkwise.vn", OwnerId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssignLotStaffErrors.LotNotFound.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("customer@parkwise.vn")]
    [InlineData("other_owner@parkwise.vn")]
    [InlineData("suspended_staff@parkwise.vn")]
    [InlineData("non_existent@parkwise.vn")]
    public async Task AssignStaff_returns_neutral_404_error_when_email_is_not_active_staff(string email)
    {
        // Identity returns IsFound = false for all non-staff/inactive/missing accounts (anti-enumeration)
        var ownership = new FakeLotOwnership(isOwner: true, lotExists: true);
        var assignments = new FakeAssignmentRepository(lotExists: true);
        var staffLookup = new FakeStaffLookup(isFound: false, Guid.Empty);
        var audit = new FakeAuditLog();
        var clock = new FixedClock(Now);

        var handler = new AssignLotStaffByEmailCommandHandler(ownership, assignments, staffLookup, audit, clock);

        var result = await handler.Handle(
            new AssignLotStaffByEmailCommand(LotId, email, OwnerId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssignLotStaffErrors.StaffNotFound.Code, result.Error!.Code);
        Assert.Equal("parking.staff.not_found", result.Error.Code);
        Assert.Equal("Không tìm thấy tài khoản nhân viên hợp lệ với email đã cung cấp.", result.Error.Message);
        Assert.Empty(assignments.Items);
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task AssignStaff_successfully_assigns_new_staff_and_records_audit_log_in_same_transaction()
    {
        var ownership = new FakeLotOwnership(isOwner: true, lotExists: true);
        var assignments = new FakeAssignmentRepository(lotExists: true);
        var staffLookup = new FakeStaffLookup(isFound: true, StaffId);
        var audit = new FakeAuditLog();
        var clock = new FixedClock(Now);

        var handler = new AssignLotStaffByEmailCommandHandler(ownership, assignments, staffLookup, audit, clock);

        var result = await handler.Handle(
            new AssignLotStaffByEmailCommand(LotId, "staff.active@parkwise.vn", OwnerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(LotId, result.Value!.LotId);
        Assert.Equal(StaffId, result.Value.AccountId);
        Assert.Equal("Staff", result.Value.Role);
        Assert.Equal(Now, result.Value.AssignedAtUtc);

        // Verify assignment saved
        var saved = Assert.Single(assignments.Items);
        Assert.Equal(LotId, saved.LotId);
        Assert.Equal(StaffId, saved.AccountId);
        Assert.Equal(ParkingLotMemberRole.Staff, saved.Role);

        // Verify audit log recorded in same transaction
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.LotStaffAssigned, entry.Action);
        Assert.Equal(OwnerId, entry.ActorUserId);
        Assert.Equal(LotId, entry.LotId);
        Assert.Equal(StaffId.ToString(), entry.TargetId);
        Assert.True(assignments.SavedChanges);
    }

    [Fact]
    public async Task AssignStaff_is_idempotent_when_staff_is_already_assigned()
    {
        var existingAssignment = ParkingLotAssignment.Create(LotId, StaffId, ParkingLotMemberRole.Staff, OwnerId, Now.AddDays(-1));
        var ownership = new FakeLotOwnership(isOwner: true, lotExists: true);
        var assignments = new FakeAssignmentRepository([existingAssignment], lotExists: true);
        var staffLookup = new FakeStaffLookup(isFound: true, StaffId);
        var audit = new FakeAuditLog();
        var clock = new FixedClock(Now);

        var handler = new AssignLotStaffByEmailCommandHandler(ownership, assignments, staffLookup, audit, clock);

        // Call assignment again with same staff email
        var result = await handler.Handle(
            new AssignLotStaffByEmailCommand(LotId, "staff.already@parkwise.vn", OwnerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(StaffId, result.Value!.AccountId);
        Assert.Equal("Staff", result.Value.Role);
        Assert.Equal(Now.AddDays(-1), result.Value.AssignedAtUtc);

        // Invariant: does NOT create a duplicate record
        Assert.Single(assignments.Items);

        // Invariant: does NOT re-record audit log or trigger unneeded write
        Assert.Empty(audit.Entries);
    }

    [Fact]
    public async Task RemoveLotStaff_returns_403_when_caller_is_not_the_lot_owner()
    {
        var existing = ParkingLotAssignment.Create(LotId, StaffId, ParkingLotMemberRole.Staff, OwnerId, Now);
        var ownership = new FakeLotOwnership(isOwner: false, lotExists: true);
        var assignments = new FakeAssignmentRepository([existing], lotExists: true);
        var audit = new FakeAuditLog();
        var clock = new FixedClock(Now);

        var handler = new RemoveLotStaffByOwnerCommandHandler(ownership, assignments, audit, clock);

        var result = await handler.Handle(new RemoveLotStaffByOwnerCommand(LotId, StaffId, OtherOwnerId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AssignLotStaffErrors.NotLotOwner.Code, result.Error!.Code);
        Assert.Single(assignments.Items);
    }

    [Fact]
    public async Task RemoveLotStaff_successfully_removes_staff_and_records_audit()
    {
        var existing = ParkingLotAssignment.Create(LotId, StaffId, ParkingLotMemberRole.Staff, OwnerId, Now);
        var ownership = new FakeLotOwnership(isOwner: true, lotExists: true);
        var assignments = new FakeAssignmentRepository([existing], lotExists: true);
        var audit = new FakeAuditLog();
        var clock = new FixedClock(Now);

        var handler = new RemoveLotStaffByOwnerCommandHandler(ownership, assignments, audit, clock);

        var result = await handler.Handle(new RemoveLotStaffByOwnerCommand(LotId, StaffId, OwnerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(assignments.Items);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(AuditActions.LotStaffRemoved, entry.Action);
        Assert.Equal(StaffId.ToString(), entry.TargetId);
    }

    [Fact]
    public async Task RemoveLotStaff_refuses_to_remove_owner_assignment()
    {
        var ownerAssignment = ParkingLotAssignment.Create(LotId, OtherOwnerId, ParkingLotMemberRole.Owner, OwnerId, Now);
        var ownership = new FakeLotOwnership(isOwner: true, lotExists: true);
        var assignments = new FakeAssignmentRepository([ownerAssignment], lotExists: true);
        var audit = new FakeAuditLog();
        var clock = new FixedClock(Now);

        var handler = new RemoveLotStaffByOwnerCommandHandler(ownership, assignments, audit, clock);

        var result = await handler.Handle(new RemoveLotStaffByOwnerCommand(LotId, OtherOwnerId, OwnerId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(LotMemberErrors.MemberNotFound.Code, result.Error!.Code);
        Assert.Single(assignments.Items);
    }

    [Fact]
    public async Task ListOwnerLotAssignments_returns_assignments_when_owner_valid()
    {
        var staff1 = ParkingLotAssignment.Create(LotId, StaffId, ParkingLotMemberRole.Staff, OwnerId, Now);
        var ownership = new FakeLotOwnership(isOwner: true, lotExists: true);
        var assignments = new FakeAssignmentRepository([staff1], lotExists: true);

        var handler = new ListOwnerLotAssignmentsQueryHandler(ownership, assignments);

        var result = await handler.Handle(new ListOwnerLotAssignmentsQuery(LotId, OwnerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!);
        Assert.Equal(StaffId, item.AccountId);
        Assert.Equal("Staff", item.Role);
    }

    [Fact]
    public void OwnerStaffAssignmentRateLimiting_configures_expected_policy_and_limits()
    {
        Assert.Equal("OwnerStaffAssignmentRateLimitPolicy", ParkingSystem.Parking.WebAPI.Security.OwnerStaffAssignmentRateLimiting.PolicyName);
        Assert.Equal(10, ParkingSystem.Parking.WebAPI.Security.OwnerStaffAssignmentRateLimiting.DefaultPermitsPerMinute);
        Assert.Equal("parking.rate_limited", ParkingSystem.Parking.WebAPI.Security.OwnerStaffAssignmentRateLimiting.ErrorCode);

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddOwnerStaffAssignmentRateLimiting();
        var provider = services.BuildServiceProvider();

        var rateLimiterOptions = provider.GetService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>>();
        Assert.NotNull(rateLimiterOptions);
    }

    private sealed class FakeLotOwnership(bool isOwner, bool lotExists) : ILotOwnershipService
    {
        public Task<bool> LotExistsAsync(Guid lotId) => Task.FromResult(lotExists);
        public Task<bool> IsLotOwnerAsync(Guid lotId, Guid callerId) => Task.FromResult(isOwner);
        public Task<bool> IsLotApprovedAsync(Guid lotId) => Task.FromResult(true);
    }

    private sealed class FakeAssignmentRepository : IParkingLotAssignmentRepository
    {
        private readonly bool _lotExists;
        public List<ParkingLotAssignment> Items { get; }
        public bool SavedChanges { get; private set; }

        public FakeAssignmentRepository(IEnumerable<ParkingLotAssignment>? initial = null, bool lotExists = true)
        {
            Items = initial != null ? [..initial] : [];
            _lotExists = lotExists;
        }

        public Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken) => Task.FromResult(_lotExists);

        public Task<ParkingLotAssignment?> FindAsync(Guid lotId, Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.FirstOrDefault(a => a.LotId == lotId && a.AccountId == accountId));

        public Task<IReadOnlyList<ParkingLotAssignment>> ListByLotAsync(Guid lotId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ParkingLotAssignment>>(Items.Where(a => a.LotId == lotId).ToList());

        public Task<IReadOnlyList<ParkingSystem.Parking.Application.ParkingLots.ListStaffLots.StaffLot>> ListApprovedLotsOfMemberAsync(Guid accountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ParkingSystem.Parking.Application.ParkingLots.ListStaffLots.StaffLot>>([]);

        public void Add(ParkingLotAssignment assignment) => Items.Add(assignment);

        public void Remove(ParkingLotAssignment assignment) => Items.Remove(assignment);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SavedChanges = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStaffLookup(bool isFound, Guid accountId) : IStaffAccountLookup
    {
        public Task<(bool IsFound, Guid AccountId)> LookupStaffByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult((isFound, accountId));
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];
        public void Record(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
