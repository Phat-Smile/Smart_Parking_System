# US-MAP-004 — Customer zone utilization (BE-2 / S3)

## Public API

`GET /api/parking/lots/{lotId}/zone-utilization`

- Anonymous read-only endpoint for **Approved** lots only; missing, Draft, Pending, or Rejected lots return 404.
- `Cache-Control: no-store`. Read again after realtime events; the response is a current snapshot, not a push stream.
- Response: `lotId`, `layoutVersion`, `zones[]` with `floorId`, `zoneId`, `zoneCode`, `vehicleGroup`, `declaredCapacity`, `usableCapacity`, `inUse`, `available`, status counts, and `utilizationPercent`.
- The existing `GET /api/parking/lots/{lotId}/layout` supplies individual slot states and geometry for the 2D/3D UI.
- The owner API `/api/parking/owner/lots/{lotId}/zone-utilization` remains authenticated and unchanged.

## Counting rules

- **Car:** count actual slots. `Occupied`, `Assigned`, and `NeedsVerification` count as in use. `Maintenance` and `Unavailable` are excluded from usable capacity; `Available` slots are free. Owner-declared capacity is informational, not an invented free-slot count.
- **Motorbike:** use `currentVehicleCount` versus the zone's declared capacity; clamp to `[0, capacity]`. No individual motorbike slots are expected.
- `utilizationPercent` = `inUse / usableCapacity * 100`, rounded to two decimals; zero when usable capacity is zero.
- Booking held quotas are **not** allocated to physical zones before vehicle arrival. These figures are physical utilization, not a guarantee of availability for new reservations.

## Verification

- Unit: car blocked statuses and sensor statuses; motorbike counter bounds.
- Integration: pending/missing lot returns 404-equivalent application error; Approved car/motorbike zones reflect updated database states without changing layout version.
- HTTP smoke (on existing approved seeded lot): `curl -i http://127.0.0.1:5002/api/parking/lots/10000000-0000-0000-0000-000000000001/zone-utilization`.

**Scope:** This BE patch does not implement a frontend widget, persist per-event historical snapshots, or guarantee an under-two-second UI update. Existing SignalR updates can trigger a client refresh.
