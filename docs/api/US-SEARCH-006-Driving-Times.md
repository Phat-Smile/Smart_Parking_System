# US-SEARCH-006 — Driving time to parking lots (BE-2, Sprint 3)

## Public endpoints

Both endpoints are anonymous; origin coordinates are supplied by the requesting client, and are **not** persisted or logged by the feature.

- `GET /api/parking/lots/driving-times?latitude=10.77653&longitude=106.700981&radiusKm=5&limit=10`
  - Uses the existing approved-nearby search (Haversine radius) to select up to ten candidates, then Mapbox Matrix to obtain road travel time/distance.
  - `latitude` -90..90 and `longitude` -180..180 are required; `radiusKm` default 5, >0..50; `limit` default 10, 1..10.
  - HTTP 200 returns an array ranked by reachable travel time in **seconds**, with unreachable lots placed last. An empty candidate list returns `[]` without a provider request.
- `GET /api/parking/lots/{lotId}/driving-time?latitude=10.77653&longitude=106.700981`
  - Only an **Approved** lot is visible. Returns HTTP 404 for unknown, Pending and Rejected lots, without contacting Mapbox.
  - Uses Mapbox Directions and returns HTTP 200 with a single estimate.

Example response item:

```json
{
  "lotId": "10000000-0000-0000-0000-000000000001",
  "name": "Bãi A",
  "address": "Địa chỉ A",
  "latitude": 10.78,
  "longitude": 106.71,
  "reachable": true,
  "durationSeconds": 420,
  "distanceMeters": 2350,
  "straightLineDistanceKm": 1.8
}
```

For the single-lot endpoint, `straightLineDistanceKm` is `null`. When Mapbox confirms there is no road route, `reachable=false`, `durationSeconds=null` and `distanceMeters=null` (never zero or an invented straight-line ETA). Invalid query parameters return HTTP 400; a Mapbox failure or missing token returns HTTP 503 without leaking tokens/provider URLs.

## Operational and integration notes

- Set the existing `Mapbox:AccessToken` (`Mapbox__AccessToken` environment variable) on the Parking API server; do not expose it to the browser or commit it to Git. No external Mapbox call is made until an endpoint with eligible lots is requested.
- The Matrix provider call is limited to ten nearby approved lots to bound cost. Additional nearby lots beyond ten are not queried in this request; a larger candidate selection would require a later approved batching/pagination design.
- Do not assume a no-route result means the lot is closed. The backlog marks opening-hours filtering when no driving time as **MISSING SPEC**; this implementation leaves the approval/availability/opening-hours rules unchanged.
- The FE task mentions drawing the route on the map. **Geometry/polyline is not returned by these endpoints** because the existing Directions client requests `overview=false`. Drawing a road route needs a separately agreed geometry contract or client map routing behavior.

## Tests

Run light checks first:

```bash
 dotnet test --project tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj --filter-class ParkingSystem.Tests.Parking.UnitTests.ParkingDrivingTimeEstimatorTests
 dotnet test --project tests/Parking/Parking.IntegrationTests/Parking.IntegrationTests.csproj --filter-class ParkingSystem.Tests.Parking.IntegrationTests.ParkingDrivingTimeHttpTests
 dotnet build src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj
```

The HTTP tests use TestServer and in-memory readers; no Docker or real Mapbox token is needed. Full Parking integration tests use Testcontainers and may require a stable Docker environment.
