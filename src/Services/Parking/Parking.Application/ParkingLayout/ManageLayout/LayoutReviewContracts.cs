using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;

public sealed record LayoutReviewSubmission(Guid RequestId, Guid LotId, int ExpectedVersion, string Status, DateTimeOffset RequestedAtUtc);
public sealed record LayoutReviewRequest(Guid RequestId, Guid LotId, Guid RequestedBy, int ExpectedVersion,
    DateTimeOffset RequestedAtUtc, IReadOnlyList<LayoutFloorInput> ProposedFloors);

public sealed record SaveLayoutOutcome(Error? Error = null, LayoutReviewSubmission? PendingReview = null);
public sealed record ReviewLayoutOutcome(Error? Error = null, LayoutReviewSubmission? Review = null);
public sealed record ReviewedLayoutChange(LayoutReviewSubmission Request, ParkingLotLayout? ActiveLayout,
    IReadOnlyList<ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage.CapacityWarning> Warnings);

public static class LayoutReviewErrors
{
    public static readonly Error NotFound = new("parking.layout_review.not_found", "The layout change request was not found.");
    public static readonly Error AlreadyReviewed = new("parking.layout_review.already_reviewed", "This layout request is no longer Pending.");
    public static readonly Error PendingAlreadyExists = new("parking.layout_review.pending_exists", "Only one Pending layout change request is allowed per lot.");
    public static readonly Error LotNotApproved = new("parking.layout_review.lot_not_approved", "Only Approved parking lots use the layout review process.");
    public static readonly Error InvalidDecision = new("parking.layout_review.invalid_decision", "Rejection requires a reason of 1-500 characters; approval must omit it.");
    public static readonly Error ConcurrencyConflict = new("parking.layout_review.conflict", "The layout review was modified concurrently. Retry after reloading.");
}
