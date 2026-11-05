using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.CompleteParkingSession;

// US-PAY-006 (BR-09.d, BR-11.c, BR-11.e, MF-12): staff or the gate device call this when the vehicle is at the exit, and
// this call is what confirms the vehicle left (lead decision 2026-11-04). Parking takes no money here: payment arrives
// as payment.session-paid.v1.
public sealed record CompleteParkingSessionCommand(Guid SessionId, string PlateNumber, string QrToken, Guid ActorUserId)
    : IRequest<Result<CompletedParkingSession>>;

public sealed record CompletedParkingSession(Guid SessionId, string Status, DateTimeOffset CompletedAt);

public static class CompleteParkingSessionErrors
{
    public static readonly Error SessionNotFound =
        new("parking.session.not_found", "The parking session does not exist.");

    public static readonly Error SessionNotOpen =
        new("parking.session.not_open", "The parking session is no longer open.");

    // BR-11.d: the barrier stays closed and staff verify the vehicle (US-STAFF-004).
    public static readonly Error ExitNotVerified =
        new("parking.session.exit_not_verified", "The plate or the QR does not match this parking session.");

    public static readonly Error PaymentRequired =
        new("parking.session.payment_required", "The parking session must be paid before the vehicle can leave.");

    public static readonly Error ExitWindowExpired =
        new("parking.session.exit_window_expired", "The exit window has passed. The extra fee must be paid before the vehicle can leave.");
}

public sealed class CompleteParkingSessionCommandValidator : AbstractValidator<CompleteParkingSessionCommand>
{
    public CompleteParkingSessionCommandValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.ActorUserId).NotEmpty();
        RuleFor(command => command.PlateNumber)
            .Must(plate => !string.IsNullOrWhiteSpace(plate))
            .MaximumLength(ParkingSession.PlateNumberMaxLength);
        RuleFor(command => command.QrToken)
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .MaximumLength(ParkingSession.QrTokenMaxLength);
    }
}

public sealed class CompleteParkingSessionCommandHandler(
    IParkingSessionRepository sessions,
    IParkingUnitOfWork unitOfWork,
    IParkingLotAccess lotAccess,
    ISlotDesignator slots,
    IMotorbikeZoneCounter motorbikeZones,
    IOutboxWriter outbox,
    IClock clock) : IRequestHandler<CompleteParkingSessionCommand, Result<CompletedParkingSession>>
{
    public async Task<Result<CompletedParkingSession>> Handle(CompleteParkingSessionCommand request, CancellationToken cancellationToken)
    {
        // The row lock makes two calls for one session run one after another, so a slot or space is given back only once.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var session = await sessions.FindByIdForUpdateAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result<CompletedParkingSession>.Failure(CompleteParkingSessionErrors.SessionNotFound);

        // TECH-12: the lot comes from the session, not from the client.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, session.LotId, cancellationToken))
            return Result<CompletedParkingSession>.Failure(ParkingLotAccessErrors.AccessDenied);

        // MF-12: the gate may repeat the call. The vehicle already left, so nothing is done a second time.
        if (session.Status == ParkingSessionStatus.Completed)
            return Result<CompletedParkingSession>.Success(ToResult(session, session.ExitTime!.Value));

        if (session.Status == ParkingSessionStatus.Cancelled)
            return Result<CompletedParkingSession>.Failure(CompleteParkingSessionErrors.SessionNotOpen);

        var plateMatches = session.PlateNumber == ParkingSession.NormalizePlate(request.PlateNumber);
        var qrMatches = session.QrToken == request.QrToken.Trim();
        if (!plateMatches || !qrMatches)
            return Result<CompletedParkingSession>.Failure(CompleteParkingSessionErrors.ExitNotVerified);

        if (session.Status == ParkingSessionStatus.Active)
            return Result<CompletedParkingSession>.Failure(CompleteParkingSessionErrors.PaymentRequired);

        var now = clock.UtcNow;
        if (!ExitEligibility.CanExit(session, plateMatches, qrMatches, now))
        {
            // BR-09.d: Paid goes back to Active until the extra fee is paid.
            session.ReopenAfterExitWindow();
            await sessions.TrySaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<CompletedParkingSession>.Failure(CompleteParkingSessionErrors.ExitWindowExpired);
        }

        session.Complete(now);

        // BR-11.e: a car gives its slot back, a motorbike one space of its zone (BR-07).
        if (session.VehicleType == VehicleType.Motorbike)
        {
            if (session.ZoneId is { } zoneId)
                await motorbikeZones.ReleaseSpaceAsync(zoneId, cancellationToken);
        }
        else
        {
            await slots.ReleaseSessionSlotAsync(session.Id, cancellationToken);
        }

        outbox.Enqueue(
            new ParkingSessionClosedIntegrationEvent(
                session.Id,
                session.LotId,
                session.PlateNumber,
                session.VehicleType.ToString(),
                session.EntryTime,
                now,
                now),
            "parking.session-closed.v1",
            1);

        await sessions.TrySaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<CompletedParkingSession>.Success(ToResult(session, now));
    }

    private static CompletedParkingSession ToResult(ParkingSession session, DateTimeOffset completedAt) =>
        new(session.Id, session.Status.ToString(), completedAt);
}
