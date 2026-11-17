using FluentValidation;
using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Booking;
using ParkingSystem.Parking.Application.Incidents;
using ParkingSystem.Parking.Domain.Incidents;
using ParkingSystem.Parking.Application.Common.Identity;
using ParkingSystem.Parking.Application.Common.Realtime;
using ParkingSystem.Parking.Application.Common.Persistence;
using ParkingSystem.Parking.Application.ParkingLayout;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Application.Common.Messaging.Outbox;
using ParkingSystem.Contracts.Events.Parking;

namespace ParkingSystem.Parking.Application.ParkingSessions.OpenParkingSession;

public sealed record OpenParkingSessionCommand(Guid LotId, string PlateNumber, string VehicleType, Guid ActorUserId, DateTimeOffset? PhysicalDetectionTime = null)
    : IRequest<Result<OpenedParkingSession>>;

public sealed record OpenedParkingSession(
    Guid SessionId,
    Guid LotId,
    string PlateNumber,
    string VehicleType,
    string CustomerType,
    string Status,
    DateTimeOffset EntryTime,
    bool IsAccountLinked,
    Guid? ReservationId,
    string QrToken,
    Guid? ZoneId,
    string? ZoneCode,
    string? FloorName,
    int? AvailableMotorbikeSpaces,
    string? SlotCode);

public static class OpenParkingSessionErrors
{
    // ASSUMPTION: one open (Active or Paid) session per plate per lot; not written in Context yet.
    public static readonly Error SessionAlreadyOpen =
        new("parking.session.already_open", "This plate already has an open session in this lot.");

    // Unknown and not-approved lots share one error, like the public lot detail API (PR #31 review).
    public static readonly Error LotNotFound =
        new("parking.lot.not_found", "The parking lot does not exist or is not approved.");

    // US-PARK-008 (BR-07.b): every motorbike zone of the lot has reached its capacity, so the barrier stays closed.
    public static readonly Error MotorbikeZoneFull =
        new("parking.motorbike_zone.full", "Every motorbike zone of this lot is full.");

    public static readonly Error MotorbikeZoneNotConfigured =
        new("parking.motorbike_zone.not_configured", "This lot has no motorbike zone.");

    // US-PARK-007 (BR-04.h): walk-in capacity = max(0, Available car slots - reservations held) is 0, so the barrier stays
    // closed. The message is the one the gate screen shows.
    public static readonly Error WalkInFull =
        new("parking.walk_in.full", "Hết chỗ cho xe không đặt trước.");

    // US-PARK-004 (BR-02.f, point 35): a car with a reservation arrived and no suitable slot is left. An Open
    // NoSuitableSlot incident is recorded so staff place it in another zone or the prepaid amount is refunded.
    public static readonly Error ReservedNoSlot =
        new("parking.session.reserved_no_slot", "Không còn chỗ phù hợp cho xe đã đặt chỗ. Nhân viên xếp khu khác hoặc hoàn tiền đặt trước.");

    // US-PARK-007 (decision 2026-10-09): Booking did not answer and no answer from the last minute is known, so walk-in
    // cars are refused rather than risking a slot that belongs to a reservation. Motorbikes are not affected.
    public static readonly Error HeldQuotaUnavailable =
        new("parking.held_quota.unavailable", "Cannot read the reservations held for this lot right now. Walk-in cars are not accepted.");
}

