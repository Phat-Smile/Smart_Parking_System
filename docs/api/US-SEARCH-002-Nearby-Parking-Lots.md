# US-SEARCH-002 — Search Nearby Parking Lots (BE-2, Sprint 2)

## Endpoint

`GET /api/parking/lots/nearby?latitude=10.77653&longitude=106.700981&radiusKm=5&limit=20`

- Authentication: anonymous allowed.
- `latitude` required, decimal, from -90 to 90.
- `longitude` required, decimal, from -180 to 180.
- `radiusKm` optional, > 0 and <= 50; default 5 km (implementation default; confirm with PO).
- `limit` optional, 1 to 100; default 20 (implementation default; confirm with PO).
- Returns HTTP 200 with a JSON array of approved parking lots only. Empty array if none found.
- Returns HTTP 400 on missing, malformed or out-of-range query values.

Example response:

```json
[
  {
    "id": "10000000-0000-0000-0000-000000000001",
    "name": "Near",
    "address": "A",
    "latitude": 10.776530,
    "longitude": 106.700981,
    "type": "Outdoor",
    "distanceKm": 0.0
  }
]
```

Rules: excludes Pending/Rejected lots, calculates great-circle distance (Haversine),
sorts ascending by unrounded distance (then name, id), and limits result count.
Candidates are first narrowed by latitude in PostgreSQL; no new table or migration is needed.

## Testing

Run build, Parking.UnitTests, Parking.IntegrationTests and then an anonymous HTTP smoke test.
This patch was authored against the supplied repository snapshot; **validate it against the latest origin/develop** before pushing.
