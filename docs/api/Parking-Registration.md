# Parking Registration API — US-OWNER-001 (S2)

## Scope and traceability

- Story: `US-OWNER-001` — Chủ bãi đăng ký bãi đỗ.
- SRS: `FR-OWN-001` / `FR-PARK-009`; MF7 (đăng ký và duyệt bãi).
- Owner: Parking Service / BE-2.
- A new registration is always **Pending** and not displayed by the public catalog or detail endpoints.
- Admin approval/rejection is a separate S2 story: `US-ADMIN-002`.
- Existing `TECH-02` sample lots have no owner account; the new `owner_user_id` column is nullable for backward compatibility.
- **Open dependency:** account-owner approval belongs to Identity / BE-1. This API currently requires the signed `ParkingLotOwner` role, but cannot independently check a separate owner-account approval status because Identity does not expose that status yet. Do not treat this as approval of the owner account.

## Endpoint

`POST /api/parking/lots`

JWT required: `role=ParkingLotOwner`, `sub=<authenticated user's UUID>`.
The request intentionally does **not** include `ownerUserId`: the server derives it from the validated token.
Guest: 401. Authenticated non-owner (Customer, Staff, SystemAdmin): 403.

Request:
```json
{
  "name": "Bãi xe Nguyễn Huệ",
  "address": "10 Nguyễn Huệ, Quận 1, TP.HCM",
  "latitude": 10.774251,
  "longitude": 106.703612,
  "type": "Outdoor"
}
```

Parking lot types: `MultiStorey`, `Underground`, `Outdoor` (case-insensitive input).
`name`: nonempty, max 200 chars after trimming; `address`: nonempty, max 500.
Coordinates are required, latitude [-90,90], longitude [-180,180]. Invalid data returns 400.
Strings containing only spaces, invalid enum names, missing coordinates and numeric enum representations are rejected.

On success: **201 Created** with no `Location` header (public GET intentionally returns 404 while pending):
```json
{
  "id": "a-new-generated-uuid",
  "ownerUserId": "uuid-from-jwt-sub",
  "name": "Bãi xe Nguyễn Huệ",
  "address": "10 Nguyễn Huệ, Quận 1, TP.HCM",
  "latitude": 10.774251,
  "longitude": 106.703612,
  "type": "Outdoor",
  "approvalStatus": "Pending"
}
```

The API does not accept a client-provided `approvalStatus` or `ownerUserId`.
No automatic `Approved` transition is allowed.

## Persistence

New migration `20261013140000_AddParkingLotOwnerRegistration`:
- `parking.parking_lots.owner_user_id uuid NULL` (preserves existing S1 seed and catalogue rows).
- Index `ix_parking_lots_owner_user_id`.
- Ownership refers to `identity.users.id` by UUID only; no cross-service FK.

Apply Parking EF migrations explicitly after backing up local DB. API startup does **not** auto-migrate.
See `docs/development/Database-Migrations.md`.

## Verification

From repo root:
```bash
dotnet test tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj
dotnet test tests/Parking/Parking.IntegrationTests/Parking.IntegrationTests.csproj
dotnet build src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj
```

Integration tests require Docker/Testcontainers PostgreSQL.
Check 201/400/401/403 with JWTs in an authorized local environment.

## Not implemented in this story

- `US-ADMIN-002`: approving/rejecting lots, rejection reason, resubmission.
- `US-OWNER-002`: opening hours.
- Owner-specific pending/rejected lot detail/list APIs.
- Owner account approval check pending Identity contract, audit trail, attachments / lot layouts.
- Verification of physical addresses across different owners (business contract not yet specified).

## Duplicate registration guard (PR #73 review)

For the same authenticated owner, a second registration with the **same trimmed name**
returns **409 Conflict** (`errorCode: parking.lot_duplicate`), without creating a new
Pending lot. A different owner may register a lot with the same name.

The check is enforced twice: an existence query for clear feedback, plus a PostgreSQL
unique index `ux_parking_lots_owner_user_id_name` on `(owner_user_id, name)` for
concurrent submissions. The existing owner ID index is retained. Legacy rows whose
`owner_user_id` is NULL are unaffected by PostgreSQL's default NULL-distinct uniqueness.
Names are compared case-sensitively by the database; case-insensitive equivalence is
not part of this review fix. These semantics need product confirmation if two distinct
locations owned by one account are legitimately allowed to share a name.

Create and apply a **new** EF migration for the unique index; do not edit
`20261013140000_AddParkingLotOwnerRegistration`, which has already been applied.
