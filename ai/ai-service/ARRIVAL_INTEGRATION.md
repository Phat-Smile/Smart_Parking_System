# US-AI-002: Arrival availability (phase 1, session proxy)

This follows the verified 2026-11-06 `origin/develop` ZIP snapshot (`40ba632`) and requires the separately applied `BE2_S3_ArrivalAvailability_Baseline.patch` first.

## Data path

`GET /api/parking/lots/{lotId}/arrival-availability?arrivalAt=<ISO8601+offset>&vehicleType=Car|Motorbike` (anonymous, approved lots only) → Parking session proxy snapshots → Booking `/internal/booking/held-quota` → AI `/internal/ai/arrival-availability`. Internal calls carry `X-Arrival-Service-Key` (the Booking lookup is skipped for Motorbike) from server config; `ARRIVAL_API_KEY` must be nonempty, otherwise the API returns 503. The internal AI path is not to be published through the public gateway.

* Booking reads current active `Confirmed` or unexpired `TemporaryHold` reservations whose half-open booking windows include `arrivalAt`. It is a conservative estimate of quota held now; it cannot guarantee which holds will confirm at arrival.
* Parking derives 15-minute historical proxy values from actual recorded sessions on observed days in the last 28 days and current configured physical capacity. It is **not** historical sensor occupancy or reliable sampling; days with no known sessions are not fabricated. Current overrides / maintenance may differ from the past.
* AI uses the existing `lot_weekday_15m` / fallback baseline. `estimated=true` always. A `sampleCount=0` returns explicit `insufficientData=true`; clients must display the caveat and must never treat this as permission to book or enter.
* Transport or service-key failures fail with HTTP 503, **not** silent `heldQuota=0` or a fake probability.

## Local Docker setup

Set `ARRIVAL_API_KEY` to a high-entropy demo-only value in ignored `.env`, then rebuild/start `parking-api`, `booking-api`, and `ai-service`. The server-side Compose values `BookingService__BaseUrl`, `AiService__BaseUrl`, and `ArrivalIntegration__Key` / `ARRIVAL_API_KEY` are mapped automatically. Never use the key as a frontend environment variable. Existing `SERVICE_KEY`/`ServiceAuth` configuration remains unchanged; if globally enabled, the Parking internal Booking client also sends that existing `X-Service-Key` header. The dedicated key is never sent to the browser.

**Not covered:** periodic persisted 15-minute sensor snapshots; full Booking/AI contract integration tests on live DB; any UI work; calibrated probability or E2E validation. Do not label this implementation complete until these are verified.
