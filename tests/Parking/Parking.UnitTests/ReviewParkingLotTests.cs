using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Application.ParkingLots.ReviewParkingLot;
using ParkingSystem.Parking.Domain.ParkingLots;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ReviewParkingLotTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly ReviewParkingLotCommandValidator validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Rejection_requires_nonblank_reason(string? reason)
    {
        var result = validator.Validate(NewCommand(ParkingLotApprovalStatus.Rejected, reason));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReviewParkingLotCommand.Reason));
    }

    [Fact]
    public void Rejection_reason_cannot_exceed_database_limit()
    {
        var result = validator.Validate(NewCommand(ParkingLotApprovalStatus.Rejected, new string('a', ParkingLot.RejectionReasonMaxLength + 1)));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReviewParkingLotCommand.Reason));
    }

    [Fact]
    public void Approval_does_not_accept_rejection_reason()
    {
        var result = validator.Validate(NewCommand(ParkingLotApprovalStatus.Approved, "Unrelated reason"));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReviewParkingLotCommand.Reason));
    }

    [Fact]
    public void Valid_approve_and_reject_commands_pass()
    {
        Assert.True(validator.Validate(NewCommand(ParkingLotApprovalStatus.Approved, null)).IsValid);
        Assert.True(validator.Validate(NewCommand(ParkingLotApprovalStatus.Rejected, " Missing papers ")).IsValid);
    }

    [Fact]
    public void Pending_is_not_a_valid_review_decision()
    {
        var result = validator.Validate(NewCommand(ParkingLotApprovalStatus.Pending, null));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ReviewParkingLotCommand.Decision));
    }

    [Fact]
    public async Task Unknown_lot_returns_not_found_without_writing_audit()
    {
        var audit = new FakeAuditLog();
        var repository = new MissingLotRepository();
        var handler = new ReviewParkingLotCommandHandler(repository, audit, new FixedClock());

        var result = await handler.Handle(NewCommand(ParkingLotApprovalStatus.Approved, null), TestContext.Current.CancellationToken);

        Assert.Equal(ReviewParkingLotErrors.NotFound, result.Error);
        Assert.False(repository.SaveCalled);
        Assert.Empty(audit.Entries);
    }

    private static ReviewParkingLotCommand NewCommand(ParkingLotApprovalStatus decision, string? reason) =>
        new(Guid.NewGuid(), decision, reason, AdminId);

    private sealed class MissingLotRepository : IParkingLotApprovalRepository
    {
        public bool SaveCalled { get; private set; }
        public Task<ParkingLot?> FindByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<ParkingLot?>(null);
        public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalled = true;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];
        public void Record(AuditEntry entry) => Entries.Add(entry);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
    }
}
