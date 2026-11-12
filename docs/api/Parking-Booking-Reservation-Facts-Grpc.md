# Parking to Booking: reservation facts and price list (internal gRPC)

Internal contract between Parking (server) and Booking (client). It is not exposed through the Gateway. File: `contracts/grpc/parking_availability.proto`, service `ParkingAvailabilityContract`. Port 8081 of `parking-api`, reachable only inside the Compose network. The caller sends `X-Service-Key` when `ServiceAuth:ServiceKey` is configured.

Both rpcs were added next to `GetAvailableSlotCount`; no existing field changed.

## GetLotReservationInfo

Request: `lot_id` (UUID "D" format).

| Field | Meaning |
|---|---|
| `valid_capacity` | BR-02.a: car slots not in Maintenance or Unavailable. Motorbike zones are not counted. |
| `reservation_ratio_percent` | BR-02.b, 0 to 100. The lot's setting, 50 when the owner has not set one. |
| `available_slot_count` | Car slots whose effective status is Available now (same value as `GetAvailableSlotCount`). |
| `reservation_enabled` | false when the owner stopped taking reservations. True when never configured. |
| `deposit_amount` | VND, whole amount. The lot's setting, 20000 when the owner has not set one. |

## GetLotPricing

Request: `lot_id`, `vehicle_group` (`Car` or `Motorbike`).

| Field | Meaning |
|---|---|
| `price` | VND, whole amount, greater than 0: the price of one entry or one block. |
| `billing_mode` | `PerEntry` or `Block`. |
| `block_minutes` | 0 for `PerEntry`. |
| `overnight_fee` | VND, whole amount, 0 when the lot has none. |
| `policy_version` | Version of the latest price list for this vehicle group. |

Booking computes the prepayment itself (BR-10.a); Parking only states the price list. The overnight time mark is not part of the contract yet: Booking uses its default (00:00, UTC+7).

## Status codes

| Code | When |
|---|---|
| `INVALID_ARGUMENT` | `lot_id` is not a canonical UUID, or `vehicle_group` is not `Car` or `Motorbike`. |
| `NOT_FOUND` | The lot does not exist or is not approved (the two cases are not told apart). |
| `FAILED_PRECONDITION` | `GetLotPricing`: the lot has no price list for this vehicle group, or the price list holds a fractional amount. `GetLotReservationInfo`: the configured deposit is fractional. |

## Booking side

`ParkingLotCapacityGrpcProvider` and `ParkingPricingGrpcProvider` (Booking.WebAPI/Grpc) replace the `Unconfigured*` providers. They are fail-closed: any gRPC failure, timeout, or abnormal figure (negative capacity, ratio over 100, price 0, unknown billing mode) becomes `booking.capacity.info_unavailable` (HTTP 503 on the hold route). A default capacity, ratio, deposit or price is never invented.

## Known limits

- `available_slot_count` follows `Availability:Provider`; with `Stub` (the Development default in Compose) it is always 10.
- Parking has no overnight time mark; the contract will gain a field when the lot can configure one.
- The latest price list by version is used, as in the public pricing route.
