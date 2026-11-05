using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.MarkParkingSessionPaid;

// US-PAY-006 (MF-12 step 83): Payment reports payment.session-paid.v1 for every way of paying, including an amount of 0
// (lead decision 2026-11-04), so this is the only way a session becomes Paid.
public sealed record MarkParkingSessionPaidCommand(Guid SessionId, DateTimeOffset PaidAtUtc) : IRequest<Result<ParkingSessionPaid>>;

public sealed record ParkingSessionPaid(Guid SessionId, string Status);

public static class MarkParkingSessionPaidErrors
{
    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "The parking session does not exist.");

    // The event came again or too late: the session is already Paid or closed, so nothing changes.
    public static readonly Error SessionNotActive =
        new("parking.session.not_active", "Only an Active parking session can be marked as paid.");
}

public sealed class MarkParkingSessionPaidCommandHandler(IParkingSessionRepository sessions)
    : IRequestHandler<MarkParkingSessionPaidCommand, Result<ParkingSessionPaid>>
{
    public async Task<Result<ParkingSessionPaid>> Handle(MarkParkingSessionPaidCommand request, CancellationToken cancellationToken)
    {
        var session = await sessions.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<ParkingSessionPaid>.Failure(MarkParkingSessionPaidErrors.SessionNotFound);

        if (!session.MarkPaid(request.PaidAtUtc))
            return Result<ParkingSessionPaid>.Failure(MarkParkingSessionPaidErrors.SessionNotActive);

        await sessions.TrySaveChangesAsync(cancellationToken);

        return Result<ParkingSessionPaid>.Success(new ParkingSessionPaid(session.Id, session.Status.ToString()));
    }
}
