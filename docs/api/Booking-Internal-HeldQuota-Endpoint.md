# Booking Internal Held-Quota Endpoint

## Overview
This document specifies the internal machine-to-machine held-quota endpoint provided by the Booking service for the Parking service (`HeldQuotaProvider`, US-RES-009 / FR-RES-002, MF-05 / MF-09A, contract #4).

The endpoint allows Parking to query the number of active car reservations currently holding spots for an arriving time window, enabling Parking to calculate real-time walk-in capacity (`availableSlots − heldQuota`) and protect reserved spots from walk-in vehicles.

The route is mapped exclusively under `/internal/**` and is protected by a dedicated machine service key (`X-Arrival-Service-Key`, matching `ArrivalIntegration:Key`). The API Gateway only routes `/api/**` paths and does not expose `/internal/**`.

---

## Endpoint Specification

* **Method:** `GET`
* **Route:** `/internal/booking/held-quota`
* **Base URL (internal Compose network):** `http://booking-api:8080`
* **Visibility:** Internal service-to-service only (not exposed through API Gateway).

### Headers

| Header | Required | Description |
|---|---|---|
| `X-Arrival-Service-Key` | Yes | Dedicated service key matching configuration `ArrivalIntegration:Key`. |

### Query Parameters

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `lotId` | UUID | Yes | — | Unique identifier of the parking lot. |
| `arrivalAt` | ISO 8601 string | Yes | — | Arrival / evaluation timestamp (must be within `[now - 1m, now + 30d]`). |
| `vehicleGroup` | string | No | `Car` | Vehicle group to check. Allowed values: `Car`, `Motorbike` (case-insensitive). Any other value returns HTTP 400. |

### Security & Authentication Rules
1. **Dedicated Key Validation:**
   - The value in `X-Arrival-Service-Key` is validated against `ArrivalIntegration:Key` using constant-time comparison (`CryptographicOperations.FixedTimeEquals`).
   - If `ArrivalIntegration:Key` is not configured on the server, returns **`503 Service Unavailable`**.
   - If `X-Arrival-Service-Key` is missing, empty, or invalid, returns **`403 Forbidden`**.
2. **Anonymous / Machine Call:**
   - No JWT bearer token is required.
   - The endpoint is marked with `.AllowAnonymous()` and `.ExcludeFromDescription()`.

---

## Business Logic & Counting Rules

### 1. Active Held Quota Definition (BR-02.a, BR-02.e)
Held quota at evaluation time $t$ equals the number of reservations meeting all of the following conditions:
- **Lot match:** `r.LotId == lotId`
- **Vehicle Group:** `r.VehicleGroup == VehicleGroup.Car` (motorbikes never take reservations per BR-01; queries for `Motorbike` immediately return `heldQuota: 0` without DB lookup).
- **Status in force:**
  - `Confirmed`, OR
  - `TemporaryHold` where `TempHoldExpiresAt > now` (unexpired deposit payment hold).
- **Vehicle has not entered:** Status is NOT `Used`.
- **Not terminal / inactive:** Status is NOT `Cancelled` (regardless of cancel reason) and NOT `Expired`.
- **Hold Window Overlap (Half-Open Interval $[WindowStartAt, HoldEndAt)$):**
  $$\text{WindowStartAt} \le t < \text{HoldEndAt}$$
  - For **HoldWhileDriving (HWD)**: $[A - 30\text{m}, A + 16\text{m})$
  - For **Scheduled**: $[A - 30\text{m}, A + 16\text{m})$, where $A$ is `AppointmentTime`.
    *(Note: For scheduled bookings, the held-quota window ends strictly at `HoldEndAt` ($A + 16\text{m}$), NOT at the end of the booked duration. The longer duration window is used only for the ceiling check (BR-02.c i). A booking for tomorrow never reduces today's walk-in places).*

### 2. Walk-In Calculation in Parking Service (BR-02.d)
$$\text{Walk-In Places} = \max(0, \text{Available Slots} - \text{Held Quota})$$

- If $\text{Available Slots} = 0$: Display **"Đã đầy"**.
- If $\text{Available Slots} > 0$ but $\text{Walk-In Places} = 0$: Display **"Hết chỗ cho xe không đặt trước"** (all remaining physical slots are reserved for arriving bookings).

---

## Response Body

### 1. Successful Query (HTTP 200 OK)

```json
{
  "lotId": "11111111-1111-1111-1111-111111111111",
  "arrivalAt": "2026-11-10T10:00:00Z",
  "vehicleGroup": "Car",
  "heldQuota": 3
}
```

#### Fields

| Field | Type | Description |
|---|---|---|
| `lotId` | UUID | ID of the evaluated parking lot. |
| `arrivalAt` | ISO 8601 string | Timestamp at which the held quota was evaluated. |
| `vehicleGroup` | string | Evaluated vehicle group (`Car` or `Motorbike`). |
| `heldQuota` | integer | Number of active reservations holding a place at `arrivalAt`. |

### 2. Motorbike Query (HTTP 200 OK)
When `vehicleGroup=Motorbike`:
```json
{
  "lotId": "11111111-1111-1111-1111-111111111111",
  "arrivalAt": "2026-11-10T10:00:00Z",
  "vehicleGroup": "Motorbike",
  "heldQuota": 0
}
```

### 3. Error Responses
- **HTTP 400 Bad Request:**
  - `lotId` is empty (`00000000-0000-0000-0000-000000000000`).
  - `arrivalAt` is invalid, in the past ($< \text{now} - 1\text{m}$), or too far in the future ($> \text{now} + 30\text{d}$).
  - `vehicleGroup` is invalid (e.g. `Truck`, `Van`).
- **HTTP 403 Forbidden:** Missing, empty, or mismatched `X-Arrival-Service-Key`.
- **HTTP 503 Service Unavailable:** `ArrivalIntegration:Key` is not configured on the Booking server.

---

## Worked Example

Suppose Lot A has:
- Physical car capacity: 10
- Occupied car slots: 5
- $\text{Available Slots} = 10 - 5 = 5$

Active reservations for Lot A:
1. Res 1: HWD, $A = 10:00$ (Hold window: $[09:30, 10:16)$), Confirmed.
2. Res 2: Scheduled, $A = 10:15$, Duration 2h (Hold window: $[09:45, 10:31)$), Confirmed.
3. Res 3: Scheduled, $A = 10:30$, Duration 3h (Hold window: $[10:00, 10:46)$), Confirmed.
4. Res 4: Scheduled for tomorrow $10:00$, Confirmed.

At time $t = 10:05$:
- Res 1: $09:30 \le 10:05 < 10:16$ $\rightarrow$ **Counted** (1)
- Res 2: $09:45 \le 10:05 < 10:31$ $\rightarrow$ **Counted** (2)
- Res 3: $10:00 \le 10:05 < 10:46$ $\rightarrow$ **Counted** (3)
- Res 4: Tomorrow $\rightarrow$ **Not Counted**

Total `heldQuota` = 3.

**Parking Service Calculation at Entry Gate:**
$$\text{Walk-In Places} = \max(0, 5 - 3) = 2$$
$\rightarrow$ A walk-in car without a booking is permitted to enter because 2 walk-in places remain.

If 2 more walk-in cars enter, $\text{Available Slots} = 3$, $\text{heldQuota} = 3$:
$$\text{Walk-In Places} = \max(0, 3 - 3) = 0$$
$\rightarrow$ The entrance sign displays: **"Hết chỗ cho xe không đặt trước"**.
A 4th walk-in car is rejected at the gate. If Res 1 arrives, its license plate matches a Confirmed booking; check-in succeeds, transitioning Res 1 to `Used`.

---

## Client Guidelines for Parking Service (BE-4)

When implementing the client in `Parking.Infrastructure` (replacing `StubHeldQuotaReader`):
1. **Endpoint Call:**
   `GET /internal/booking/held-quota?lotId={lotId}&arrivalAt={now}&vehicleGroup={vehicleGroup}` with header `X-Arrival-Service-Key`.
2. **Gate Decisions:**
   Call with `arrivalAt = DateTimeOffset.UtcNow` when evaluating whether to admit a walk-in vehicle at the entrance gate.
3. **Resilience & Fallback:**
   Follow the standard `HeldQuotaProvider` resilience rules:
   - **Timeout:** 2-second deadline.
   - **Cache:** 60-second in-memory cache to prevent blocking the gate camera / sensor loop.
   - **Failure fallback:** If Booking service does not respond within deadline and cache is expired, fail safe: refuse unreserved walk-in entries when available capacity is low.

