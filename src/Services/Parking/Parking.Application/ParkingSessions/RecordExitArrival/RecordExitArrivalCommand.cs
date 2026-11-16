using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.RecordExitArrival;

// Q-EXIT-WINDOW: Gate camera or QR scanner records the vehicle's arrival at the exit gate for a Paid session.
public sealed record RecordExitArrivalCommand(Guid SessionId, Guid ActorUserId)
    : IRequest<Result<RecordedExitArrival>>;

public sealed record RecordedExitArrival(Guid SessionId, DateTimeOffset ExitArrivedAt);

public static class RecordExitArrivalErrors
{
    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "The parking session does not exist.");

    public static readonly Error SessionNotPaid =
        new("parking.session.not_paid", "Only a Paid parking session can record exit arrival.");

    public static readonly Error ExitWindowExpired =
        new("parking.session.exit_window_expired", "The exit window has expired. Overstay fee payment required.");
}

public sealed class RecordExitArrivalCommandValidator : AbstractValidator<RecordExitArrivalCommand>
{
    public RecordExitArrivalCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}

public sealed class RecordExitArrivalCommandHandler(
    IParkingSessionRepository sessions,
    IParkingLotAccess lotAccess,
    IClock clock) : IRequestHandler<RecordExitArrivalCommand, Result<RecordedExitArrival>>
{
    public async Task<Result<RecordedExitArrival>> Handle(RecordExitArrivalCommand request, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdForUpdateAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<RecordedExitArrival>.Failure(RecordExitArrivalErrors.SessionNotFound);

        if (!await lotAccess.CanOperateAsync(request.ActorUserId, session.LotId, cancellationToken))
            return Result<RecordedExitArrival>.Failure(ParkingLotAccessErrors.AccessDenied);

        if (session.Status != ParkingSessionStatus.Paid)
            return Result<RecordedExitArrival>.Failure(RecordExitArrivalErrors.SessionNotPaid);

        var now = clock.UtcNow;
        if (session.ExitArrivedAt is { } existingArrival)
        {
            if (session.IsExitWindowOver(now))
            {
                session.ReopenAfterExitWindow();
                await sessions.TrySaveChangesAsync(cancellationToken);
                return Result<RecordedExitArrival>.Failure(RecordExitArrivalErrors.ExitWindowExpired);
            }

            return Result<RecordedExitArrival>.Success(new RecordedExitArrival(session.Id, existingArrival));
        }

        if (!session.RecordExitArrival(now))
        {
            session.ReopenAfterExitWindow();
            await sessions.TrySaveChangesAsync(cancellationToken);
            return Result<RecordedExitArrival>.Failure(RecordExitArrivalErrors.ExitWindowExpired);
        }

        await sessions.TrySaveChangesAsync(cancellationToken);

        return Result<RecordedExitArrival>.Success(new RecordedExitArrival(session.Id, session.ExitArrivedAt!.Value));
    }
}
