"""Opt-in MQTT device simulator for a dedicated development parking lot.

Sensor topics follow the documented Parking slot contract. Gate topics are a
provisional Sprint 3 convention until the Parking MQTT consumer is specified.
"""

from __future__ import annotations

import json
import math
import os
import re
import signal
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from threading import Event
from typing import Any, Mapping

_SLOT_RE = re.compile(r"^[A-Z0-9][A-Z0-9_-]{0,19}$")
_PLATE_RE = re.compile(r"^[A-Z0-9.-]{5,16}$")


@dataclass(frozen=True)
class SimulationConfig:
    lot_id: uuid.UUID
    slots: tuple[str, ...]
    interval_seconds: float
    mode: str
    plate_number: str | None = None
    confidence: float = 0.97


def parse_slots(raw: str) -> tuple[str, ...]:
    slots = tuple(part.strip().upper() for part in raw.split(","))
    if not slots or not all(_SLOT_RE.fullmatch(slot) for slot in slots):
        raise ValueError("SIM_SLOT_CODES must contain valid, nonempty car slot codes (maximum 20 characters)")
    if len(set(slots)) != len(slots):
        raise ValueError("SIM_SLOT_CODES must not contain duplicates")
    return slots


def load_config(env: Mapping[str, str]) -> SimulationConfig:
    try:
        lot_id = uuid.UUID(env["SIM_LOT_ID"])
    except (KeyError, ValueError, AttributeError) as exc:
        raise ValueError("SIM_LOT_ID must be a valid UUID for an existing demo lot") from exc
    if lot_id.int == 0:
        raise ValueError("SIM_LOT_ID must not be the empty UUID")
    slots = parse_slots(env.get("SIM_SLOT_CODES", ""))
    try:
        interval = float(env.get("SIM_INTERVAL_SECONDS", "5"))
    except ValueError as exc:
        raise ValueError("SIM_INTERVAL_SECONDS must be numeric") from exc
    if not math.isfinite(interval) or not 1 <= interval <= 3600:
        raise ValueError("SIM_INTERVAL_SECONDS must be between 1 and 3600")
    mode = env.get("SIM_MODE", "slot").strip().lower()
    if mode not in {"slot", "full-cycle"}:
        raise ValueError("SIM_MODE must be 'slot' or 'full-cycle'")
    plate = None
    confidence = 0.97
    if mode == "full-cycle":
        plate = env.get("SIM_PLATE_NUMBER", "").strip().upper()
        if not _PLATE_RE.fullmatch(plate):
            raise ValueError("SIM_PLATE_NUMBER must be a valid demo plate (5-16 characters)")
        try:
            confidence = float(env.get("SIM_CONFIDENCE", "0.97"))
        except ValueError as exc:
            raise ValueError("SIM_CONFIDENCE must be numeric") from exc
        if not math.isfinite(confidence) or not 0 <= confidence <= 1:
            raise ValueError("SIM_CONFIDENCE must be between 0 and 1")
    return SimulationConfig(lot_id, slots, interval, mode, plate, confidence)


def event_metadata(lot_id: uuid.UUID, device_id: str) -> dict[str, str]:
    return {
        "eventId": str(uuid.uuid4()),
        "deviceId": device_id,
        "lotId": str(lot_id),
        "eventTime": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
    }


def make_sensor_event(config: SimulationConfig, slot_code: str, state: str) -> tuple[str, dict[str, Any]]:
    if slot_code not in config.slots or state not in {"Occupied", "Empty"}:
        raise ValueError("Invalid configured slot or sensor state")
    topic = f"lot/{config.lot_id}/slot/{slot_code}"
    return topic, {
        **event_metadata(config.lot_id, f"sim-slot-{slot_code}"),
        "slotCode": slot_code,
        "state": state,
    }


