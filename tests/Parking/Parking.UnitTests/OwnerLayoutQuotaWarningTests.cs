using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class OwnerLayoutQuotaWarningTests
{
    [Fact]
    public async Task Save_succeeds_and_returns_shortage_warning()
    {
        var lotId = Guid.NewGuid();
        var repo = new FakeRepository(lotId);
        var check = new FakeCheck(new CapacityWarning(
            HeldQuotaShortageCheck.WarningCode, "Available car slots (1) are lower than the reservations held (3).", 1, 3));

        var result = await new SaveParkingLayoutCommandHandler(repo, check)
            .Handle(new SaveParkingLayoutCommand(lotId, Guid.NewGuid(), 0, ValidLayout()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var warning = Assert.Single(result.Value!.Warnings);
        Assert.Equal("parking.capacity.below_held_quota", warning.Code);
        Assert.Equal(1, warning.AvailableSlots);
        Assert.Equal(3, warning.HeldQuota);
        Assert.Equal(1, repo.SaveCalls);
        Assert.Equal(1, check.Calls);
    }

    [Fact]
    public async Task Save_without_shortage_returns_an_empty_warning_array()
    {
        var lotId = Guid.NewGuid();
        var repo = new FakeRepository(lotId);
        var check = new FakeCheck(null);

        var result = await new SaveParkingLayoutCommandHandler(repo, check)
            .Handle(new SaveParkingLayoutCommand(lotId, Guid.NewGuid(), 0, ValidLayout()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Warnings);
        Assert.Equal(1, repo.SaveCalls);
        Assert.Equal(1, check.Calls);
    }

    [Fact]
    public async Task Save_conflict_does_not_query_quota_and_returns_the_original_error()
    {
        var lotId = Guid.NewGuid();
        var repo = new FakeRepository(lotId) { SaveError = ParkingLayoutErrors.StaleVersion };
        var check = new FakeCheck(new CapacityWarning("warning", "warning", 0, 1));

        var result = await new SaveParkingLayoutCommandHandler(repo, check)
            .Handle(new SaveParkingLayoutCommand(lotId, Guid.NewGuid(), 0, ValidLayout()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ParkingLayoutErrors.StaleVersion, result.Error);
        Assert.Equal(1, repo.SaveCalls);
        Assert.Equal(0, check.Calls);
    }

    [Fact]
    public async Task Get_layout_keeps_empty_warnings_by_default()
    {
        var lotId = Guid.NewGuid();
        var result = await new GetParkingLayoutQueryHandler(new FakeRepository(lotId))
            .Handle(new GetParkingLayoutQuery(lotId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Warnings);
    }

    [Fact]
    public async Task Approved_lot_submission_reports_pending_without_computing_quota()
    {
        var lotId = Guid.NewGuid();
        var submission = new LayoutReviewSubmission(Guid.NewGuid(), lotId, 0, "Pending", DateTimeOffset.UtcNow);
        var repo = new FakeRepository(lotId) { PendingReview = submission };
        var check = new FakeCheck(new CapacityWarning("x", "x", 1, 2));
        var result = await new SaveParkingLayoutCommandHandler(repo, check)
            .Handle(new SaveParkingLayoutCommand(lotId, Guid.NewGuid(), 0, ValidLayout()), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(submission.RequestId, result.Value!.PendingReview?.RequestId);
        Assert.Empty(result.Value.Warnings);
        Assert.Equal(0, check.Calls);
    }

    [Fact]
    public async Task Admin_approval_checks_quota_only_after_review_succeeds()
    {
        var lotId = Guid.NewGuid();
        var review = new LayoutReviewSubmission(Guid.NewGuid(), lotId, 1, "Approved", DateTimeOffset.UtcNow);
        var repo = new FakeRepository(lotId) { ReviewResult = new ReviewLayoutOutcome(Review: review) };
        var check = new FakeCheck(new CapacityWarning("x", "x", 1, 2));
        var result = await new ReviewLayoutChangeCommandHandler(repo, check)
            .Handle(new ReviewLayoutChangeCommand(lotId, review.RequestId, Guid.NewGuid(), true, null), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.ActiveLayout);
        Assert.Single(result.Value.Warnings);
        Assert.Equal(1, check.Calls);
    }

    [Fact]
    public async Task Admin_rejection_keeps_active_layout_unread_and_does_not_check_quota()
    {
        var lotId = Guid.NewGuid();
        var review = new LayoutReviewSubmission(Guid.NewGuid(), lotId, 1, "Rejected", DateTimeOffset.UtcNow);
        var repo = new FakeRepository(lotId) { ReviewResult = new ReviewLayoutOutcome(Review: review) };
        var check = new FakeCheck(null);
        var result = await new ReviewLayoutChangeCommandHandler(repo, check)
            .Handle(new ReviewLayoutChangeCommand(lotId, review.RequestId, Guid.NewGuid(), false, "Bad layout"), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.ActiveLayout);
        Assert.Empty(result.Value.Warnings);
        Assert.Equal(0, check.Calls);
    }

    [Fact]
    public async Task Invalid_review_reason_never_reaches_repository()
    {
        var lotId = Guid.NewGuid();
        var repo = new FakeRepository(lotId);
        var check = new FakeCheck(null);
        var result = await new ReviewLayoutChangeCommandHandler(repo, check)
            .Handle(new ReviewLayoutChangeCommand(lotId, Guid.NewGuid(), Guid.NewGuid(), false, " "), CancellationToken.None);
        Assert.Equal(LayoutReviewErrors.InvalidDecision, result.Error);
        Assert.Equal(0, repo.ReviewCalls);
    }

    private static LayoutFloorInput[] ValidLayout() =>
    [
        new LayoutFloorInput(Guid.NewGuid(), "Ground",
        [
            new LayoutZoneInput(Guid.NewGuid(), "C1", "Car", 2, false, [])
        ])
    ];

    private sealed class FakeRepository(Guid lotId) : IManageParkingLayoutRepository
    {
        public int SaveCalls { get; private set; }
        public Error? SaveError { get; init; }
        public LayoutReviewSubmission? PendingReview { get; init; }
        public ReviewLayoutOutcome ReviewResult { get; init; } = new(LayoutReviewErrors.NotFound);
        public int ReviewCalls { get; private set; }

        public Task<bool> LotExistsAsync(Guid requestedLotId, CancellationToken cancellationToken) =>
            Task.FromResult(requestedLotId == lotId);

        public Task<bool> IsOwnerAsync(Guid requestedLotId, Guid actorId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<ParkingLotLayout> ReadAsync(Guid requestedLotId, CancellationToken cancellationToken) =>
            Task.FromResult(new ParkingLotLayout(lotId, 1, []));

        public Task<SaveLayoutOutcome> SaveAsync(Guid requestedLotId, Guid actorId, int expectedVersion,
            IReadOnlyList<LayoutFloorInput> floors, CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(new SaveLayoutOutcome(SaveError, PendingReview));
        }

        public Task<IReadOnlyList<LayoutReviewRequest>> ListPendingReviewsAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LayoutReviewRequest>>([]);

        public Task<ReviewLayoutOutcome> ReviewAsync(Guid lot, Guid requestId, Guid adminId,
            bool approve, string? reason, CancellationToken ct)
        {
            ReviewCalls++;
            return Task.FromResult(ReviewResult);
        }
    }

    private sealed class FakeCheck(CapacityWarning? warning) : IHeldQuotaShortageCheck
    {
        public int Calls { get; private set; }

        public Task<CapacityWarning?> CheckAsync(Guid lotId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(warning);
        }
    }
}
