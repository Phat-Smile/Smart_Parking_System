# D2-BE-1 and D2-BE-2 — List parking lots for owners and administrators

Catalog entries API-PK-01 (`GET /api/parking/owner/lots`) and API-PK-03 (`GET /api/parking/admin/lots`). Both return a page in the same envelope; only the item differs.

```json
{ "items": [ ], "totalCount": 25, "page": 1, "pageSize": 20 }
```

## Paging and filter (both lists)

| Query | Meaning |
|---|---|
| `page` | Page number from 1. Missing, 0 or negative becomes 1. |
| `pageSize` | Rows per page. Missing, 0 or negative becomes 20; more than 100 becomes 100. |
| `status` | Optional. `Pending`, `Approved` or `Rejected`, case-insensitive. Only the name is accepted: a number, an unknown value or a list such as `Pending,Rejected` is a 400. |

- `page` and `pageSize` in the response are the values actually used, after the rules above.
- `totalCount` counts every lot that matches `status`, not only the lots on the page.
- A page past the last one returns an empty `items` and the true `totalCount`.
- A `page` or `pageSize` that is not an integer (`abc`, `1.5`, a number too large for an int) is a 400.

## Owner list

`GET /api/parking/owner/lots`

Role `ParkingLotOwner`; the `sub` claim must be a valid account UUID.

Returns every lot the caller owns, whatever its approval status, so a Pending or Rejected lot is listed with its review result. "Owns" means an `Owner` row in `parking.parking_lot_assignments` (MOD-PK-05): a co-owner added by an administrator sees the lot, an owner removed by an administrator no longer does. `parking_lots.owner_user_id` only records who registered the lot and grants nothing.

Order: name ascending, then id.

200 OK:

```json
{
  "items": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "name": "Bai xe Nguyen Hue",
      "address": "1 Demo Street",
      "latitude": 10.77,
      "longitude": 106.70,
      "type": "Outdoor",
      "approvalStatus": "Rejected",
      "rejectionReason": "Missing required fire safety documentation",
      "hasOpeningHours": true,
      "hasPricing": false
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

`rejectionReason` is null unless the lot is Rejected. `hasOpeningHours` is true once opening hours were saved for the lot; `hasPricing` is true once at least one vehicle type has a price.

## Admin list

`GET /api/parking/admin/lots`

Role `SystemAdmin`. Returns every lot, optionally filtered by `status`, so an administrator can build the review queue.

Order: Pending lots first (the review queue), then by name, then id.

200 OK: the same envelope; each item is

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "ownerUserId": "22222222-2222-2222-2222-222222222222",
  "name": "Bai xe Nguyen Hue",
  "address": "1 Demo Street",
  "latitude": 10.77,
  "longitude": 106.70,
  "type": "Outdoor",
  "approvalStatus": "Pending",
  "rejectionReason": null
}
```

`ownerUserId` is the registrant and can be null for lots seeded before owners existed. Parking does not know the owner's name or email (they belong to Identity); a client looks them up through the administrator account API.

## Owner lot detail

Added for Demo 1 (catalog `API-PK-02`). The owner reads one of their lots in any approval state; the public detail
hides Pending and Rejected lots.

| API | URL | Permission |
| --- | --- | --- |
| GET | `/api/parking/owner/lots/{lotId}` | `ParkingLotOwner` with an `Owner` assignment row for this lot |

```json
{
  "id": "10000000-0000-0000-0000-000000000002",
  "name": "Bãi đỗ chờ duyệt",
  "address": "02 Đường Mẫu, TP. Hồ Chí Minh",
  "latitude": 10.78,
  "longitude": 106.71,
  "type": "Outdoor",
  "approvalStatus": "Rejected",
  "rejectionReason": "Thiếu giấy phép",
  "openingHours": null,
  "pricing": [ { "vehicleType": "CAR", "version": 1, "billingMode": "Block", "blockMinutes": 60, "price": 20000, "overnightFee": 0 } ]
}
```

| Status | `errorCode` | When |
| --- | --- | --- |
| 401 | — | No token. |
| 403 | — | The role is not `ParkingLotOwner`. |
| 403 | `parking.lot.access_denied` | The caller does not own this lot. |
| 404 | `parking.lot_not_found` | The lot does not exist. |

The layout summary listed in the catalog is not in this response: the owner reads it with
`GET /api/parking/owner/lots/{lotId}/layout`.

## Errors

- 400: invalid `status`, or a `page` / `pageSize` that is not an integer.
- 401: unauthenticated request.
- 403: authenticated caller without the required role, or without a valid UUID subject.

## Not in this version

- `createdAtUtc` (listed in the catalog): a lot has no creation time column yet, so adding it needs a Parking migration.
- The catalog's `GET /api/parking/lots/{lotId}/pricing` (all vehicle types at once, API-PK-05): the service has `GET /api/parking/lots/{lotId}/pricing/{vehicleType}` for one vehicle type.