def make_gate_event(config: SimulationConfig, direction: str) -> tuple[str, dict[str, Any]]:
    if config.mode != "full-cycle" or direction not in {"Entry", "Exit"}:
        raise ValueError("Gate events require full-cycle mode and Entry/Exit direction")
    # Provisional, configurable later when Parking's gate MQTT consumer is agreed.
    topic = f"lot/{config.lot_id}/gate/{direction.lower()}"
    return topic, {
        **event_metadata(config.lot_id, f"sim-gate-{direction.lower()}"),
        "direction": direction,
        "plateNumber": config.plate_number,
        "vehicleType": "Car",  # Physical slot sensor events only exist for car slots.
        "confidence": config.confidence,
    }


def events_for_step(config: SimulationConfig, step: int) -> tuple[str, dict[str, Any]]:
    if config.mode == "slot":
        slot = config.slots[step % len(config.slots)]
        state = "Occupied" if (step // len(config.slots)) % 2 == 0 else "Empty"
        return make_sensor_event(config, slot, state)
    slot = config.slots[(step // 4) % len(config.slots)]
    stage = step % 4
    if stage == 0:
        return make_gate_event(config, "Entry")
    if stage == 1:
        return make_sensor_event(config, slot, "Occupied")
    if stage == 2:
        return make_sensor_event(config, slot, "Empty")
    return make_gate_event(config, "Exit")


def publish_events(client: Any, config: SimulationConfig, stop_requested: Event) -> None:
    """Publish one event per tick with QoS 1, checking broker acknowledgement."""
    step = 0
    while not stop_requested.is_set():
        topic, event = events_for_step(config, step)
        message = json.dumps(event, separators=(",", ":"))
        published = client.publish(topic, message, qos=1, retain=False)
        published.wait_for_publish(timeout=10)
        if not published.is_published():
            raise RuntimeError("MQTT publish was not acknowledged")
        print(f"Simulated event {event['eventId']} on {topic}", flush=True)
        step += 1
        stop_requested.wait(config.interval_seconds)


def run_from_environment(stop_requested: Event, env: Mapping[str, str] | None = None) -> None:
    env = os.environ if env is None else env
    if env.get("DEVICE_SIMULATION_ENABLED", "false").casefold() != "true":
        print("IoT simulator idle (DEVICE_SIMULATION_ENABLED=false); no traffic sent", flush=True)
        stop_requested.wait()
        return

    config = load_config(env)
    # Import only when opt-in simulation is enabled, so local tests need no broker/client.
    import paho.mqtt.client as mqtt

    client = mqtt.Client(
        mqtt.CallbackAPIVersion.VERSION2,
        client_id=f"parking-iot-sim-{uuid.uuid4().hex[:12]}",
    )
    username = env.get("MQTT_USERNAME", "")
    if username:
        client.username_pw_set(username, env.get("MQTT_PASSWORD", ""))
    connected = Event()

    def on_connect(_client: Any, _userdata: Any, _flags: Any, reason_code: Any, _props: Any) -> None:
        if reason_code == 0:
            connected.set()
        else:
            print(f"MQTT broker rejected connection: {reason_code}", flush=True)

    def on_disconnect(_client: Any, _userdata: Any, _flags: Any, _reason: Any, _props: Any) -> None:
        connected.clear()

    client.on_connect = on_connect
    client.on_disconnect = on_disconnect
    client.reconnect_delay_set(min_delay=1, max_delay=10)
    try:
        client.connect(env.get("MQTT_HOST", "rabbitmq"), int(env.get("MQTT_PORT", "1883")), keepalive=60)
        client.loop_start()
        if not connected.wait(timeout=10):
            raise RuntimeError("MQTT broker connection was not established")
        publish_events(client, config, stop_requested)
    finally:
        client.disconnect()
        client.loop_stop()


def main() -> None:
    stop_requested = Event()
    signal.signal(signal.SIGTERM, lambda *_: stop_requested.set())
    signal.signal(signal.SIGINT, lambda *_: stop_requested.set())
    run_from_environment(stop_requested)


if __name__ == "__main__":
    main()
