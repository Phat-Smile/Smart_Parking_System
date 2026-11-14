"""Contract tests for internal AI recommendations HTTP endpoint (US-AI-001, FR-AI-001, BR-08)."""

import asyncio
import json
import os
import unittest
from datetime import datetime, timezone
from unittest.mock import patch
from uuid import uuid4

from fastapi import FastAPI
from app.api.recommendations import router


class RecommendationApiTests(unittest.TestCase):

    def setUp(self):
        self.app = FastAPI()
        self.app.include_router(router)
        self.api_key = "test_arrival_secret_key_123"

    def _call_post(self, payload: dict, service_key: str | None = None):
        """Invoke ASGI app directly without external dependencies."""
        body = json.dumps(payload).encode("utf-8")
        sent = []

        headers = [
            (b"content-type", b"application/json"),
            (b"content-length", str(len(body)).encode("ascii")),
        ]
        if service_key is not None:
            headers.append((b"x-arrival-service-key", service_key.encode("ascii")))

        scope = {
            "type": "http",
            "asgi": {"version": "3.0"},
            "http_version": "1.1",
            "method": "POST",
            "scheme": "http",
            "path": "/internal/ai/recommendations",
            "raw_path": b"/internal/ai/recommendations",
            "query_string": b"",
            "headers": headers,
        }

        async def receive():
            return {"type": "http.request", "body": body, "more_body": False}

        async def send(message):
            sent.append(message)

        asyncio.run(self.app(scope, receive, send))

        start = next(m for m in sent if m["type"] == "http.response.start")
        body_parts = [m.get("body", b"") for m in sent if m["type"] == "http.response.body"]
        raw_body = b"".join(body_parts).decode("utf-8")
        data = json.loads(raw_body) if raw_body else {}

        return start["status"], data

    def test_missing_or_wrong_service_key_returns_forbidden(self):
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key}):
            payload = {
                "customerLatitude": 10.7725,
                "customerLongitude": 106.6980,
                "vehicleType": "Car",
                "arrivalAt": "2026-11-07T10:00:00Z",
                "durationMinutes": 120,
                "candidateLots": [],
            }
            # Missing header
            status, _ = self._call_post(payload, service_key=None)
            self.assertEqual(403, status)

            # Wrong key
            status, _ = self._call_post(payload, service_key="wrong_key")
            self.assertEqual(403, status)

    def test_valid_recommendation_request_returns_top_candidates(self):
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key}):
            lot1_id = str(uuid4())
            lot2_id = str(uuid4())

            payload = {
                "customerLatitude": 10.7725,
                "customerLongitude": 106.6980,
                "vehicleType": "Car",
                "arrivalAt": "2026-11-07T10:00:00Z",
                "durationMinutes": 120,
                "candidateLots": [
                    {
                        "lotId": lot1_id,
                        "name": "Bãi đỗ Nguyễn Huệ",
                        "latitude": 10.7740,
                        "longitude": 106.7020,
                        "priceForDuration": 25000.0,
                        "heldQuota": 0,
                        "snapshots": [],
                    },
                    {
                        "lotId": lot2_id,
                        "name": "Bãi đỗ Lê Lợi",
                        "latitude": 10.7710,
                        "longitude": 106.6990,
                        "priceForDuration": 15000.0,
                        "heldQuota": 0,
                        "snapshots": [],
                    },
                ],
            }

            status, data = self._call_post(payload, service_key=self.api_key)
            self.assertEqual(200, status)
            self.assertIn("recommendedLots", data)
            self.assertEqual(2, len(data["recommendedLots"]))

            # Verify response schema fields
            first = data["recommendedLots"][0]
            self.assertEqual(1, first["rank"])
            self.assertIn("lotId", first)
            self.assertIn("probabilityPercent", first)
            self.assertIn("reasons", first)
            self.assertIsInstance(first["reasons"], list)
            self.assertIn("sampleCount", first)
            self.assertIn("insufficientData", first)
            self.assertIn("baselineSource", first)

    def test_unknown_held_quota_flags_insufficient_data(self):
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key}):
            lot_id = str(uuid4())
            payload = {
                "customerLatitude": 10.7725,
                "customerLongitude": 106.6980,
                "vehicleType": "Car",
                "arrivalAt": "2026-11-07T10:00:00Z",
                "durationMinutes": 120,
                "candidateLots": [
                    {
                        "lotId": lot_id,
                        "name": "Bãi đỗ Test",
                        "latitude": 10.7740,
                        "longitude": 106.7020,
                        "priceForDuration": 25000.0,
                        "heldQuota": 0,
                        "heldQuotaKnown": False,
                        "snapshots": [],
                    }
                ],
            }
            status, data = self._call_post(payload, service_key=self.api_key)
            self.assertEqual(200, status)
            first = data["recommendedLots"][0]
            self.assertTrue(first["insufficientData"])


if __name__ == "__main__":
    unittest.main()
