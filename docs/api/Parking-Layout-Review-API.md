# Parking Layout — Save & Change Review API

**Service:** Parking · **Scope:** US-OWNER-007; layout business rules and approved-lot change review  
**Implementation:** `fix/OwnerLayoutBusinessRules` (through `a52c2c8`)  
**Status:** Implemented locally; requires HTTP authorization tests and PR review before merge.

## 1. Business rules

- The caller must hold the `ParkingLotOwner` role **and** an `Owner` assignment for the target lot to read or submit its layout. A `Staff` assignment does not qualify.
- For a **Pending/Rejected parking lot**, a valid PUT layout applies immediately and responds with `200 OK`.
- For an **Approved parking lot**, a valid PUT creates a *Pending layout change request* and responds with `202 Accepted`; the active floors, zones, slots, and layout document do not change at submission time.
- At most one Pending request per parking lot. A unique partial database index enforces this rule.
- A SystemAdmin may list Pending requests and approve or reject one. Approval rechecks input validation, expected layout version, and existing operational constraints. Applying the layout and marking its request Approved happen in a single serializable PostgreSQL transaction.
- Capacity per zone is at least 1. Zone codes are unique across the *whole lot* (case-insensitive). Motorbike zones have neither individual parking slots nor slot sensors. Other established slot/geometry rules continue to apply.
- Held-quota shortage is a **warning**, not a validation failure. Compute it **after actual application**, not after submission of a Pending request. Until Booking supplies its held-reservations contract, the current reader is a stub, so `warnings` may be empty even when real reservations exist.
- This review does **not** change the parking-lot registration approval status or use the existing `ParkingLot.Approve()` / `Reject()` methods.

## 2. API endpoints

| Method | Route | Policy | Success |
| --- | --- | --- | --- |
| GET | `/api/parking/owner/lots/{lotId}/layout` | `OwnerPolicy` + Owner assignment for lot | `200 OK` |
| PUT | `/api/parking/owner/lots/{lotId}/layout` | `OwnerPolicy` + Owner assignment for lot | `200 OK` applied; `202 Accepted` pending review |
| GET | `/api/parking/admin/layout-change-requests/pending` | `SystemAdminPolicy` | `200 OK` |
| POST | `/api/parking/admin/lots/{lotId}/layout-change-requests/{requestId}/approve` | `SystemAdminPolicy` | `200 OK` |
| POST | `/api/parking/admin/lots/{lotId}/layout-change-requests/{requestId}/reject` | `SystemAdminPolicy` | `200 OK` |

Authorization comes from an authenticated JWT role claim. For the Owner endpoints, the application additionally verifies the account's lot-specific Owner assignment.

### 2.1 GET owner layout

Returns the active `ParkingLotLayout`: `lotId`, `version`, `floors` and `warnings`. `pendingReview` is null unless populated on a successful PUT submission response; the GET does not serve the Pending proposal as the active layout.

Common errors: `401` unauthenticated, `403` wrong role or not assigned Owner to the lot, `404` lot not found.

### 2.2 PUT owner layout

Request:

```json
{
  "expectedVersion": 1,
  "floors": [
    {
      "id": "11111111-1111-4111-8111-111111111111",
      "name": "Ground",
      "zones": [
        {
          "id": "22222222-2222-4222-8222-222222222222",
          "code": "MB-A",
          "vehicleGroup": "Motorbike",
          "capacity": 20,
          "hasSlotSensor": false,
          "slots": [],
          "bounds": { "x": 0, "y": 0, "width": 8, "height": 6 },
          "polygon": null
        }
      ]
    }
  ]
}
```

IDs are stable client-generated GUIDs; retain existing IDs and parent relationships on subsequent edits. This example illustrates a Motorbike zone using bounds rather than individual slots.

**Pending/Rejected lot:** `200 OK` with the current (newly applied) `ParkingLotLayout` and `warnings: []` or a quota warning.

**Approved lot:** `202 Accepted` with the **unchanged active** `ParkingLotLayout` plus `pendingReview`:

