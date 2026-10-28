# US-OWNER-007 — Save / read parking layout (S2)

Base: `develop` at `9ae474b`. This is a Parking backend API only; 2D/3D drawing, IoT simulation, real-time slot state transitions, and slot deletion are out of scope.

## Security

Both endpoints require a valid JWT with `role=ParkingLotOwner` **and** a current `parking_lot_assignments` record with role `Owner` for the requested lot. An owner of another lot and a Staff-assigned account receive HTTP 403; missing lot returns 404. The role policy alone is insufficient. Before using the API, SystemAdmin must assign the Owner through TECH-12. A future Register Parking Lot integration must create that assignment.

## Endpoints

- `GET /api/parking/owner/lots/{lotId}/layout` — 200 with nested floors/zones/slots; 403/404 as above.
- `PUT /api/parking/owner/lots/{lotId}/layout` — saves the complete **non-destructive** snapshot, returns the refreshed layout; 400 invalid payload, 403/404 authorization/existence, 409 conflicting existing entities or active operational slots.

A `GET` response contains `version` (0 before the first save) and rendering coordinates. Every `PUT` must include `expectedVersion` from the latest GET; a stale value returns 409. The server increments version when data changes, so clients can cache the JSON by version (Context architectural rule). Rendering JSON lives in `parking.parking_layout_documents` (`jsonb`) alongside the operational Floor/Zone/Slot tables.

Client sends stable GUIDs generated once for each floor, zone, and car slot. When editing, start from GET and retain all existing IDs, including IDs for unchanged records. **Omitting an existing floor, zone or slot returns 409; it never deletes it.** ID reuse from another lot returns 409. Parent relation (zone→floor, slot→zone) and vehicle group of an existing zone cannot be changed. New floors, zones and slots may be added; names, zone capacities/sensor flags, slot codes and declared overrides can be updated safely.

Updates to code or override of a slot that has an assigned session, dispatch state, or occupied sensor state return 409; no operational state is reset by a layout save. `currentVehicleCount`, `sensorState`, `dispatchState`, `assignedSessionId`, `effectiveStatus`, and `rowVersion` are server-owned fields. Saving unchanged data is idempotent and does not create another audit record. A real change records `parking.layout.updated` in the same `SaveChangesAsync` as its layout updates.

### Example PUT

```json
{
  "expectedVersion": 0,
  "floors": [
    {
      "id": "0a000000-0000-0000-0000-000000000001",
      "name": "Ground",
      "zones": [
        {
          "id": "0b000000-0000-0000-0000-000000000001",
          "code": "CAR-A",
          "vehicleGroup": "Car",
          "capacity": 2,
          "hasSlotSensor": false,
          "slots": [
            { "id": "0c000000-0000-0000-0000-000000000001", "slotCode": "A-01", "overrideState": null, "position": { "x": 0, "y": 0 } },
            { "id": "0c000000-0000-0000-0000-000000000002", "slotCode": "A-02", "overrideState": "Unavailable", "position": { "x": 1, "y": 0 } }
          ]
        },
        {
          "id": "0b000000-0000-0000-0000-000000000002",
          "code": "BIKE-A",
          "vehicleGroup": "Motorbike",
          "capacity": 30,
          "hasSlotSensor": false,
          "bounds": { "x": 0, "y": 3, "width": 6, "height": 5 },
          "slots": []
        }
      ]
    }
  ]
}
```

Validation: IDs must be nonempty and unique by entity type, floor names unique per lot, zone codes unique per floor, slot codes unique per lot (normalized uppercase), car zone slot count <= declared capacity, each car slot requires a unique grid `position` within its zone, motorbike zone requires `bounds` or a polygon of at least 3 points, no individual motorbike slots; only `Maintenance`, `Unavailable`, or `null` are accepted for `overrideState`.

TECH-06 already introduced the operational `floors`, `zones`, and `parking_slots` tables. This patch adds a separate EF migration `AddOwnerLayoutRenderDocument` for versioned rendering JSON, without altering those operational tables. Do not manually migrate or seed production databases while testing. Test with isolated Postgres / Testcontainers.

## Known follow-up decisions

Removal/reparenting of existing items and concurrent runtime layout editing need a separate safety contract; no silent deletion is implemented. Open point 21 in Context covers slot effective-status priority; this change uses `ParkingSlot.ComputeEffectiveStatus` rather than changing that priority.
