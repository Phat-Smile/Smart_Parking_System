# US-MAP-002 — Public parking layout read (2D)

Base: `origin/develop` at `5eb3bf3` (2026-11-02).

`GET /api/parking/lots/{lotId}/layout` serves the canonical floor/zone/slot geometry and current `effectiveStatus` of approved lots. Anonymous access is allowed for approved lots; pending, rejected, unknown lots return 404. Responses have `Cache-Control: no-store`.

**Contract:** `PublicParkingLayout { lotId, version, asOfUtc, floors }`; nested floors/zones/slots reuse `ParkingLayoutContract`. Coordinates and polygons originate from Owner-stored layout; FE draws 2D. The same contract may be reused by a later 3D FE integration; this task does not deliver a 3D renderer or a realtime stream.

**Tests:** allowed approved lot, hidden unapproved lot, unknown lot, layout properties and slot status. No migrations.
