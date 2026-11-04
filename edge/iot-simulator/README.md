# US-MAP-006 — IoT Simulator

The simulator is **opt-in** and sends nothing unless `DEVICE_SIMULATION_ENABLED=true`.
Set the following **only for a dedicated development/demo lot**, with slot codes
that already exist in Parking. The simulator does not read or modify Parking's
DB and **cannot verify** that a configured lot or slot actually exists.

```dotenv
DEVICE_SIMULATION_ENABLED=true
SIM_LOT_ID=<existing demo parking lot UUID>
SIM_SLOT_CODES=A-01,A-02
SIM_INTERVAL_SECONDS=5
SIM_MODE=slot
```

- `slot` (default): publishes successive alternating `Occupied` / `Empty`
  car-slot sensor events on `lot/{lotId}/slot/{slotCode}` (one per tick).
- `full-cycle`: **add** `SIM_PLATE_NUMBER=30A12345` and optionally
  `SIM_CONFIDENCE=0.97`. Publishes four events per car slot in sequence:
  **gate Entry → slot Occupied → slot Empty → gate Exit**, and repeats.
  Gate topic convention is **provisional**:
  `lot/{lotId}/gate/entry` and `lot/{lotId}/gate/exit`.

Payload fields use JSON camelCase: all events include `eventId` (a new UUID),
`deviceId`, `lotId`, and UTC `eventTime`. Sensor payloads add `slotCode` and
`state`; gate payloads add `direction` (`Entry` or `Exit`), `plateNumber`,
`vehicleType` (`Car`) and `confidence` (0..1). MQTT is QoS 1, retain=false;
publication is acknowledged before the next event is sent. Re-delivery remains
possible at QoS 1 and consumers must deduplicate by `eventId`.

Start with `docker compose up --build rabbitmq iot-simulator`, and observe
`docker compose logs -f iot-simulator`. Disable by setting
`DEVICE_SIMULATION_ENABLED=false`. Never point it to a production lot.

**Integration still missing:** The current Parking service has
`POST /api/parking/slots/sensor-events` but does not subscribe to the MQTT
sensor topic. Parking has a staff HTTP gate-reading endpoint but no MQTT gate
consumer either; the gate MQTT topic/schema must be confirmed when implementing
that consumer. Publishing alone does **not** update the UI, slot state, or
parking sessions. Full device-to-Parking E2E is **not** claimed complete.

Run broker-free unit tests from the repo root:

```bash
python -m unittest discover -s edge/iot-simulator/src -p 'test_*.py' -v
```
