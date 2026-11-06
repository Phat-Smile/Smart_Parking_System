# Parking: slot sensor events (US-MAP-005)

`POST /api/parking/slots/sensor-events` receives an event from a slot sensor (or the IoT simulator bridge).

## Authentication

The route has no user login. The device proves itself with the header `X-Device-Key`, compared against `DeviceAuth:SensorKey` (environment variable `DeviceAuth__SensorKey`, set from `SENSOR_DEVICE_KEY` in Compose).

| Case | Result |
|---|---|
| Header missing or wrong (comparison is case-sensitive) | `401`, `errorCode` `device.key_invalid` |
| `DeviceAuth:SensorKey` not configured | `401` for every call (the route stays closed) |
| Key correct | the request is processed as before |

The Gateway's `X-Service-Key` does not open this route; it is a different key.

Never commit the key. Use a different value outside local development.
