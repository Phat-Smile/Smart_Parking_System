# Booking Internal No-Suitable-Slot Cancellation Endpoint

## Overview
This document specifies the internal machine-to-machine endpoint provided by the Booking service for the Parking service (`US-PARK-004`, BR-02.f, context point 35).

When a reserved car arrives at the parking gate and the lot has no suitable slot available:
- **Decision (team, 2026-11-17):** Parking counts Available slots **before** check-in. If there is no suitable slot, Parking does **not** check in the reservation (it remains `Confirmed`). Parking calls this endpoint to cancel the reservation and issue a 100% refund back to the customer's wallet.
- The route is mapped exclusively under `/internal/booking/reservations/no-suitable-slot` (not routed by API Gateway) and protected by the dedicated service key `ArrivalIntegration:Key`.

---

## Endpoint Specification

* **Method:** `POST`
* **Route:** `/internal/booking/reservations/no-suitable-slot`
* **Base URL (internal Compose network):** `http://booking-api:8080`
* **Visibility:** Internal service-to-service only (not exposed through API Gateway).

### Headers

| Header | Required | Description |
|---|---|---|
| `Content-Type` | Yes | `application/json` |
| `X-Arrival-Service-Key` | Yes | Dedicated service key matching configuration `ArrivalIntegration:Key`. |

### Security & Authentication Rules
1. **Dedicated Key Validation:**
   - The value in `X-Arrival-Service-Key` is validated against `ArrivalIntegration:Key` using constant-time comparison (`CryptographicOperations.FixedTimeEquals`).
   - If `ArrivalIntegration:Key` is not configured on the server, returns **`503 Service Unavailable`**.
   - If `X-Arrival-Service-Key` is missing, empty, or invalid, returns **`403 Forbidden`**.
2. **Anonymous / Machine Call:**
   - No JWT bearer token is required.
   - The endpoint is marked with `.AllowAnonymous()` and `.ExcludeFromDescription()`.

---

## Request Body

```json
{
  "lotId": "11111111-1111-1111-1111-111111111111",
  "plateNumber": "51A-123.45",
  "arrivedAt": "2026-11-18T08:00:00Z"
}
```

### Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `lotId` | UUID | Yes | ID of the parking lot where vehicle arrived. |
| `plateNumber` | string | Yes | License plate of the arriving vehicle (normalized internally). |
| `arrivedAt` | ISO 8601 string | Yes | Arrival timestamp at the gate. |

---

## Response Body

### 1. Active Confirmed Reservation Cancelled (HTTP 200 OK)
When an active `Confirmed` reservation matches the lot, plate, and arrival window `[WindowStartAt - 15m, Cutoff)`:
```json
{
  "cancelled": true,
  "reservationId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
}
```
* Status transitions from `Confirmed` to `Cancelled`.
* `CancelReason` is set to `NoSuitableSlot`.
* `IsNoShow` is `false`.
* `PrepaidOutcome` is set to `Refunded` (or `null` pending worker reconciliation if payment client call times out).
* Outbox event `booking.reservation-cancelled.v1` is enqueued with `refundAmount = PrepaidAmount`, `forfeitedAmount = 0`.

### 2. Idempotent Retry / Already Cancelled (HTTP 200 OK)
If the request is repeated and the reservation was already cancelled with reason `NoSuitableSlot`:
```json
{
  "cancelled": true,
  "reservationId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
}
```
* No duplicate refund or duplicate outbox event is issued.

### 3. No Matching Active Reservation Found (HTTP 200 OK)
When no matching active reservation is found within the arrival window:
```json
{
  "cancelled": false,
  "reservationId": null
}
```

### 4. Error Responses
* **HTTP 400 Bad Request:** Payload validation failed (e.g. empty `lotId`, empty `plateNumber`, or default `arrivedAt`).
* **HTTP 403 Forbidden:** Missing or mismatched `X-Arrival-Service-Key`.
* **HTTP 503 Service Unavailable:** Server configuration `ArrivalIntegration:Key` is missing.

---

## Business Rules & Invariants

1. **100% Refund (BR-02.f):**
   - The prepaid amount (`PrepaidAmount`) is refunded 100% back to the user's wallet.
   - Immediate refund call to Payment service has a 1-second timeout. If it times out or fails temporarily, the `ReservationPaymentReconciliationWorker` automatically finishes the refund asynchronously.
2. **Never a No-Show (BR-02.f / BR-03.f):**
   - `IsNoShow` is strictly `false`.
3. **Used Invariant (BR-03.c / Decision D2):**
   - A `Used` reservation can never be cancelled. If a car has already checked in, this endpoint does not cancel `Used` reservations.
