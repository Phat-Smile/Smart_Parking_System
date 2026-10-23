# US-SEARCH-004 — Filter parking lots by vehicle type

**Sprint:** S2 · **Scope:** Parking Service · **SP:** 1

## API

`GET /api/parking/lots?vehicleType={Car|Motorbike}` (public, no auth).

| Request | Expected HTTP | Behavior |
|---|---:|---|
| `/api/parking/lots` | 200 | Existing approved catalog, unchanged |
| `?vehicleType=Car` | 200 | Approved lots with ≥1 car zone |
| `?vehicleType=Motorbike` | 200 | Approved lots with ≥1 motorbike zone |
| `?vehicleType=car` | 200 | Vehicle type comparison is case-insensitive |
| `?vehicleType=Bicycle` | 400 | Validation problem, `vehicleType` error |
| `?vehicleType=1` | 400 | Numeric enum value is not accepted |
| `?vehicleType=` | 400 | Empty filter is invalid |

### Output

Same JSON array as the existing catalog. No new response fields or migrations.

```json
[
  {
    "id": "...",
    "name": "Central Parking",
    "address": "1 Demo Street",
    "latitude": 10.776530,
    "longitude": 106.700981,
    "type": "MultiStorey"
  }
]
```

**Data rule:** A parking lot must be `Approved` and have a floor belonging to that lot with at least one zone whose `VehicleGroup` matches `Car`/`Motorbike`. An unfiltered request includes approved lots even if their layout has no zones. `Type` (`Outdoor`, `MultiStorey`, `Underground`) is not a vehicle type. This filter checks supported vehicle categories, **not remaining available capacity**.

**Implementation:** EF Core correlated `EXISTS` subqueries; deterministic name/ID ordering; no duplicated lots when multiple zones match. Existing `IParkingLotCatalogReader.ListApprovedAsync(ct)` callers keep compiling because the optional filter defaults to null.

## Testing

- Unit: unfiltered query forwards null filter; filtered queries forward `Car` and `Motorbike`.
- Integration: migrated PostgreSQL checks approved-only, matching zone, no-zone behavior and duplicate elimination.
- Postman: use included collection; an empty `[]` may mean local seed data contains no zones for selected type, so verify SQL/integration test as well.

## Notes

- Query parameter is intentionally optional; sending an empty string is invalid (400).
- Filter works on the catalog endpoint only. Filtering nearby/by-address routes requires separately extending those APIs after their PRs are merged, to avoid coupling this branch to unmerged work.
