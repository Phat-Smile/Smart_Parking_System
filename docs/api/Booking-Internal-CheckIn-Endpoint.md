# Booking Internal Check-In Endpoint

## Overview
This document specifies the internal machine-to-machine check-in endpoint provided by the Booking service for the Parking service (`BookingCheckInHttpClient`, US-PARK-004 / MF-08).

Following the security remediation (P0 Bug Fix), the check-in route was moved out of the public `/api/booking/**` group and mapped exclusively under `/internal/**`. The API Gateway only routes `/api/**` paths and does not route `/internal/**`. The endpoint is protected by a dedicated service key (`ArrivalIntegration:Key`).

---

## Endpoint Specification

* **Method:** `POST`
* **Route:** `/internal/booking/reservations/check-in`
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
  "entryTime": "2026-11-10T08:00:00Z",
  "sessionId": "44444444-4444-4444-4444-444444444444"
}
```

### Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `lotId` | UUID | Yes | ID of the parking lot where vehicle entered. |
| `plateNumber` | string | Yes | License plate of the arriving vehicle. |
| `entryTime` | ISO 8601 string | No | Vehicle entry timestamp (defaults to server UtcNow if null). |
| `sessionId` | UUID | No | Parking session ID created by Parking service upon entry. |

---

## Response Body

### 1. Active Confirmed Reservation Found (HTTP 200 OK)
When a matching reservation in `Confirmed` state is found within the valid arrival window:
```json
{
  "hasReservation": true,
  "reservationId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
  "userId": "22222222-2222-2222-2222-222222222222",
  "vehicleId": "33333333-3333-3333-3333-333333333333",
  "message": "Check-in thành công. Trạng thái đặt chỗ đã chuyển sang Used."
}
```
* The reservation transitions from `Confirmed` to `Used`.
* `SessionId` is linked to the reservation.

### 2. No Active Reservation Found (HTTP 200 OK)
When no active confirmed reservation matches the lot, plate, and entry time window:
```json
{
  "hasReservation": false,
  "reservationId": null,
  "userId": null,
  "vehicleId": null,
  "message": "Không tìm thấy đặt chỗ đang Confirmed trong khung giờ hợp lệ cho biển số này tại bãi."
}
```

### 3. Error Responses
* **HTTP 400 Bad Request:** Payload validation failed (e.g. invalid UUID, empty plate number).
* **HTTP 403 Forbidden:** Missing or mismatched `X-Arrival-Service-Key`.
* **HTTP 503 Service Unavailable:** Server configuration `ArrivalIntegration:Key` is missing.

---

## Client Implementation Guidelines (Parking Service / BE-4)

In `Parking.Infrastructure/Booking/BookingCheckInHttpClient.cs`:
1. **Update Request URI:**
   Change request URI from:
   ```csharp
   $"{baseUrl}/api/booking/reservations/check-in"
   ```
   to:
   ```csharp
   $"{baseUrl}/internal/booking/reservations/check-in"
   ```
2. **Add Header:**
   Add `X-Arrival-Service-Key` header with key from `ArrivalIntegration:Key` (or configuration options):
   ```csharp
   request.Headers.Add("X-Arrival-Service-Key", arrivalKey);
   ```

