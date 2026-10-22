using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Domain.ParkingLots;

namespace ParkingSystem.Parking.Application.ParkingLots.ReviewParkingLot;

public sealed record ReviewParkingLotCommand(
    Guid ParkingLotId, ParkingLotApprovalStatus Decision, string? Reason, Guid ActorUserId)
    : IRequest<Result<ReviewedParkingLot>>;

public sealed record ReviewedParkingLot(Guid ParkingLotId, string ApprovalStatus, string? RejectionReason);

public static class ReviewParkingLotErrors
{
    public static readonly Error NotFound = new("parking.lot_not_found", "The parking lot does not exist.");
    public static readonly Error AlreadyReviewed = new("parking.lot_already_reviewed", "Only Pending parking lots can be reviewed.");
}

public sealed class ReviewParkingLotCommandValidator : AbstractValidator<ReviewParkingLotCommand>
{
    public ReviewParkingLotCommandValidator()
    {
        RuleFor(command => command.ParkingLotId).NotEmpty();
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.Decision)
            .Must(decision => decision is ParkingLotApprovalStatus.Approved or ParkingLotApprovalStatus.Rejected);
        RuleFor(command => command.Reason)
            .Must((command, reason) => command.Decision == ParkingLotApprovalStatus.Rejected
                ? !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= ParkingLot.RejectionReasonMaxLength
                : string.IsNullOrWhiteSpace(reason))
            .WithMessage($"A rejection requires a reason between 1 and {ParkingLot.RejectionReasonMaxLength} characters; approval must omit it.");
    }
}

public sealed class ReviewParkingLotCommandHandler(
    IParkingLotApprovalRepository parkingLots,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<ReviewParkingLotCommand, Result<ReviewedParkingLot>>
{
    public async Task<Result<ReviewedParkingLot>> Handle(ReviewParkingLotCommand request, CancellationToken cancellationToken)
    {
        var lot = await parkingLots.FindByIdAsync(request.ParkingLotId, cancellationToken);
        if (lot is null)
            return Result<ReviewedParkingLot>.Failure(ReviewParkingLotErrors.NotFound);

        var previousStatus = lot.ApprovalStatus;
        var changed = request.Decision switch
        {
            ParkingLotApprovalStatus.Approved => lot.Approve(),
            ParkingLotApprovalStatus.Rejected => lot.Reject(request.Reason!),
            _ => false
        };
        if (!changed)
            return Result<ReviewedParkingLot>.Failure(ReviewParkingLotErrors.AlreadyReviewed);

        auditLog.Record(new AuditEntry(
            request.Decision == ParkingLotApprovalStatus.Approved
                ? AuditActions.ParkingLotApproved
                : AuditActions.ParkingLotRejected,
            request.ActorUserId,
            lot.Id,
            nameof(ParkingLot),
            lot.Id.ToString(),
            lot.RejectionReason,
            clock.UtcNow,
            new Dictionary<string, string>
            {
                ["fromStatus"] = previousStatus.ToString(),
                ["toStatus"] = lot.ApprovalStatus.ToString()
            }));

        // The status change and audit log entry are persisted in one SaveChanges transaction.
        // Optimistic concurrency prevents a late second reviewer overwriting a decision.
        if (!await parkingLots.TrySaveChangesAsync(cancellationToken))
            return Result<ReviewedParkingLot>.Failure(ReviewParkingLotErrors.AlreadyReviewed);

        return Result<ReviewedParkingLot>.Success(new ReviewedParkingLot(
            lot.Id, lot.ApprovalStatus.ToString(), lot.RejectionReason));
    }
}
