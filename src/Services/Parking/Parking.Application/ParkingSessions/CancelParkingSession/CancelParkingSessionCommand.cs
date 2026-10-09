using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.CancelParkingSession;

// Team decision (lead review, 2026-10-06): the barrier was opened but the vehicle did not enter,
// so staff undo the session manually. The session is kept as Cancelled and the action is audited (NFR-16).
public sealed record CancelParkingSessionCommand(Guid SessionId, string Reason, Guid ActorUserId)
    : IRequest<Result<CancelledParkingSession>>;

public sealed record CancelledParkingSession(Guid SessionId, string Status, DateTimeOffset CancelledAt);

public static class CancelParkingSessionErrors
{
    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "The parking session does not exist.");

    public static readonly Error SessionNotActive =
        new("parking.session.not_active", "Only an Active parking session can be cancelled.");
}

public sealed class CancelParkingSessionCommandValidator : AbstractValidator<CancelParkingSessionCommand>
{
    public CancelParkingSessionCommandValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .MaximumLength(ParkingSession.CancelReasonMaxLength);
        RuleFor(command => command.ActorUserId).NotEmpty();
    }
}

public sealed class CancelParkingSessionCommandHandler(
    IParkingSessionRepository sessions,
    IParkingLotAccess lotAccess,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<CancelParkingSessionCommand, Result<CancelledParkingSession>>
{
    public async Task<Result<CancelledParkingSession>> Handle(CancelParkingSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<CancelledParkingSession>.Failure(CancelParkingSessionErrors.SessionNotFound);

        // TECH-12: bãi lấy từ phiên, không tin bãi do client gửi.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, session.LotId, cancellationToken))
            return Result<CancelledParkingSession>.Failure(ParkingLotAccessErrors.AccessDenied);

        var previousStatus = session.Status;
        var now = clock.UtcNow;
        if (!session.Cancel(request.ActorUserId, request.Reason, now))
            return Result<CancelledParkingSession>.Failure(CancelParkingSessionErrors.SessionNotActive);

        // Saved together with the status change, so a cancellation without an audit entry cannot happen.
        auditLog.Record(new AuditEntry(
            AuditActions.SessionCancelled,
            request.ActorUserId,
            session.LotId,
            nameof(ParkingSession),
            session.Id.ToString(),
            session.CancelReason!,
            now,
            new Dictionary<string, string>
            {
                ["fromStatus"] = previousStatus.ToString(),
                ["toStatus"] = session.Status.ToString()
            }));

        await sessions.TrySaveChangesAsync(cancellationToken);

        return Result<CancelledParkingSession>.Success(
            new CancelledParkingSession(session.Id, session.Status.ToString(), now));
    }
}