public sealed class OpenParkingSessionCommandValidator : AbstractValidator<OpenParkingSessionCommand>
{
    public OpenParkingSessionCommandValidator()
    {
        RuleFor(command => command.LotId).NotEmpty();
        RuleFor(command => command.ActorUserId).NotEmpty();

        RuleFor(command => command.PlateNumber)
            .Must(plate => !string.IsNullOrWhiteSpace(plate))
            .MaximumLength(ParkingSession.PlateNumberMaxLength);

        // Match names only: Enum.TryParse also accepts numeric strings ("1") and padded values (" Car ").
        RuleFor(command => command.VehicleType)
            .Must(value => Enum.GetNames<VehicleType>().Contains(value, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"vehicleType must be one of: {string.Join(", ", Enum.GetNames<VehicleType>())}.");
    }
}

/// <summary>
/// One step of the gate-entry flow (MAIN FLOW MF-08, MF-09A, MF-09B), not the whole entry. Called by staff or the gate
/// only after these steps are done elsewhere:
/// <list type="bullet">
/// <item>The plate is accepted: confidence at least 0.9 and a valid format, or confirmed by staff (BR-05.a).</item>
/// <item>Car with a booking (MF-08): Booking has moved the booking to Used, and a slot is designated (US-PARK-004).</item>
/// </list>
/// Walk-in cars (MF-09A, US-PARK-007) are admitted here only while walk-in capacity, max(0, Available car slots minus the
/// reservations Booking holds), is above 0; one slot is then designated atomically and its code returned to tell the driver.
/// Motorbikes (MF-09B, US-PARK-008) take one space of the lot's motorbike zone with the most free spaces here, atomically
/// and in the same transaction as the session; the zone and floor are returned so they can be printed with the QR.
/// The barrier stays closed only when every motorbike zone is full.
/// Opening the session is the moment the system sends the barrier-open command, so EntryTime is taken from the clock here.
/// </summary>
public sealed class OpenParkingSessionCommandHandler(
    IParkingSessionRepository sessions,
    IParkingLotDetailReader parkingLots,
    IParkingLotAccess lotAccess,
    IPlateOwnerLookup plateOwners,
    IClock clock,
    IMotorbikeZoneCounter motorbikeZones,
    IParkingUnitOfWork unitOfWork,
    ISlotDesignator slots,
    IHeldQuotaProvider heldQuota,
    IZoneLabelReader zoneLabels,
    IBookingCheckInClient bookingClient,
    IBookingNoSlotReporter noSlotReporter,
    IAvailableCarSlotCounter availableCarSlots,
    IIncidentRepository incidents,
    IParkingRealtimeNotifier? realtimeNotifier = null,
    IOutboxWriter? outbox = null) : IRequestHandler<OpenParkingSessionCommand, Result<OpenedParkingSession>>
{
    public async Task<Result<OpenedParkingSession>> Handle(OpenParkingSessionCommand request, CancellationToken cancellationToken)
    {
        // Only an existing, approved lot can receive vehicles.
        if (await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken) is null)
            return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.LotNotFound);

