using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.ExtendExitWindow;

// Q-EXIT-WINDOW (BR-09.d): staff grant a one-time 15-minute extension to the exit window of a Paid session with mandatory reason.
public sealed record ExtendExitWindowCommand(Guid SessionId, string Reason, Guid ActorUserId)
    : IRequest<Result<ExtendedExitWindow>>;

public sealed record ExtendedExitWindow(Guid SessionId, DateTimeOffset NewExitWindowEndsAt);

public static class ExtendExitWindowErrors
{
    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "The parking session does not exist.");

    public static readonly Error CannotExtend =
        new("parking.session.cannot_extend_exit_window", "Only a Paid session without a prior extension can have its exit window extended.");
}

public sealed class ExtendExitWindowCommandValidator : AbstractValidator<ExtendExitWindowCommand>
{
    public ExtendExitWindowCommandValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.Reason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .MaximumLength(ParkingSession.StaffActionReasonMaxLength);
        RuleFor(command => command.ActorUserId).NotEmpty();
    }
}

public sealed class ExtendExitWindowCommandHandler(
    IParkingSessionRepository sessions,
    IParkingLotAccess lotAccess,
    IAuditLog auditLog,
    IClock clock) : IRequestHandler<ExtendExitWindowCommand, Result<ExtendedExitWindow>>
{
    public async Task<Result<ExtendedExitWindow>> Handle(ExtendExitWindowCommand request, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<ExtendedExitWindow>.Failure(ExtendExitWindowErrors.SessionNotFound);

        // TECH-12: the lot comes from the session, not from the client.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, session.LotId, cancellationToken))
            return Result<ExtendedExitWindow>.Failure(ParkingLotAccessErrors.AccessDenied);

        var now = clock.UtcNow;
        if (!session.ExtendExitWindow(now, request.Reason))
            return Result<ExtendedExitWindow>.Failure(ExtendExitWindowErrors.CannotExtend);

        auditLog.Record(new AuditEntry(
            AuditActions.SessionExitWindowExtended,
            request.ActorUserId,
            session.LotId,
            nameof(ParkingSession),
            session.Id.ToString(),
            request.Reason.Trim(),
            now,
            new Dictionary<string, string>
            {
                ["newExitWindowEndsAt"] = session.ExitWindowEndsAt!.Value.ToString("o")
            }));

        await sessions.TrySaveChangesAsync(cancellationToken);

        return Result<ExtendedExitWindow>.Success(new ExtendedExitWindow(session.Id, session.ExitWindowEndsAt!.Value));
    }
}
