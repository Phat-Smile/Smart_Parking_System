# US-OWNER-002 — Configure parking-lot opening hours (Sprint 2)

Base: `origin/develop` at `9ae474b`. Supports `FR-OWNER-001`, `MF-15` and the arrival-time search acceptance condition. Requires migration `20261020030000_AddParkingLotOpeningHours` before starting the API.

## Endpoints

- `PUT /api/parking/owner/lots/{lotId}/opening-hours` — bearer token with `role=ParkingLotOwner` **and** an active `parking_lot_assignments` row for this `lotId`, `role=Owner`. Complete weekly replacement, returns 200 + saved schedule; invalid schedule 400; unknown/not-owned lot 404; missing authentication 401; non-owner token 403.
- `GET /api/parking/owner/lots/{lotId}/opening-hours` — same owner policy; also works for pending lots.
- `GET /api/parking/lots/{lotId}/opening-hours` — public, **approved** lots only; 404 for missing/pending/rejected lots. `isConfigured=false` if not yet set.
- `GET /api/parking/lots/{lotId}` — existing public detail now includes `openingHours` (null when unset).
- `GET /api/parking/lots?arrivalAt=2026-10-19T02:30:00Z` — approved lots open at the explicit arrival instant. If `arrivalAt` is absent, preserve existing catalog behavior. Invalid timestamp => HTTP 400 via ASP.NET binding.

## PUT JSON body

Send **exactly seven days**, `dayOfWeek` 0 (Sunday) through 6 (Saturday), each exactly once. Vietnam-local hours (`Asia/Ho_Chi_Minh`, UTC+07:00). `opensAt` and `closesAt` are required in strict 24-hour `HH:mm` format for normal opening days. Interval is `[opensAt, closesAt)`; when `closesAt < opensAt`, the interval extends into the next calendar day. Same start and end is rejected: use `is24Hours=true` instead. A closed/24-hour day must have both times `null` (or omitted); cannot be both closed and 24-hour.

```json
{
  "days": [
    { "dayOfWeek": 0, "isClosed": true,  "is24Hours": false, "opensAt": null, "closesAt": null },
    { "dayOfWeek": 1, "isClosed": false, "is24Hours": false, "opensAt": "09:00", "closesAt": "18:00" },
    { "dayOfWeek": 2, "isClosed": false, "is24Hours": false, "opensAt": "09:00", "closesAt": "18:00" },
    { "dayOfWeek": 3, "isClosed": false, "is24Hours": false, "opensAt": "09:00", "closesAt": "18:00" },
    { "dayOfWeek": 4, "isClosed": false, "is24Hours": false, "opensAt": "09:00", "closesAt": "18:00" },
    { "dayOfWeek": 5, "isClosed": false, "is24Hours": false, "opensAt": "09:00", "closesAt": "18:00" },
    { "dayOfWeek": 6, "isClosed": true,  "is24Hours": false, "opensAt": null, "closesAt": null }
  ]
}
```

## Decisions and integration notes

- Since `develop` does not yet contain owner-registration PR #73, ownership uses the existing TECH-12 assignment table. When PR #73 merges, reconcile owner ID/assignment creation with this authorization check.
- Only configured lots are excluded by the arrival-time filter; **legacy lots with no hours remain visible** to preserve the existing catalog until owners set schedules. The product team should decide whether unconfigured lots may continue accepting customers.
- `arrivalAt` must be supplied explicitly by a caller with an ETA. BE-v4 `Điểm mở #15` leaves the rule for **missing travel time** unresolved; do not silently assume ETA or modify search fallback.
- Filtering currently materializes approved candidates before testing the seven-day schedule. This is acceptable for small S2 datasets, but search/AI integration should add a scalable query strategy and use the same schedule evaluator to avoid divergent results.
- Updates write one audit entry (`parking_lot.opening_hours_updated`) in the same EF `SaveChangesAsync` call. Repeating identical JSON causes no additional audit entry.
- Normalized per-day hours, explicit 24h and overnight windows are contract choices for this implementation; align the schema with PO if requirements change.

## Before committing

```bash
 dotnet build src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj
 dotnet test tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj
 dotnet test tests/Parking/Parking.IntegrationTests/Parking.IntegrationTests.csproj
 dotnet ef migrations has-pending-model-changes --project src/Services/Parking/Parking.Persistence --startup-project src/Services/Parking/Parking.WebAPI
 git diff --check
```

Apply migrations only to a disposable DB migrated from `develop`; the existing main DB has previously had orphan parking sessions that can block unrelated FK migrations.
