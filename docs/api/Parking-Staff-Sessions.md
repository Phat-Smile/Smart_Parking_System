# Staff lots, lot assignments and parking session reads (Sprint 2)

Four read-only endpoints for the staff screens, the admin lot screen and the customer QR page. All errors use the shared
problem format with an `errorCode` field. Times are UTC ISO 8601.

| # | Route | Who can call | Used in Demo A, flow 4 |
|---|---|---|---|
| 1 | `GET /api/parking/staff/lots` | `Staff`, `ParkingLotOwner` | Step 2: staff pick the lot for their shift |
| 2 | `GET /api/parking/admin/lots/{lotId}/assignments` | `SystemAdmin` | Step 1: admin sees who is assigned |
| 3 | `GET /api/parking/sessions/{sessionId}` | the session's customer, or staff assigned to its lot | Step 6: customer sees the session and its QR |
| 4 | `GET /api/parking/staff/lots/{lotId}/sessions` | `Staff`, `ParkingLotOwner` assigned to the lot | Step 7: staff see vehicles in the lot, cancel a wrong session |

## 1. List my lots

`GET /api/parking/staff/lots` — approved lots the caller is assigned to (as Owner or Staff), sorted by name. The web app
should call this instead of `/staff/assigned-lot`, which does not exist.

200 OK (an account with no lot gets `[]`):

```json
[
  { "id": "10000000-0000-0000-0000-000000000001", "name": "Central", "address": "1 Demo Street", "type": "Outdoor" }
]
```

`type` is `MultiStorey`, `Underground` or `Outdoor`. Pending and rejected lots are not listed.

## 2. List the accounts assigned to a lot

`GET /api/parking/admin/lots/{lotId}/assignments` — oldest assignment first.

200 OK (a lot with nobody assigned gets `[]`):

```json
[
  { "accountId": "11111111-1111-1111-1111-111111111111", "role": "Staff", "assignedAtUtc": "2026-10-27T08:00:00+00:00" }
]
```

`role` is `Owner` or `Staff`. Assign and remove use the existing `PUT` / `DELETE .../assignments/{accountId}`.

## 3. Get one parking session

`GET /api/parking/sessions/{sessionId}` — any signed-in account may call; the server decides:

- the customer linked to the session gets it **with** `qrToken`, but only while the session is `Active` or `Paid` (a closed session's QR is invalid);
- staff or owner assigned to the session's lot get it **without** the QR (`qrToken: null`);
- anyone else gets 403.

200 OK:

```json
{
  "sessionId": "2b684220-8663-48a3-aed6-a1ef40748c3a",
  "lotId": "10000000-0000-0000-0000-000000000001",
  "plateNumber": "59A-12345",
  "vehicleType": "Motorbike",
  "status": "Active",
  "entryTime": "2026-10-27T08:00:00+00:00",
  "slotCode": null,
  "qrToken": "q0Z8...43 chars",
  "paidAtUtc": null,
  "graceUntilUtc": null
}
```

`status` is `Active`, `Paid`, `Completed` or `Cancelled`. The `Location` header returned when a session is opened points here.

**Fields that are always `null` in Sprint 2** (decision 2026-10-27; they will be filled later, not removed):

| Field | Filled by |
|---|---|
| `slotCode` | US-PARK-007, once a car is given a slot. Motorbikes have no slot. |
| `paidAtUtc` | US-PAY-006, once a session is marked Paid. |
| `graceUntilUtc` | US-PAY-006: `paidAtUtc` + 15 minutes (BR-09.d). |

## 4. List the sessions of a lot

`GET /api/parking/staff/lots/{lotId}/sessions?status=Active&page=1&pageSize=20` — newest entry first.

| Query | Default | Rule |
|---|---|---|
| `status` | all statuses | `Active`, `Paid`, `Completed` or `Cancelled`, case-insensitive |
| `page` | 1 | 1 or more |
| `pageSize` | 20 | 1 to 100 |

200 OK:

```json
{
  "items": [
    { "sessionId": "2b684220-8663-48a3-aed6-a1ef40748c3a", "plateNumber": "59A-12345", "vehicleType": "Motorbike",
      "status": "Active", "entryTime": "2026-10-27T08:00:00+00:00", "slotCode": null }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

## Errors

| Status | `errorCode` | When |
|---|---|---|
| 400 | (validation) | API 4: `page` < 1, `pageSize` outside 1–100, or unknown `status` |
| 401 | — | no or invalid token |
| 403 | — | wrong role (Customer on API 1/4, anyone but SystemAdmin on API 2) |
| 403 | `parking.lot.access_denied` | API 3: not the session's customer and not assigned to its lot. API 4: not assigned to the lot (an unknown lot also returns this) |
| 404 | `parking.lot.not_found` | API 2: unknown lot (same code as `PUT` / `DELETE` assignments) |
| 404 | `parking.session.not_found` | API 3: unknown session |

## Test accounts

Use the shared test accounts (password `Test@12345`): `admin@parking.test`, `owner@parking.test`, `staff@parking.test`,
`customer@parking.test`. No extra seed is needed:

1. Sign in as `admin@parking.test` and assign the staff account to a lot:
   `PUT /api/parking/admin/lots/{lotId}/assignments/{staffAccountId}` with `{ "role": "Staff" }`.
2. Sign in as `staff@parking.test` and call API 1; the lot appears once it is `Approved`.
3. Open a session for a plate registered to `customer@parking.test`, then call API 3 as the customer to see the QR.
