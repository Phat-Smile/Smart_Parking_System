"""Deterministic MQTT simulator tests that do not need an MQTT broker."""

import json
import unittest
import uuid
from threading import Event
from unittest.mock import patch

from main import (SimulationConfig, events_for_step, load_config, make_gate_event,
                  make_sensor_event, parse_slots, publish_events, run_from_environment)


class PublishAck:
    def __init__(self, ok):
        self.ok = ok

    def wait_for_publish(self, timeout):
        assert timeout == 10

    def is_published(self):
        return self.ok


class FakeMqttClient:
    def __init__(self, stop, stop_after=4, ack=True):
        self.stop = stop
        self.stop_after = stop_after
        self.ack = ack
        self.events = []

    def publish(self, topic, payload, qos, retain):
        self.events.append((topic, json.loads(payload), qos, retain))
        if len(self.events) == self.stop_after:
            self.stop.set()
        return PublishAck(self.ack)


class SimulatorTests(unittest.TestCase):
    def setUp(self):
        self.lot_id = uuid.uuid4()
        self.config = SimulationConfig(self.lot_id, ("A-01", "B-02"), 1, "slot")

    def test_parses_existing_slot_codes_safely(self):
        self.assertEqual(("A-01", "B_02"), parse_slots(" a-01,b_02 "))
        for bad in ("", ",", "A,A", "A/B", "A.B", "A+B", "A#B", "A" * 21, "A,,B"):
            with self.subTest(value=bad), self.assertRaises(ValueError):
                parse_slots(bad)

    def test_requires_opt_in_lot_and_valid_configuration(self):
        with self.assertRaisesRegex(ValueError, "SIM_LOT_ID"):
            load_config({"SIM_SLOT_CODES": "A-01"})
        env = {"SIM_LOT_ID": str(self.lot_id), "SIM_SLOT_CODES": "A-01"}
        self.assertEqual("slot", load_config(env).mode)
        for value in ("nan", "inf", "0", "3601", "oops"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                load_config({**env, "SIM_INTERVAL_SECONDS": value})
        for mode in ("unknown", "full-cycle"):
            with self.subTest(mode=mode), self.assertRaises(ValueError):
                load_config({**env, "SIM_MODE": mode})
        self.assertEqual("full-cycle", load_config({**env, "SIM_MODE": "full-cycle", "SIM_PLATE_NUMBER": "30A-123.45"}).mode)
        with self.assertRaises(ValueError):
            load_config({**env, "SIM_MODE": "full-cycle", "SIM_PLATE_NUMBER": "30A12345", "SIM_CONFIDENCE": "nan"})

    def test_sensor_contract_topic_and_fresh_event_ids(self):
        topic, first = make_sensor_event(self.config, "A-01", "Occupied")
        _, second = make_sensor_event(self.config, "A-01", "Empty")
        self.assertEqual(f"lot/{self.lot_id}/slot/A-01", topic)
        self.assertEqual({"eventId", "deviceId", "lotId", "eventTime", "slotCode", "state"}, set(first))
        self.assertEqual("Occupied", first["state"])
        self.assertNotEqual(first["eventId"], second["eventId"])
        self.assertTrue(first["eventTime"].endswith("Z"))
        with self.assertRaises(ValueError):
            make_sensor_event(self.config, "OTHER", "Empty")

    def test_full_cycle_covers_gate_entry_occupancy_vacancy_and_exit(self):
        config = SimulationConfig(self.lot_id, ("A-01",), 1, "full-cycle", "30A12345", 0.98)
        results = [events_for_step(config, step) for step in range(4)]
        self.assertEqual(["gate/entry", "slot/A-01", "slot/A-01", "gate/exit"],
                         [topic.split(str(self.lot_id) + "/")[1] for topic, _ in results])
        self.assertEqual("Entry", results[0][1]["direction"])
        self.assertEqual("Exit", results[3][1]["direction"])
        self.assertEqual("Car", results[0][1]["vehicleType"])
        self.assertEqual("30A12345", results[0][1]["plateNumber"])
        self.assertEqual(0.98, results[0][1]["confidence"])
        self.assertEqual(["Occupied", "Empty"], [results[i][1]["state"] for i in (1, 2)])
        self.assertEqual(4, len({payload["eventId"] for _, payload in results}))
        with self.assertRaises(ValueError):
            make_gate_event(config, "Other")

    def test_slot_only_rotates_and_alternates(self):
        results = [events_for_step(self.config, step)[1] for step in range(6)]
        self.assertEqual(["A-01", "B-02", "A-01", "B-02", "A-01", "B-02"],
                         [result["slotCode"] for result in results])
        self.assertEqual(["Occupied", "Occupied", "Empty", "Empty", "Occupied", "Occupied"],
                         [result["state"] for result in results])

    def test_qos_one_nonretained_and_acknowledged(self):
        stop = Event()
        mqtt = FakeMqttClient(stop)
        with patch("builtins.print"):
            publish_events(mqtt, self.config, stop)
        self.assertEqual(4, len(mqtt.events))
        self.assertTrue(all(qos == 1 and retain is False for _, _, qos, retain in mqtt.events))

    def test_unacknowledged_publish_raises(self):
        stop = Event()
        with self.assertRaisesRegex(RuntimeError, "not acknowledged"):
            publish_events(FakeMqttClient(stop, ack=False), self.config, stop)

    def test_disabled_never_connects_even_without_configuration(self):
        stop = Event()
        stop.set()
        with patch("builtins.print"):
            run_from_environment(stop, {"DEVICE_SIMULATION_ENABLED": "false"})


if __name__ == "__main__":
    unittest.main()
