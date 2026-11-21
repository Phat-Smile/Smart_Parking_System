# Admin parking submission dates and trends

All routes require the `parking-admin` policy (SystemAdmin role with MFA).

## Admin parking lot list

```http
GET /api/parking/admin/lots?status=Pending&page=1&pageSize=20&fromDate=2026-11-01&toDate=2026-11-20
```

`status`, `page`, `pageSize`, `fromDate`, and `toDate` are optional. Date filters use the `yyyy-MM-dd` format and the Vietnam calendar (`Asia/Ho_Chi_Minh`, UTC+07:00). Each supplied date includes that entire local calendar day; the database query uses a half-open UTC range so the end boundary is exact. Either date can be supplied independently. Invalid values and a `fromDate` later than `toDate` return HTTP 400 validation ProblemDetails. Filtering happens before paging, so `totalCount` describes the filtered rows.

Each list item retains its existing fields and adds `submittedAtUtc`, for example:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Bãi xe Bitexco",
  "approvalStatus": "Pending",
  "submittedAtUtc": "2026-11-15T08:30:00Z"
}
```

## Daily analytics trends

```http
GET /api/parking/admin/analytics/trends?period=7days
GET /api/parking/admin/analytics/trends?period=30days
```

`period` defaults to `7days`; only `7days` and `30days` are accepted. Results use Vietnam calendar days, include today in Vietnam, and fill empty days with zero. All three arrays have the same length and chronological order. Timestamps remain stored and returned in UTC; conversion to Vietnam time is only for calendar grouping and date filtering.

```json
{
  "labels": ["11/11", "12/11", "13/11"],
  "newSubmissions": [2, 5, 3],
  "approvedCount": [1, 3, 2]
}
```

`approvedCount` counts persisted `parking_lot.approved` audit events by their recorded UTC occurrence day. It does not group approved lots by submission date. The current review workflow only permits a transition from Pending, so each lot can contribute at most one approval event. Legacy approvals are counted only when their audit event exists.

## Legacy submission dates

The repository has no reliable historical submission timestamp for pre-existing parking lots. The migrations therefore mark those rows with `0001-01-01T00:00:00Z` (UTC `DateTime.MinValue`) as an explicit unknown-date sentinel, then make the column required and set the database default to `NOW()` for later inserts. The sentinel is **not** the actual submission date. It is outside current analytics windows and is returned as `submittedAtUtc: null` so the UI does not present it as a real date. Newly registered lots receive the application clock's UTC instant.
