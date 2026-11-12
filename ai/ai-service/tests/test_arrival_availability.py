import unittest
from datetime import datetime, timedelta, timezone
from uuid import uuid4

from pydantic import ValidationError

from app.api.arrival_availability import ArrivalAvailabilityRequest, get_arrival_availability
from app.application.arrival_availability import (
    AvailabilitySnapshot,
    estimate_arrival_availability,
)


class ArrivalAvailabilityTests(unittest.TestCase):
    def setUp(self):
        self.lot = uuid4()
        # 2026-11-03 14:30 Vietnam / 07:30 UTC (Tuesday)
        self.arrival = datetime(2026, 11, 3, 7, 30, tzinfo=timezone.utc)

    def sample(self, minutes_ago=0, available=1, lot=None, vehicle="Car"):
        return AvailabilitySnapshot(
            lot or self.lot,
            vehicle,
            self.arrival - timedelta(days=7, minutes=minutes_ago),
            available,
        )

    def test_uses_same_lot_weekday_and_fifteen_minute_bucket(self):
        samples = [self.sample(available=n) for n in [3, 2, 0, 1]]
        samples += [self.sample(minutes_ago=15, available=100)]
        result = estimate_arrival_availability(self.lot, "Car", self.arrival, 1, samples)
        self.assertEqual("lot_weekday_15m", result.baseline_source)
        self.assertEqual(4, result.sample_count)
        self.assertEqual(50, result.probability_percent)  # 2 successes; 2 failures; smoothing
        self.assertFalse(result.insufficient_data)

    def test_reservations_at_arrival_reduce_predicted_available_probability(self):
        snapshots = [self.sample(available=2) for _ in range(8)]
        without_held = estimate_arrival_availability(self.lot, "Car", self.arrival, 0, snapshots)
        with_held = estimate_arrival_availability(self.lot, "Car", self.arrival, 2, snapshots)
        self.assertGreater(
            without_held.probability_percent, with_held.probability_percent
        )
        self.assertEqual(10, with_held.probability_percent)
        self.assertIsNotNone(with_held.warning)

    def test_fallback_to_same_lot_time_then_peer_same_vehicle_then_default(self):
        different_weekday = [self.sample(available=1)]
        different_weekday[0] = AvailabilitySnapshot(
            self.lot, "Car", self.arrival - timedelta(days=1), 1
        )
        self.assertEqual("lot_daily_15m", estimate_arrival_availability(
            self.lot, "Car", self.arrival, 0, different_weekday).baseline_source)
        peer = [AvailabilitySnapshot(uuid4(), "Car", self.arrival - timedelta(days=1), 1)]
        self.assertEqual("peer_daily_15m", estimate_arrival_availability(
            self.lot, "Car", self.arrival, 0, peer).baseline_source)
        no_data = estimate_arrival_availability(self.lot, "Car", self.arrival, 0, [])
        self.assertEqual("default_50_percent", no_data.baseline_source)
        self.assertTrue(no_data.insufficient_data)
        self.assertEqual(50, no_data.probability_percent)

    def test_future_snapshots_do_not_leak_into_prediction(self):
        future = AvailabilitySnapshot(self.lot, "Car", self.arrival + timedelta(days=7), 100)
        result = estimate_arrival_availability(self.lot, "Car", self.arrival, 0, [future])
        self.assertEqual("default_50_percent", result.baseline_source)
        self.assertEqual(0, result.sample_count)

    def test_motorbike_ignores_car_samples_and_uses_capacity_snapshots(self):
        result = estimate_arrival_availability(
            self.lot, "Motorbike", self.arrival, 0,
            [self.sample(available=0, vehicle="Car"),
             self.sample(available=3, vehicle="Motorbike")],
        )
        self.assertEqual(1, result.sample_count)
        self.assertEqual(66.67, result.probability_percent)

    def test_timezone_aware_validation_and_no_negative_quota(self):
        with self.assertRaises(ValidationError):
            ArrivalAvailabilityRequest(lotId=self.lot, vehicleType="Car",
                arrivalAt=self.arrival.replace(tzinfo=None), heldQuotaAtArrival=0)
        with self.assertRaises(ValidationError):
            ArrivalAvailabilityRequest(lotId=self.lot, vehicleType="Car",
                arrivalAt=self.arrival, heldQuotaAtArrival=-1)

    def test_http_contract_accepts_valid_request_and_rejects_bad_timestamp(self):
        """Exercise HTTP via ASGI without adding an unpinned httpx dependency."""
        import asyncio
        import json
        import os
        from unittest.mock import patch

        from fastapi import FastAPI
        from app.api.arrival_availability import router

        app = FastAPI()
        app.include_router(router)

        async def post(payload, service_key=None):
            body = json.dumps(payload).encode("utf-8")
            sent = []

            async def receive():
                return {"type": "http.request", "body": body, "more_body": False}

            async def send(message):
                sent.append(message)

            path = b"/internal/ai/arrival-availability"
            scope = {
                "type": "http",
                "asgi": {"version": "3.0"},
                "http_version": "1.1",
                "method": "POST",
                "scheme": "http",
                "path": path.decode("ascii"),
                "raw_path": path,
                "query_string": b"",
                "root_path": "",
                "headers": [(b"content-type", b"application/json")]
                    + ([(b"x-arrival-service-key", service_key.encode("utf-8"))] if service_key else []),
                "server": ("testserver", 80),
                "client": ("127.0.0.1", 12345),
            }
            await app(scope, receive, send)
            status = next(m["status"] for m in sent if m["type"] == "http.response.start")
            response = b"".join(
                m.get("body", b"") for m in sent if m["type"] == "http.response.body"
            )
            return status, json.loads(response)

        async def exercise():
            payload = {
                "lotId": str(self.lot),
                "vehicleType": "Car",
                "arrivalAt": "2026-11-03T14:30:00+07:00",
                "heldQuotaAtArrival": 0,
                "snapshots": [],
            }
            missing_key, _ = await post(payload)
            self.assertEqual(403, missing_key)
            wrong_key, _ = await post(payload, service_key="bad-key")
            self.assertEqual(403, wrong_key)
            status, response = await post(payload, service_key="local-test-secret")
            self.assertEqual(200, status)
            self.assertEqual(50, response["probabilityPercent"])
            self.assertTrue(response["estimated"])

            payload["arrivalAt"] = "2026-11-03T14:30:00"
            status, _ = await post(payload, service_key="local-test-secret")
            self.assertEqual(422, status)  # This isolated FastAPI app uses default 422.

        with patch.dict(os.environ, {"ARRIVAL_API_KEY": "local-test-secret"}):
            asyncio.run(exercise())
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": ""}):
            no_key_configured_status, _ = asyncio.run(post({
                "lotId": str(self.lot), "vehicleType": "Car",
                "arrivalAt": "2026-11-03T14:30:00+07:00",
                "heldQuotaAtArrival": 0, "snapshots": [],
            }, service_key="local-test-secret"))
            self.assertEqual(503, no_key_configured_status)

    def test_snapshots_validate_timezone_type_and_capacity(self):
        basic = {
            "lotId": self.lot,
            "vehicleType": "Car",
            "arrivalAt": self.arrival,
            "heldQuotaAtArrival": 0,
        }
        for invalid_snapshot in [
            {"lotId": self.lot, "vehicleType": "Car",
             "capturedAt": self.arrival.replace(tzinfo=None), "availableSlots": 1},
            {"lotId": self.lot, "vehicleType": "Car",
             "capturedAt": self.arrival, "availableSlots": -1},
            {"lotId": self.lot, "vehicleType": "Truck",
             "capturedAt": self.arrival, "availableSlots": 1},
        ]:
            with self.subTest(snapshot=invalid_snapshot), self.assertRaises(ValidationError):
                ArrivalAvailabilityRequest(**basic, snapshots=[invalid_snapshot])
        with self.assertRaises(ValidationError):
            ArrivalAvailabilityRequest(**basic, snapshots=[{
                "lotId": self.lot, "vehicleType": "Car",
                "capturedAt": self.arrival, "availableSlots": 1,
            }] * 5001)

    def test_request_adapter_produces_explicit_estimation_metadata(self):
        request = ArrivalAvailabilityRequest(lotId=self.lot, vehicleType="Car",
            arrivalAt=self.arrival, heldQuotaAtArrival=0)
        result = get_arrival_availability(request)
        self.assertEqual(self.lot, result.lotId)
        self.assertEqual(50, result.probabilityPercent)
        self.assertTrue(result.estimated)
        self.assertTrue(result.insufficientData)


if __name__ == "__main__":
    unittest.main()