        // TECH-12: chỉ Staff/Owner được gắn với bãi này mới mở phiên được.
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, request.LotId, cancellationToken))
            return Result<OpenedParkingSession>.Failure(ParkingLotAccessErrors.AccessDenied);

        var plate = ParkingSession.NormalizePlate(request.PlateNumber);
        var vehicleType = Enum.Parse<VehicleType>(request.VehicleType, ignoreCase: true);

        if (await sessions.HasOpenSessionAsync(request.LotId, plate, cancellationToken))
            return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.SessionAlreadyOpen);

        // A plate match links the session for history and fees only; it is not authentication (BR-05.f).
        var owner = await plateOwners.FindByPlateAsync(plate, cancellationToken);

        var sessionId = Guid.NewGuid();
        var entryTime = request.PhysicalDetectionTime ?? clock.UtcNow;

        // US-PARK-004 (MF-08) & US-PARK-007: asked before the transaction, so no lot lock is held while waiting for Booking.
        CheckedInReservation? checkedInBooking = null;
        int? held = null;
        if (vehicleType == VehicleType.Car)
        {
            // BR-02.f, point 35 option (a): count first and check the reservation in only when a slot is left, so a
            // reservation is never moved to Used for a car that cannot get a slot. Booking then cancels and refunds it.
            if (await availableCarSlots.CountAvailableCarSlotsAsync(request.LotId, cancellationToken) <= 0)
            {
                var cancelledReservationId = await noSlotReporter.ReportNoSuitableSlotAsync(
                    request.LotId, plate, entryTime, cancellationToken);
                if (cancelledReservationId is not { } reservationId)
                    return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.WalkInFull);

                incidents.Add(Incident.OpenNoSuitableSlot(Guid.NewGuid(), request.LotId, reservationId, plate, entryTime));
                await incidents.SaveChangesAsync(cancellationToken);
                return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.ReservedNoSlot);
            }

            // Kiểm tra xe có đặt chỗ trước đang Confirmed tại bãi trong khung giờ hay không (US-PARK-004)
            checkedInBooking = await bookingClient.CheckInReservationAsync(
                request.LotId,
                plate,
                entryTime,
                sessionId,
                cancellationToken);

            // Nếu không có đặt chỗ (Walk-in), kiểm tra quota để đảm bảo không lấn vào số chỗ đã giữ (US-PARK-007)
            if (checkedInBooking is null)
            {
                held = await heldQuota.GetHeldReservationsAsync(request.LotId, cancellationToken);
                if (held is null)
                    return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.HeldQuotaUnavailable);
            }
        }

        // Taking the space or slot and saving the session commit together: a failed save gives it back.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        DesignatedSlot? slot = null;
        if (vehicleType == VehicleType.Car)
        {
            var available = await slots.LockLotAndCountAvailableCarSlotsAsync(request.LotId, cancellationToken);

            if (checkedInBooking is not null)
            {
                // US-PARK-004: Xe có đặt chỗ đã được giữ slot trước đó trong held quota.
                // Chỉ từ chối nếu bãi thực tế không còn slot trống nào khả dụng.
                if (available <= 0)
                    return await NoSlotAfterCheckInAsync(request, checkedInBooking, plate, entryTime, transaction, cancellationToken);
            }
            else
            {
                // BR-02.d: slots kept for held reservations are not offered to walk-ins.
                if (available - held!.Value <= 0)
                    return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.WalkInFull);
            }

            // BR-04.a, BR-04.g: one Available slot becomes Assigned to this session in one statement. Null only when a slot
            // changed state after the count without the lock (for example staff set it to Maintenance).
            slot = await slots.DesignateCarSlotAsync(request.LotId, sessionId, entryTime, cancellationToken);
            if (slot is null)
                return checkedInBooking is null
                    ? Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.WalkInFull)
                    : await NoSlotAfterCheckInAsync(request, checkedInBooking, plate, entryTime, transaction, cancellationToken);
        }

        MotorbikeSpaceTaken? space = null;
        if (vehicleType == VehicleType.Motorbike)
        {
            // US-PARK-008: check and +1 in one statement, so two gates can never both take the last space.
            space = await motorbikeZones.TakeSpaceAsync(request.LotId, cancellationToken);
            if (space is null)
                return Result<OpenedParkingSession>.Failure(
                    await motorbikeZones.GetAvailabilityAsync(request.LotId, cancellationToken) is null
                        ? OpenParkingSessionErrors.MotorbikeZoneNotConfigured
                        : OpenParkingSessionErrors.MotorbikeZoneFull);
        }

        var session = ParkingSession.Open(
            sessionId,
            request.LotId,
            plate,
            vehicleType,
            entryTime,
            SessionQrToken.New(),
            checkedInBooking?.UserId ?? owner?.UserId,
            checkedInBooking?.VehicleId ?? owner?.VehicleId,
            space?.ZoneId ?? slot?.ZoneId,
            checkedInBooking?.ReservationId);
        sessions.Add(session);
        if (session.UserId is { } customerId)
            outbox?.Enqueue(new ParkingSessionOpenedIntegrationEvent(session.Id, customerId,
                session.LotId, session.EntryTime), "parking.session-opened.v1", 1);

        // Two entry requests for the same plate at the same time: the unique index lets only one win.
        if (!await sessions.TrySaveChangesAsync(cancellationToken))
            return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.SessionAlreadyOpen);

        await transaction.CommitAsync(cancellationToken);

        // US-MAP-003: Broadcast realtime status change if slot or motorbike space was assigned
        if (realtimeNotifier is not null)
        {
            if (slot is not null)
            {
                await realtimeNotifier.NotifySlotStatusChangedAsync(
                    request.LotId,
                    slot.SlotId,
                    slot.SlotCode,
                    "Assigned",
                    "Available",
                    entryTime,
                    cancellationToken);
            }
            else if (space is not null)
            {
                await realtimeNotifier.NotifyLotOccupancyChangedAsync(
                    request.LotId,
                    0,
                    space.AvailableSpaces,
                    entryTime,
                    cancellationToken);
            }
        }

        // The driver is told the slot with its zone and floor (BR-04.a), like the motorbike zone printed with the QR.
        ZoneLabel? slotZone = null;
        if (slot is not null)
            (await zoneLabels.GetLabelsAsync([slot.ZoneId], cancellationToken)).TryGetValue(slot.ZoneId, out slotZone);

        return Result<OpenedParkingSession>.Success(new OpenedParkingSession(
            session.Id,
            session.LotId,
            session.PlateNumber,
            session.VehicleType.ToString(),
            session.CustomerType.ToString(),
            session.Status.ToString(),
            session.EntryTime,
            owner is not null || checkedInBooking?.UserId is not null,
            session.ReservationId,
            session.QrToken,
            session.ZoneId,
            space?.ZoneCode ?? slotZone?.ZoneCode,
            space?.FloorName ?? slotZone?.FloorName,
            space?.AvailableSpaces,
            slot?.SlotCode));
    }

    // Rare race: a slot was free at the count above but taken before the lock, after Booking already moved the
    // reservation to Used. Booking has no way out of Used yet (point 35), so staff handle it from the incident (BR-02.f).
    private async Task<Result<OpenedParkingSession>> NoSlotAfterCheckInAsync(
        OpenParkingSessionCommand request,
        CheckedInReservation checkedInBooking,
        string plate,
        DateTimeOffset entryTime,
        IParkingTransaction transaction,
        CancellationToken cancellationToken)
    {
        incidents.Add(Incident.OpenNoSuitableSlot(Guid.NewGuid(), request.LotId, checkedInBooking.ReservationId, plate, entryTime));
        await incidents.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<OpenedParkingSession>.Failure(OpenParkingSessionErrors.ReservedNoSlot);
    }
}
