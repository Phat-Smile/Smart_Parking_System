# Payment wallet, fee and audit log API

Contract for the web app (Demo 1 step 14, staff fee on step 17, audit log on step 21). Every route goes through
the Gateway at `http://localhost:5000`.

## Response shape

- Success: the DTO itself, with no envelope. The old `{ result, isSuccess, statusCode, message }` shape is gone.
- Business error: `application/problem+json` with `errorCode`, like the other services:

```json
{
  "type": "about:blank",
  "title": "Top-up not found.",
  "status": 404,
  "instance": "/api/payment/wallets/top-up/T404",
  "errorCode": "payment.topup_not_found",
  "traceId": "…",
  "correlationId": "…"
}
```

- Request validation (FluentValidation) returns a 400 validation problem with `errors`.
- 401 without a token and 403 for a wrong role come from the shared web foundation.
- `GET /api/payment/vnpay/ipn` is not part of this contract: VNPay reads its `RspCode` answer.

## Routes

| Method | Route | Who | Success |
| --- | --- | --- | --- |
| GET | `/api/payment/wallets/me` | signed in | 200 `WalletBalance` |
| POST | `/api/payment/wallets/top-up` | signed in; header `Idempotency-Key` (8–64 characters) | 201 new top-up, 200 replay of the same key |
| GET | `/api/payment/wallets/top-up/{topupId}` | signed in, owner of the top-up | 200 `TopupStatus` |
| GET | `/api/payment/wallets/transactions?page&pageSize&type&fromUtc&toUtc` | signed in customer | 200 `TransactionsPage` |
| GET | `/api/payment/parking-sessions/{sessionId}/fee` | role `Customer`, `Staff` or `LotOwner` | 200 `SessionFee`, `Cache-Control: no-store` |
| GET | `/api/payment/admin/audit-logs?page&pageSize&actionType&parkingLotId&actorId&fromUtc&toUtc` | `SystemAdmin` | 200 `AuditLogsPage` |

## Bodies

`WalletBalance`

```json
{
  "walletId": "…", "balance": 150000, "currency": "VND", "isDemo": true, "demoNotice": "…",
  "limits": { "minTopUp": 10000, "maxTopUp": 5000000, "maxBalance": 10000000 },
  "gatewayMode": "Sandbox",
  "pendingTopUps": [ { "topupId": "…", "amount": 100000, "createdAtUtc": "…" } ]
}
```

Top-up request: `{ "amount": 100000 }` (whole VND within the limits). Response `TopupResult`:

```json
{ "topupId": "…", "amount": 100000, "status": "Pending", "paymentUrl": "https://sandbox.vnpayment.vn/…",
  "createdAtUtc": "…", "expiresAtUtc": "…", "isReplay": false }
```

The web app sends a new `Idempotency-Key` for each new top-up and the same key when it retries the same one,
then opens `paymentUrl`. After VNPay returns, it reads `GET /top-up/{topupId}`.

`TopupStatus`: `{ "topupId", "amount", "status", "createdAtUtc", "completedAtUtc", "expiresAtUtc" }`.

`TransactionsPage`: `{ "items": [ { "entryId", "createdAtUtc", "entryType", "amount", "balanceDelta", "affectsBalance",
"balanceAfter", "reason", "referenceType", "referenceId" } ], "page", "pageSize", "totalItems", "totalPages" }`.

`SessionFee`: `{ "sessionId", "plateNumber", "vehicleType", "entryTimeUtc", "calculatedAtUtc", "durationMinutes",
"pricingPolicyId", "calculationMethod", "totalFee", "depositAmount", "amountDue" }`.

`AuditLogsPage`: `{ "items": [ { "id", "occurredAtUtc", "actionType", "sourceService", "actorId", "actorEmail",
"actorRole", "parkingLotId", "targetType", "targetId", "reason", "ipAddress", "details" } ], "page", "pageSize",
"totalItems", "totalPages" }`.

## Errors

| Route | Status | `errorCode` |
| --- | --- | --- |
| wallets/me | 400 | `payment.invalid_user` |
| wallets/top-up | 409 | `payment.idempotency_key_reused` (same key, different amount), `payment.topup_expired` (the key's top-up expired) |
| wallets/top-up | 400 | `payment.balance_limit_exceeded`, `payment.invalid_user`, validation problem (amount, `Idempotency-Key`) |
| wallets/top-up/{topupId} | 404 | `payment.topup_not_found` (also for another user's top-up) |
| wallets/top-up/{topupId} | 400 | `payment.invalid_topup_id` |
| wallets/transactions | 403 | `payment.customer_only` |
| wallets/transactions | 400 | `payment.invalid_page`, `payment.invalid_page_size`, `payment.invalid_entry_type`, `payment.invalid_date_range` |
| parking-sessions/{id}/fee | 400 | `payment.session_id_invalid` |
| parking-sessions/{id}/fee | 403 | `payment.session_access_denied` |
| parking-sessions/{id}/fee | 404 | `payment.session_not_found` |
| parking-sessions/{id}/fee | 409 | `payment.session_not_active` |
| parking-sessions/{id}/fee | 503 | `payment.parking_service_unavailable` |
| admin/audit-logs | 400 | validation problem or the query's error code |

## Changes from the previous shape

- Success bodies lost the envelope: read `balance` instead of `result.balance`.
- Errors carry `errorCode`; the HTTP status of each error is unchanged, except `payment.idempotency_key_reused`
  and `payment.topup_expired`, which were 400 and are now 409.
