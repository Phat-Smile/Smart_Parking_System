# US-ADMIN-002 — Review parking lot applications

Only a JWT with claim `role=SystemAdmin` can review a parking lot. Tokens with `Customer`, `ParkingLotOwner` and `Staff` cannot. The `sub` claim must be a valid account UUID for auditing.

## Approve

`POST /api/parking/admin/lots/{parkingLotId}/approve` — no request body.

200 OK:

```json
{"parkingLotId":"11111111-1111-1111-1111-111111111111","approvalStatus":"Approved","rejectionReason":null}
```

## Reject

`POST /api/parking/admin/lots/{parkingLotId}/reject`

```json
{"reason":"Missing required fire safety documentation"}
```

200 OK:

```json
{"parkingLotId":"11111111-1111-1111-1111-111111111111","approvalStatus":"Rejected","rejectionReason":"Missing required fire safety documentation"}
```

## Errors and invariants

- 400: invalid lot id or rejection reason (trimmed 1–500 characters). Invalid JSON also yields 400.
- 401: unauthenticated request.
- 403: authenticated caller without `SystemAdmin` role or without a valid UUID subject.
- 404 / `parking.lot_not_found`: unknown lot id.
- 409 / `parking.lot_already_reviewed`: lot was already Approved or Rejected, including a concurrent review attempt.
- Only `Pending` may become `Approved` or `Rejected`. Owner resubmission is a separate workflow.
- Rejected lots retain the rejection reason in `parking.parking_lots.rejection_reason`.
- Decisions record actor, timestamp and reason in `parking.audit_logs` atomically with the status update.
- Existing public catalog/detail queries only expose Approved lots; rejected lots are never public.

**Migration:** after applying this change, run `dotnet ef migrations add AddParkingLotRejectionReason` on the Parking context. Do not modify previously applied migrations. If PR #73 merges, integrate its model/migration changes and regenerate or rebase your migration snapshot before this PR is merged.
