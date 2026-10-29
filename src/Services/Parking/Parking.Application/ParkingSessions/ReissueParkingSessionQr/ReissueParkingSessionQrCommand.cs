using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.ReissueParkingSessionQr;

// US-STAFF-004 (BR-11.d, MF-14 step 96): the customer lost the session QR. The old QR is invalidated before the new one
// is handed out, and the action is audited with its reason (NFR-16).
public sealed record ReissueParkingSessionQrCommand(Guid SessionId, string Reason, Guid ActorUserId)
    : IRequest<Result<ReissuedParkingSessionQr>>;

public sealed record ReissuedParkingSessionQr(Guid SessionId, string QrToken, DateTimeOffset ReissuedAt);

public static class ReissueParkingSessionQrErrors
{
    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "The parking session does not exist.");

    // Decision 2026-10-24: payment happens at the exit gate and the barrier opens right after, so only an Active session
    // needs a new QR; a Completed or Cancelled session's QR is invalid for good (BR-11.e).
    public static readonly Error SessionNotActive =
        new("parking.session.not_active", "Only an Active parking session can get a new QR.");
}

public sealed class ReissueParkingSessionQrCommandValidator : AbstractValidator<ReissueParkingSessionQrCommand>
{
    public ReissueParkingSessionQrCommandValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .MaximumLength(ParkingSession.StaffActionReasonMaxLength);
        RuleFor(command => command.ActorUserId).NotEmpty();
    }
}

public sealed class ReissueParkingSessionQrCommandHandler(
    IParkingSessionRepository sessions,
    IParkingLotAccess lotAccess,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<ReissueParkingSessionQrCommand, Result<ReissuedParkingSessionQr>>
{
    public async Task<Result<ReissuedParkingSessionQr>> Handle(ReissueParkingSessionQrCommand request, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<ReissuedParkingSessionQr>.Failure(ReissueParkingSessionQrErrors.SessionNotFound);

        // TECH-12: the lot comes from the session, not from the client.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, session.LotId, cancellationToken))
            return Result<ReissuedParkingSessionQr>.Failure(ParkingLotAccessErrors.AccessDenied);

        if (!session.ReissueQr(SessionQrToken.New()))
            return Result<ReissuedParkingSessionQr>.Failure(ReissueParkingSessionQrErrors.SessionNotActive);

        var now = clock.UtcNow;
        // No token in the audit entry: anyone reading the log could otherwise use the QR.
        auditLog.Record(new AuditEntry(
            AuditActions.SessionQrReissued,
            request.ActorUserId,
            session.LotId,
            nameof(ParkingSession),
            session.Id.ToString(),
            request.Reason.Trim(),
            now));

        // One SaveChanges: the new token and the audit entry are committed together.
        await sessions.TrySaveChangesAsync(cancellationToken);

        return Result<ReissuedParkingSessionQr>.Success(new ReissuedParkingSessionQr(session.Id, session.QrToken, now));
    }
}
