using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Auditing;
using ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;
using ParkingSystem.Parking.Domain.ParkingLayout;
using ParkingSystem.Parking.Domain.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;
using ParkingSystem.Parking.Persistence.ParkingLots;

namespace ParkingSystem.Parking.Persistence.ParkingLayout;

internal sealed class ManageParkingLayoutRepository(ParkingDbContext db, IAuditLog audit, IClock clock)
    : IManageParkingLayoutRepository
{
    public Task<bool> LotExistsAsync(Guid lotId, CancellationToken cancellationToken) =>
        db.ParkingLots.AsNoTracking().AnyAsync(lot => lot.Id == lotId, cancellationToken);

    // Do not use IParkingLotAccess: that contract also permits Staff.
    public Task<bool> IsOwnerAsync(Guid lotId, Guid actorId, CancellationToken cancellationToken) =>
        db.IsOwnerAsync(lotId, actorId, cancellationToken);

    public async Task<ParkingLotLayout> ReadAsync(Guid lotId, CancellationToken cancellationToken)
    {
        var floors = await db.Floors.AsNoTracking().Where(f => f.LotId == lotId)
            .OrderBy(f => f.Name).ToListAsync(cancellationToken);
        var floorIds = floors.Select(f => f.Id).ToArray();
        var zones = await db.Zones.AsNoTracking().Where(z => floorIds.Contains(z.FloorId))
            .OrderBy(z => z.Code).ToListAsync(cancellationToken);
        var zoneIds = zones.Select(z => z.Id).ToArray();
        var slots = await db.ParkingSlots.AsNoTracking().Where(s => s.LotId == lotId && zoneIds.Contains(s.ZoneId))
            .OrderBy(s => s.SlotCode).ToListAsync(cancellationToken);

        var document = await db.LayoutDocuments.AsNoTracking().SingleOrDefaultAsync(d => d.LotId == lotId, cancellationToken);
        LayoutFloorInput[] stored = document is null ? [] : JsonSerializer.Deserialize<LayoutFloorInput[]>(document.RenderJson) ?? [];
        var storedZones = stored.SelectMany(f => f.Zones ?? []).ToDictionary(z => z.Id);
        var storedSlots = stored.SelectMany(f => f.Zones ?? []).SelectMany(z => z.Slots ?? []).ToDictionary(s => s.Id);

        return new ParkingLotLayout(lotId, document?.Version ?? 0, floors.Select(f => new LayoutFloor(f.Id, f.Name,
            zones.Where(z => z.FloorId == f.Id).Select(z => new LayoutZone(
                z.Id, z.Code, z.VehicleGroup.ToString(), z.Capacity, z.CurrentVehicleCount, z.HasSlotSensor,
                slots.Where(s => s.ZoneId == z.Id).Select(s => new LayoutSlot(
                    s.Id, s.SlotCode, s.OverrideState?.ToString(), s.EffectiveStatus.ToString(),
                    storedSlots.GetValueOrDefault(s.Id)?.Position)).ToArray(),
                storedZones.GetValueOrDefault(z.Id)?.Bounds, storedZones.GetValueOrDefault(z.Id)?.Polygon
            )).ToArray()
        )).ToArray());
    }

    public async Task<SaveLayoutOutcome> SaveAsync(Guid lotId, Guid actorId, int expectedVersion,
        IReadOnlyList<LayoutFloorInput> floors, CancellationToken cancellationToken)
    {
        // Serializable isolation prevents an approval-status change racing a direct Layout save.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var lot = await db.ParkingLots.AsNoTracking().SingleOrDefaultAsync(l => l.Id == lotId, cancellationToken);
            if (lot is null) return new SaveLayoutOutcome(ParkingLayoutErrors.LotNotFound);

            if (lot.ApprovalStatus == ParkingLotApprovalStatus.Approved)
            {
                var version = await db.LayoutDocuments.AsNoTracking().Where(d => d.LotId == lotId)
                    .Select(d => (int?)d.Version).SingleOrDefaultAsync(cancellationToken) ?? 0;
                if (version != expectedVersion) return new SaveLayoutOutcome(ParkingLayoutErrors.StaleVersion);
                if (await db.LayoutChangeRequests.AnyAsync(r => r.LotId == lotId &&
                    r.Status == ParkingLayoutChangeStatus.Pending, cancellationToken))
                    return new SaveLayoutOutcome(LayoutReviewErrors.PendingAlreadyExists);

                var proposal = ParkingLayoutChangeRequest.Submit(Guid.NewGuid(), lotId, actorId, version,
                    JsonSerializer.Serialize(floors), clock.UtcNow);
                db.LayoutChangeRequests.Add(proposal);
                audit.Record(new AuditEntry(AuditActions.ParkingLayoutReviewSubmitted, actorId, lotId,
                    nameof(ParkingLayoutChangeRequest), proposal.Id.ToString(), null, clock.UtcNow));
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new SaveLayoutOutcome(PendingReview: ToSummary(proposal));
            }

            var conflict = await SaveActiveAsync(lotId, actorId, expectedVersion, floors, cancellationToken);
            if (conflict is not null) return new SaveLayoutOutcome(conflict);
            await transaction.CommitAsync(cancellationToken);
            return new SaveLayoutOutcome();
        }
        catch (DbUpdateConcurrencyException) { return new SaveLayoutOutcome(LayoutReviewErrors.ConcurrencyConflict); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return new SaveLayoutOutcome(LayoutReviewErrors.PendingAlreadyExists); }
        catch (PostgresException e) when (e.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        { return new SaveLayoutOutcome(LayoutReviewErrors.ConcurrencyConflict); }
    }

    public async Task<IReadOnlyList<LayoutReviewRequest>> ListPendingReviewsAsync(CancellationToken cancellationToken)
    {
        var requests = await db.LayoutChangeRequests.AsNoTracking()
            .Where(r => r.Status == ParkingLayoutChangeStatus.Pending)
            .OrderBy(r => r.RequestedAtUtc).ToListAsync(cancellationToken);
        return requests.Select(r => new LayoutReviewRequest(r.Id, r.LotId, r.RequestedBy,
            r.ExpectedVersion, r.RequestedAtUtc,
            JsonSerializer.Deserialize<LayoutFloorInput[]>(r.ProposedLayoutJson) ?? [])).ToArray();
    }

    public async Task<ReviewLayoutOutcome> ReviewAsync(Guid lotId, Guid requestId, Guid adminId,
        bool approve, string? reason, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var request = await db.LayoutChangeRequests.SingleOrDefaultAsync(r => r.Id == requestId &&
                r.LotId == lotId, cancellationToken);
            if (request is null) return new ReviewLayoutOutcome(LayoutReviewErrors.NotFound);
            if (request.Status != ParkingLayoutChangeStatus.Pending)
                return new ReviewLayoutOutcome(LayoutReviewErrors.AlreadyReviewed);
            var lot = await db.ParkingLots.AsNoTracking().SingleOrDefaultAsync(l => l.Id == lotId, cancellationToken);
            if (lot?.ApprovalStatus != ParkingLotApprovalStatus.Approved)
                return new ReviewLayoutOutcome(LayoutReviewErrors.LotNotApproved);

            if (approve)
            {
                LayoutFloorInput[]? proposed;
                try { proposed = JsonSerializer.Deserialize<LayoutFloorInput[]>(request.ProposedLayoutJson); }
                catch (JsonException) { return new ReviewLayoutOutcome(ParkingLayoutErrors.Conflict); }
                if (ParkingLayoutInputValidator.Validate(proposed) is { } invalid)
                    return new ReviewLayoutOutcome(invalid);
                var conflict = await SaveActiveAsync(lotId, adminId, request.ExpectedVersion, proposed!, cancellationToken);
                if (conflict is not null) return new ReviewLayoutOutcome(conflict);
                request.Approve(adminId, clock.UtcNow);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
                    return new ReviewLayoutOutcome(LayoutReviewErrors.InvalidDecision);
                request.Reject(adminId, reason, clock.UtcNow);
            }

            audit.Record(new AuditEntry(approve ? AuditActions.ParkingLayoutReviewApproved :
                AuditActions.ParkingLayoutReviewRejected, adminId, lotId,
                nameof(ParkingLayoutChangeRequest), request.Id.ToString(), reason, clock.UtcNow));
            // SaveActiveAsync may have already called SaveChanges; both operations are covered by this transaction.
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ReviewLayoutOutcome(Review: ToSummary(request));
        }
        catch (DbUpdateConcurrencyException) { return new ReviewLayoutOutcome(LayoutReviewErrors.ConcurrencyConflict); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return new ReviewLayoutOutcome(LayoutReviewErrors.ConcurrencyConflict); }
        catch (PostgresException e) when (e.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        { return new ReviewLayoutOutcome(LayoutReviewErrors.ConcurrencyConflict); }
    }

    private static LayoutReviewSubmission ToSummary(ParkingLayoutChangeRequest request) =>
        new(request.Id, request.LotId, request.ExpectedVersion, request.Status.ToString(), request.RequestedAtUtc);

    private async Task<Error?> SaveActiveAsync(Guid lotId, Guid actorId, int expectedVersion,
        IReadOnlyList<LayoutFloorInput> floors, CancellationToken cancellationToken)
    {
        // One SaveChanges for operational entities, JSON geometry, version and audit.
        var document = await db.LayoutDocuments.SingleOrDefaultAsync(d => d.LotId == lotId, cancellationToken);
        if ((document?.Version ?? 0) != expectedVersion)
            return ParkingLayoutErrors.StaleVersion;
        LayoutFloorInput[] previous = document is null ? [] : JsonSerializer.Deserialize<LayoutFloorInput[]>(document.RenderJson) ?? [];
        var oldGeometryZones = previous.SelectMany(f => f.Zones ?? []).ToDictionary(z => z.Id);
        var oldGeometrySlots = previous.SelectMany(f => f.Zones ?? []).SelectMany(z => z.Slots ?? []).ToDictionary(s => s.Id);
        var oldFloors = await db.Floors.Where(f => f.LotId == lotId).ToListAsync(cancellationToken);
        var floorIds = oldFloors.Select(f => f.Id).ToArray();
        var oldZones = await db.Zones.Where(z => floorIds.Contains(z.FloorId)).ToListAsync(cancellationToken);
        var zoneIds = oldZones.Select(z => z.Id).ToArray();
        var oldSlots = await db.ParkingSlots.Where(s => s.LotId == lotId).ToListAsync(cancellationToken);

        var incomingFloors = floors.ToDictionary(f => f.Id);
        var incomingZones = floors.SelectMany(f => f.Zones!.Select(z => (floor: f, zone: z)))
            .ToDictionary(x => x.zone.Id);
        var incomingSlots = floors.SelectMany(f => f.Zones!.SelectMany(z => z.Slots!.Select(s => (zone: z, slot: s))))
            .ToDictionary(x => x.slot.Id);

        // No deletion: slots can be linked to sessions or be controlled by sensor/dispatch workflows.
        if (oldFloors.Any(f => !incomingFloors.ContainsKey(f.Id))
            || oldZones.Any(z => !incomingZones.ContainsKey(z.Id) || incomingZones[z.Id].floor.Id != z.FloorId
                || !string.Equals(incomingZones[z.Id].zone.VehicleGroup, z.VehicleGroup.ToString(), StringComparison.OrdinalIgnoreCase)
                || z.CurrentVehicleCount > incomingZones[z.Id].zone.Capacity)
            || oldSlots.Any(s => !incomingSlots.ContainsKey(s.Id) || incomingSlots[s.Id].zone.Id != s.ZoneId))
            return ParkingLayoutErrors.Conflict;

        // A client must not inject an ID already owned by a different lot.
        var newFloorIds = incomingFloors.Keys.Except(floorIds).ToArray();
        var newZoneIds = incomingZones.Keys.Except(zoneIds).ToArray();
        var oldSlotIds = oldSlots.Select(s => s.Id).ToArray();
        var newSlotIds = incomingSlots.Keys.Except(oldSlotIds).ToArray();
        if (await db.Floors.AsNoTracking().AnyAsync(f => newFloorIds.Contains(f.Id), cancellationToken)
            || await db.Zones.AsNoTracking().AnyAsync(z => newZoneIds.Contains(z.Id), cancellationToken)
            || await db.ParkingSlots.AsNoTracking().AnyAsync(s => newSlotIds.Contains(s.Id), cancellationToken))
            return ParkingLayoutErrors.DuplicateId;

        // Prevent moving a live slot on the floor even if its code and declared override stay equal.
        if (oldSlots.Any(s => (s.AssignedSessionId is not null || s.DispatchState is not null || s.SensorState == SensorState.Occupied)
                && oldGeometrySlots.GetValueOrDefault(s.Id)?.Position != incomingSlots[s.Id].slot.Position)
            || oldZones.Any(z => z.CurrentVehicleCount > 0 &&
                (!Equals(oldGeometryZones.GetValueOrDefault(z.Id)?.Bounds, incomingZones[z.Id].zone.Bounds)
                 || !SamePoints(oldGeometryZones.GetValueOrDefault(z.Id)?.Polygon, incomingZones[z.Id].zone.Polygon))))
            return ParkingLayoutErrors.Conflict;
        // A UNIQUE constraint cannot be satisfied by temporarily swapping two existing slot codes.
        var slotCodes = oldSlots.ToDictionary(s => s.SlotCode, StringComparer.OrdinalIgnoreCase);
        if (oldSlots.Any(s =>
                slotCodes.TryGetValue(ParkingSlot.NormalizeSlotCode(incomingSlots[s.Id].slot.SlotCode!), out var other)
                && other.Id != s.Id))
            return ParkingLayoutErrors.Conflict;

        // Likewise, a newly inserted slot may not claim a code still owned by an existing row
        // during the same SaveChanges batch, even if the old row is being renamed.
        if (incomingSlots.Values.Any(x => !oldSlotIds.Contains(x.slot.Id)
                && slotCodes.TryGetValue(ParkingSlot.NormalizeSlotCode(x.slot.SlotCode!), out var old)
                && old.Id != x.slot.Id))
            return ParkingLayoutErrors.Conflict;

        foreach (var s in oldSlots)
        {
            var input = incomingSlots[s.Id].slot;
            var requestedOverride = input.OverrideState is null
                ? (OverrideState?)null : Enum.Parse<OverrideState>(input.OverrideState, true);
            if ((s.SlotCode != ParkingSlot.NormalizeSlotCode(input.SlotCode!) || s.OverrideState != requestedOverride)
                && (s.AssignedSessionId is not null || s.DispatchState is not null || s.SensorState == SensorState.Occupied))
                return ParkingLayoutErrors.Conflict;
        }

        bool changed = false;
        var oldFloorById = oldFloors.ToDictionary(f => f.Id);
        var oldZoneById = oldZones.ToDictionary(z => z.Id);
        var oldSlotById = oldSlots.ToDictionary(s => s.Id);
        foreach (var floor in floors)
        {
            if (oldFloorById.TryGetValue(floor.Id, out var existingFloor))
                changed |= existingFloor.Rename(floor.Name!);
            else
            {
                db.Floors.Add(Floor.Create(floor.Id, lotId, floor.Name!));
                changed = true;
            }
            foreach (var zone in floor.Zones!)
            {
                if (oldZoneById.TryGetValue(zone.Id, out var existingZone))
                    changed |= existingZone.Reconfigure(zone.Code!, zone.Capacity, zone.HasSlotSensor);
                else
                {
                    db.Zones.Add(Zone.Create(zone.Id, floor.Id, zone.Code!,
                        Enum.Parse<VehicleType>(zone.VehicleGroup!, true), zone.Capacity, zone.HasSlotSensor));
                    changed = true;
                }
                foreach (var slot in zone.Slots!)
                {
                    var declaredOverride = slot.OverrideState is null
                        ? (OverrideState?)null : Enum.Parse<OverrideState>(slot.OverrideState, true);
                    if (oldSlotById.TryGetValue(slot.Id, out var existingSlot))
                        changed |= existingSlot.Reconfigure(slot.SlotCode!, declaredOverride);
                    else
                    {
                        db.ParkingSlots.Add(ParkingSlot.Create(slot.Id, lotId, zone.Id, slot.SlotCode!, declaredOverride));
                        changed = true;
                    }
                }
            }
        }

        // Normalize names/codes for consistent GET→PUT round trips and idempotence.
        var canonical = floors.Select(f => new LayoutFloorInput(f.Id, f.Name!.Trim(),
            f.Zones!.Select(z => new LayoutZoneInput(z.Id, z.Code!.Trim(),
                Enum.Parse<VehicleType>(z.VehicleGroup!, true).ToString(), z.Capacity, z.HasSlotSensor,
                z.Slots!.Select(slot => new LayoutSlotInput(slot.Id, ParkingSlot.NormalizeSlotCode(slot.SlotCode!),
                    slot.OverrideState is null ? null : Enum.Parse<OverrideState>(slot.OverrideState, true).ToString(),
                    slot.Position)).ToArray(), z.Bounds, z.Polygon)).ToArray())).ToArray();
        var renderJson = JsonSerializer.Serialize(canonical);
        if (document is null)
        {
            db.LayoutDocuments.Add(new ParkingLayoutDocument { LotId = lotId, Version = 1, RenderJson = renderJson });
            changed = true;
        }
        else if (!SameDocument(document.RenderJson, renderJson))
        {
            document.RenderJson = renderJson;
            document.Version++;
            changed = true;
        }
        else if (changed)
        {
            document.Version++;
        }

        if (changed)
        {
            audit.Record(new AuditEntry(AuditActions.ParkingLayoutUpdated, actorId, lotId, nameof(ParkingLotLayout),
                lotId.ToString(), Reason: null, clock.UtcNow,
                new Dictionary<string, string> { ["floors"] = floors.Count.ToString(),
                    ["zones"] = incomingZones.Count.ToString(), ["slots"] = incomingSlots.Count.ToString() }));
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ParkingLayoutErrors.Conflict;
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return ParkingLayoutErrors.Conflict;
            }
        }
        return null;
    }

    private static bool SamePoints(IReadOnlyList<GridPoint>? a, IReadOnlyList<GridPoint>? b) =>
        a is null ? b is null : b is not null && a.SequenceEqual(b);

    private static bool SameDocument(string oldJson, string newJson) =>
        JsonElement.DeepEquals(JsonSerializer.Deserialize<JsonElement>(oldJson), JsonSerializer.Deserialize<JsonElement>(newJson));
}
