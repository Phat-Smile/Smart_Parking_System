using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Common.Constants;

public static class RefundRequestErrors
{
    public static readonly Error WalletNotFound = new(
        "payment.refund_request.wallet_not_found",
        "Wallet not found for the customer.");

    public static readonly Error NotFound = new(
        "payment.refund_request.not_found",
        "Refund request not found.");

    public static readonly Error CustomerOnly = new(
        "payment.customer_only",
        "Only customers can create refund requests.");

    public static readonly Error ForbiddenRole = new(
        "payment.forbidden_role",
        "You do not have permission to perform this action.");

    public static readonly Error CreatorCannotReview = new(
        "payment.creator_cannot_review",
        "Creator of the refund request cannot be the reviewer.");

    public static readonly Error OwnerNotConfirmed = new(
        "payment.owner_not_confirmed",
        "Owner has not confirmed this refund request.");

    public static readonly Error InvalidStatus = new(
        "payment.invalid_status",
        "Refund request is not in the required status.");

    public static readonly Error AlreadyConfirmed = new(
        "payment.already_confirmed",
        "Refund request has already been confirmed by an owner.");

    public static readonly Error AlreadyReviewed = new(
        "payment.already_reviewed",
        "Refund request has already been reviewed.");

    public static readonly Error ConcurrencyConflict = new(
        "payment.concurrency_conflict",
        "The refund request was modified concurrently. Please refresh and try again.");

    public static readonly Error ForbiddenLotAccess = new(
        "payment.forbidden_lot_access",
        "You do not have permission for this parking lot.");

    public static readonly Error LotIdRequired = new(
        "payment.refund_request.lot_id_required",
        "Parking lot id is required for lot owners.");

    public static readonly Error ParkingServiceUnavailable = new(
        "payment.parking_service_unavailable",
        "Parking service is temporarily unavailable. Please try again.");

    public static readonly Error ReasonRequired = new(
        "payment.reason_required",
        "Reason is required.");

    public static readonly Error InvalidPage = new(
        "payment.invalid_page",
        "Page must be a whole number of 1 or more.");

    public static readonly Error InvalidPageSize = new(
        "payment.invalid_page_size",
        "Page size must be between 1 and 50.");
}
