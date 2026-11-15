using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.VerifyParkingSessionExit;

// US-STAFF-004 (BR-11.d, MF-14): the plate or QR at the exit gate did not match, so staff check the vehicle in person.
// The check stands in for the plate and QR match only; it never skips payment (BR-11.c, AC 3): with money still due the
// barrier stays closed. Decision 2026-10-24: recorded in the audit log only, no session column.
public sealed record VerifyParkingSessionExitCommand(Guid SessionId, string Reason, Guid ActorUserId)
    : IRequest<Result<VerifiedParkingSessionExit>>;

public sealed record VerifiedParkingSessionExit(Guid SessionId, string Status, bool CanExit, DateTimeOffset VerifiedAt);

public static class VerifyParkingSessionExitErrors
{
    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "The parking session does not exist.");

    public static readonly Error SessionNotOpen =
        new("parking.session.not_open", "Only an Active or Paid parking session can be verified for exit.");
}

public sealed class VerifyParkingSessionExitCommandValidator : AbstractValidator<VerifyParkingSessionExitCommand>
{
    public VerifyParkingSessionExitCommandValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .MaximumLength(ParkingSession.StaffActionReasonMaxLength);
        RuleFor(command => command.ActorUserId).NotEmpty();
    }
}

public sealed class VerifyParkingSessionExitCommandHandler(
    IParkingSessionRepository sessions,
    IParkingLotAccess lotAccess,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<VerifyParkingSessionExitCommand, Result<VerifiedParkingSessionExit>>
{
    public async Task<Result<VerifiedParkingSessionExit>> Handle(VerifyParkingSessionExitCommand request, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<VerifiedParkingSessionExit>.Failure(VerifyParkingSessionExitErrors.SessionNotFound);

        // TECH-12: the lot comes from the session, not from the client.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, session.LotId, cancellationToken))
            return Result<VerifiedParkingSessionExit>.Failure(ParkingLotAccessErrors.AccessDenied);

        if (!ParkingSession.OpenStatuses.Contains(session.Status))
            return Result<VerifiedParkingSessionExit>.Failure(VerifyParkingSessionExitErrors.SessionNotOpen);

        // Staff confirmed the vehicle and the session in person, so plate and QR count as matched; payment still decides.
        var now = clock.UtcNow;
        session.RecordExitArrival(now);
        var canExit = ExitEligibility.CanExit(session, plateMatches: true, qrMatches: true, now);
        if (!canExit && session.Status == ParkingSessionStatus.Paid && session.IsExitWindowOver(now))
        {
            session.ReopenAfterExitWindow();
        }

        auditLog.Record(new AuditEntry(
            AuditActions.SessionExitManuallyVerified,
            request.ActorUserId,
            session.LotId,
            nameof(ParkingSession),
            session.Id.ToString(),
            request.Reason.Trim(),
            now,
            new Dictionary<string, string>
            {
                ["status"] = session.Status.ToString(),
                ["canExit"] = canExit.ToString()
            }));

        await sessions.TrySaveChangesAsync(cancellationToken);

        return Result<VerifiedParkingSessionExit>.Success(
            new VerifiedParkingSessionExit(session.Id, session.Status.ToString(), canExit, now));
    }
}
