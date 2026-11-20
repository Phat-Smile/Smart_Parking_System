using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Contracts.Events.Parking;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.CompleteParkingSession;

// US-PAY-006 (BR-09.d, BR-11.c, BR-11.e, MF-12): staff or the gate device call this when the vehicle is at the exit, and
// this call is what confirms the vehicle left (lead decision 2026-11-04). Parking takes no money here: payment arrives
// as payment.session-paid.v1.
// ManualVerificationReason (US-STAFF-004, BR-11.d): staff checked the vehicle in person because the gate read the plate
// wrong. It stands in for the plate match only; the QR must still match and payment still decides.
public sealed record CompleteParkingSessionCommand(
    Guid SessionId,
    string PlateNumber,
    string QrToken,
    Guid ActorUserId,
    string? ManualVerificationReason = null)
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
        RuleFor(command => command.ManualVerificationReason)
            .MaximumLength(ParkingSession.StaffActionReasonMaxLength);
    }
}

public sealed class CompleteParkingSessionCommandHandler(
    IParkingSessionRepository sessions,
    IParkingUnitOfWork unitOfWork,
    IParkingLotAccess lotAccess,
    ISlotDesignator slots,
    IMotorbikeZoneCounter motorbikeZones,
    IOutboxWriter outbox,
    IAuditLog auditLog,
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
        var manualReason = request.ManualVerificationReason?.Trim();
        var plateManuallyVerified = !plateMatches && !string.IsNullOrEmpty(manualReason);
        if (!qrMatches || (!plateMatches && !plateManuallyVerified))
            return Result<CompletedParkingSession>.Failure(CompleteParkingSessionErrors.ExitNotVerified);

        if (session.Status == ParkingSessionStatus.Active)
            return Result<CompletedParkingSession>.Failure(CompleteParkingSessionErrors.PaymentRequired);

        var now = clock.UtcNow;
        session.RecordExitArrival(now);
        if (!ExitEligibility.CanExit(session, plateMatches: true, qrMatches, now) || !session.Complete(now))
        {
            // BR-09.d: Paid goes back to Active until the extra fee is paid.
            session.ReopenAfterExitWindow();
            if (plateManuallyVerified)
                RecordManualVerification(request, session, manualReason!, canExit: false, now);
            await sessions.TrySaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<CompletedParkingSession>.Failure(CompleteParkingSessionErrors.ExitWindowExpired);
        }

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
                now,
                session.UserId),
            "parking.session-closed.v1",
            1);

        if (plateManuallyVerified)
            RecordManualVerification(request, session, manualReason!, canExit: true, now);

        await sessions.TrySaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<CompletedParkingSession>.Success(ToResult(session, now));
    }

    // NFR-16: who let the vehicle out on a manual check, and why. Saved in the same transaction. No plate value (NFR-15).
    private void RecordManualVerification(
        CompleteParkingSessionCommand request, ParkingSession session, string reason, bool canExit, DateTimeOffset now) =>
        auditLog.Record(new AuditEntry(
            AuditActions.SessionExitManuallyVerified,
            request.ActorUserId,
            session.LotId,
            nameof(ParkingSession),
            session.Id.ToString(),
            reason,
            now,
            new Dictionary<string, string>
            {
                ["status"] = session.Status.ToString(),
                ["canExit"] = canExit.ToString(),
                ["plateMatched"] = bool.FalseString
            }));

    private static CompletedParkingSession ToResult(ParkingSession session, DateTimeOffset completedAt) =>
        new(session.Id, session.Status.ToString(), completedAt);
}
