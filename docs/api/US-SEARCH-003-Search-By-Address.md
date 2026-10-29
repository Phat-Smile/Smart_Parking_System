# US-SEARCH-003 — Search Parking Lots By Address (Sprint 2, BE-2)

> Story status in master sheet: **Proposed**. Confirm final UX / provider policy with PO before closing the story.

## Endpoint

`GET /api/parking/lots/by-address?address=Nguyen%20Hue%2C%20Ho%20Chi%20Minh%20City&radiusKm=5&limit=20`

Anonymous GET. Parameters:

- `address` required; nonblank, 3–200 characters. Geocode only upon an explicit search action (not on each keystroke).
- `radiusKm` optional, (0, 50], default 5 km (proposed implementation limit).
- `limit` optional, 1–100, default 20 (proposed implementation limit).

Success **200**:

```json
{
  "destination": {
    "displayName": "Nguyễn Huệ, Hồ Chí Minh, Việt Nam",
    "latitude": 10.775,
    "longitude": 106.703
  },
  "parkingLots": [
    {"id":"10000000-0000-0000-0000-000000000001", "name":"Example", "address":"1 Demo", "latitude":10.77653, "longitude":106.700981, "type":"Outdoor", "distanceKm":0.27}
  ]
}
```

- **200 + `parkingLots: []`**: address geocoded successfully; no approved lots within radius.
- **400**: missing / invalid address, radius or limit.
- **404**: Nominatim did not find a destination. User can correct the query or choose a point on the map.
- **429**: in-process upstream cap of one request/second; retry later.
- **503**: provider unavailable / invalid upstream response / timeout; no false 404.

Only approved lots are read via the existing `IParkingLotCatalogReader`. Haversine distance is sorted before rounding. The first Nominatim search match is used; disambiguation is out of this 3-SP patch.

## Nominatim requirements and deployment

- Explicit user-triggered searches **only**. **No autocomplete using the public Nominatim server**, even with debounce.
- Public endpoint **maximum one request per second per application**: this package's in-process gate is only suitable for **one API instance**. For multiple replicas, a **distributed rate limiter** or a self-hosted / commercial provider is required.
- Cache identical normalized addresses for 12 hours; cache no-result lookups for 15 minutes.
- Identify the application using a valid User-Agent (`Nominatim:UserAgent`) and provide OSM attribution in the frontend: `© OpenStreetMap contributors`.
- Change `Nominatim:BaseUrl` via config when migrating providers. Do not log search-address strings.
- Do **not** use live Nominatim in automated tests; mock `IAddressGeocoder`.
- User stories / frontend backlog mention address suggestions on each keystroke, which conflicts with the public server usage policy; PO/FE must choose a supported provider or self-host.
- Production scaling and high-traffic optimization are **not done**: this isolated PR filters the existing approved catalog in memory, instead of a bounded geospatial DB query. Consolidate with US-SEARCH-002's reader after it merges.

Sources:

- https://operations.osmfoundation.org/policies/nominatim/
- https://nominatim.org/release-docs/latest/api/Search/

## Run locally

1. Switch to a clean branch created from `origin/develop`.
2. Apply the accompanying patch. Local run should use existing project Kestrel settings (`5002` if configured).
3. `dotnet build src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj`
4. `dotnet test tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj`
5. `dotnet test tests/Parking/Parking.IntegrationTests/Parking.IntegrationTests.csproj`
6. Run one manual request to `/api/parking/lots/by-address?...` using an address intended for public geocoding. Requests within 1s to distinct addresses can return 429 by design; repeated cached address queries do not use Nominatim.

No database migration required.