```json
{
  "lotId": "33333333-3333-4333-8333-333333333333",
  "version": 1,
  "floors": [],
  "warnings": [],
  "pendingReview": {
    "requestId": "44444444-4444-4444-8444-444444444444",
    "lotId": "33333333-3333-4333-8333-333333333333",
    "expectedVersion": 1,
    "status": "Pending",
    "requestedAtUtc": "2026-10-24T05:00:00+00:00"
  }
}
```

The sample shows an empty *active* layout for illustration only: the submitted floors are stored separately until approval.

Errors: `400` invalid layout (`parking.layout.invalid`), `401` unauthenticated, `403` owner required, `404` lot not found, `409` stale version, conflicting operational layout, or an existing Pending request.

### 2.3 GET Pending reviews (Admin)

`GET /api/parking/admin/layout-change-requests/pending`

Returns a JSON array of `LayoutReviewRequest` objects, ordered by submission time. Each contains `requestId`, `lotId`, `requestedBy`, `expectedVersion`, `requestedAtUtc`, and `proposedFloors`. This endpoint lists Pending requests across lots, not only a single lot.

Errors: `401` unauthenticated; `403` authenticated but not SystemAdmin.

### 2.4 POST approve review (Admin)

`POST /api/parking/admin/lots/{lotId}/layout-change-requests/{requestId}/approve`

No request body is required. `200 OK` returns `ReviewedLayoutChange`:

- `request`: summary with `status: "Approved"`
- `activeLayout`: applied current `ParkingLotLayout`
- `warnings`: zero or one `CapacityWarning` entries following the applied change

Validation and consistency checks run again at approval time. Errors include `400` invalid proposal, `404` request missing, `409` already reviewed / stale version / layout conflict / lot no longer approved.

### 2.5 POST reject review (Admin)

`POST /api/parking/admin/lots/{lotId}/layout-change-requests/{requestId}/reject`

Request:

```json
{ "reason": "The proposed capacity does not meet requirements." }
```

`reason` must be 1–500 non-whitespace characters. `200 OK` returns `ReviewedLayoutChange` with `request.status: "Rejected"`, `activeLayout: null`, and `warnings: []`. The active layout is unchanged. Errors include `400` invalid reason, `404` request not found and `409` already reviewed / conflicting state.

## 3. Standard error behavior

API errors use the existing `application/problem+json` business error convention. The exact message/body depends on `BusinessProblem.Create`; use the documented error codes rather than parsing human-readable messages.

| Code | Typical meaning |
| --- | --- |
| `parking.layout.invalid` | Proposed layout fails field/business validation |
| `parking.layout.lot_not_found` | Parking lot does not exist |
| `parking.layout.owner_required` | Actor is not Owner of this lot |
| `parking.layout.stale_version` | Layout version differs from `expectedVersion` |
| `parking.layout.conflict` | Existing IDs/operational constraints prevent application |
| `parking.layout_review.pending_exists` | Lot already has a Pending request |
| `parking.layout_review.not_found` | Review request not found in this lot |
| `parking.layout_review.already_reviewed` | Request is no longer Pending |
| `parking.layout_review.lot_not_approved` | Lot cannot enter this review decision state |
| `parking.layout_review.invalid_decision` | Invalid approve/reject input |
| `parking.layout_review.conflict` | Concurrent modification detected |

## 4. Verification checklist before PR

- Unauthenticated callers receive `401` on both Owner and Admin routes.
- `Customer`, `Staff`, and `ParkingLotOwner` receive `403` on Admin routes; `SystemAdmin` can reach those handlers.
- Non-Owner role receives `403` on Owner routes. An Owner-role user lacking the specific lot assignment receives `403` from the handler.
- Approved lot PUT returns `202` without changing active layout; Admin rejection preserves it, and approval applies it exactly once.
- Stale version or failed database transaction does not change active layout or incorrectly mark a request Approved.
- Pending/Rejected lot PUT continues to return `200`; warnings do not block successful application.
- Run WebAPI build, Unit Tests, Integration Tests, `dotnet ef migrations has-pending-model-changes` and `git diff --check`.
