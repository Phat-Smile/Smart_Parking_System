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

    def test_prompting_api_uses_provider_selection_and_stays_authenticated(self):
        lot_id = str(uuid4())
        payload = {
            "customerLatitude": 10.7725,
            "customerLongitude": 106.6980,
            "vehicleType": "Car",
            "arrivalAt": "2026-11-07T10:00:00Z",
            "durationMinutes": 120,
            "priority": "cheapest",
            "candidateLots": [{
                "lotId": lot_id, "name": "Bãi mẫu", "latitude": 10.77,
                "longitude": 106.70, "priceForDuration": 20000.0,
                "heldQuota": 0, "snapshots": [],
            }],
        }
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key,
                                      "AI_RECOMMENDATION_MODE": "prompting"}), \
             patch("app.application.prompting_recommendations.OpenAICompatibleChatProvider") as provider:
            provider.return_value.choose.return_value = {
                "recommendations": [{"lotId": lot_id, "rank": 1, "reasonCode": "limited_data"}]
            }
            status, data = self._call_post(payload, service_key=self.api_key)
            self.assertEqual(200, status)
            self.assertFalse(data["fallbackUsed"])
            self.assertEqual(lot_id, data["recommendedLots"][0]["lotId"])
            self.assertEqual(1, provider.return_value.choose.call_count)
            self.assertEqual("cheapest", provider.return_value.choose.call_args.args[1]["priority"])

    def test_prompting_unknown_held_quota_retains_quality_contract(self):
        lot_id = str(uuid4())
        payload = {
            "customerLatitude": 10.7725, "customerLongitude": 106.6980,
            "vehicleType": "Car", "arrivalAt": "2026-11-07T10:00:00Z",
            "candidateLots": [{"lotId": lot_id, "name": "Bãi thử", "latitude": 10.77,
                               "longitude": 106.70, "priceForDuration": 18000,
                               "heldQuotaKnown": False, "snapshots": []}],
        }
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key,
                                      "AI_RECOMMENDATION_MODE": "prompting"}), \
             patch("app.application.prompting_recommendations.OpenAICompatibleChatProvider") as provider:
            provider.return_value.choose.return_value = {
                "recommendations": [{"lotId": lot_id, "rank": 1, "reasonCode": "limited_data"}]
            }
            status, data = self._call_post(payload, service_key=self.api_key)
        self.assertEqual(200, status)
        lot = data["recommendedLots"][0]
        self.assertIsNone(lot["probabilityPercent"])
        self.assertEqual(0, lot["sampleCount"])
        self.assertTrue(lot["insufficientData"])
        self.assertEqual("default_50_percent", lot["baselineSource"])

    def test_invalid_mode_fails_closed_without_invoking_legacy_engine(self):
        payload = {
            "customerLatitude": 10.7725, "customerLongitude": 106.6980,
            "vehicleType": "Car", "arrivalAt": "2026-11-07T10:00:00Z",
            "candidateLots": [{"lotId": str(uuid4()), "name": "A", "latitude": 10.77,
                               "longitude": 106.70, "priceForDuration": 10000}],
        }
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key,
                                      "AI_RECOMMENDATION_MODE": "typo"}):
            status, data = self._call_post(payload, service_key=self.api_key)
        self.assertEqual(200, status)
        self.assertTrue(data["fallbackUsed"])
        self.assertEqual("ai_recommendation_mode_invalid", data["fallbackReason"])
        self.assertEqual([], data["recommendedLots"])

    def test_osrm_eta_contract_echoes_time_and_rejects_invalid_minutes(self):
        lot_id = str(uuid4())
        payload = {
            "customerLatitude": 10.7725,
            "customerLongitude": 106.6980,
            "vehicleType": "Car",
            "arrivalAt": "2026-11-07T10:00:00Z",
            "candidateLots": [{"lotId": lot_id, "name": "A", "latitude": 10.77,
                               "longitude": 106.70, "priceForDuration": 10000,
                               "driveTimeMinutes": 6.5}],
        }
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key,
                                      "AI_RECOMMENDATION_MODE": "prompting"}), \
             patch("app.application.prompting_recommendations.OpenAICompatibleChatProvider") as provider:
            provider.return_value.choose.return_value = {
                "recommendations": [{"lotId": lot_id, "rank": 1, "reasonCode": "short_travel"}]
            }
            status, data = self._call_post(payload, service_key=self.api_key)
            self.assertEqual(200, status)
            self.assertEqual(6.5, data["recommendedLots"][0]["driveTimeMinutes"])
            self.assertFalse(data["fallbackUsed"])
            payload["candidateLots"][0]["driveTimeMinutes"] = -1
            bad_status, _ = self._call_post(payload, service_key=self.api_key)
            self.assertEqual(422, bad_status)
            self.assertEqual(1, provider.return_value.choose.call_count)

    def test_duplicate_candidate_ids_are_rejected(self):
        lot = {"lotId": str(uuid4()), "name": "A", "latitude": 10.77,
               "longitude": 106.70, "priceForDuration": 10000}
        payload = {
            "customerLatitude": 10.7725, "customerLongitude": 106.6980,
            "vehicleType": "Car", "arrivalAt": "2026-11-07T10:00:00Z",
            "candidateLots": [lot, lot.copy()],
        }
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key,
                                      "AI_RECOMMENDATION_MODE": "prompting"}):
            status, _ = self._call_post(payload, service_key=self.api_key)
        self.assertEqual(422, status)

    def test_prompting_provider_failure_does_not_return_legacy_ranking(self):
        lot_id = str(uuid4())
        payload = {
            "customerLatitude": 10.7725, "customerLongitude": 106.6980,
            "vehicleType": "Car", "arrivalAt": "2026-11-07T10:00:00Z",
            "candidateLots": [{"lotId": lot_id, "name": "A", "latitude": 10.77,
                               "longitude": 106.70, "priceForDuration": 10000}],
        }
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key,
                                      "AI_RECOMMENDATION_MODE": "prompting"}), \
             patch("app.application.prompting_recommendations.OpenAICompatibleChatProvider") as provider:
            provider.return_value.choose.side_effect = ValueError("bad output")
            status, data = self._call_post(payload, service_key=self.api_key)
        self.assertEqual(200, status)
        self.assertTrue(data["fallbackUsed"])
        self.assertEqual([], data["recommendedLots"])

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

    def test_legacy_without_history_does_not_claim_probability_or_route_eta(self):
        lot_id = str(uuid4())
        payload = {
            "customerLatitude": 10.7725, "customerLongitude": 106.6980,
            "vehicleType": "Car", "arrivalAt": "2026-11-07T10:00:00Z",
            "candidateLots": [{"lotId": lot_id, "name": "No history", "latitude": 10.77,
                               "longitude": 106.70, "priceForDuration": 18000,
                               "heldQuotaKnown": True, "snapshots": []}],
        }
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key,
                                      "AI_RECOMMENDATION_MODE": "legacy"}):
            status, response = self._call_post(payload, service_key=self.api_key)
        self.assertEqual(200, status)
        self.assertFalse(response["fallbackUsed"])
        item = response["recommendedLots"][0]
        self.assertTrue(item["insufficientData"])
        self.assertIsNone(item["probabilityPercent"])
        self.assertIsNone(item["driveTimeMinutes"])
        self.assertEqual("default_50_percent", item["baselineSource"])

    def test_legacy_sparse_high_risk_history_is_not_reported_as_confident_warning(self):
        lot_id = str(uuid4())
        payload = {
            "customerLatitude": 10.7725, "customerLongitude": 106.6980,
            "vehicleType": "Car", "arrivalAt": "2026-11-07T10:00:00Z",
            "candidateLots": [{"lotId": lot_id, "name": "Sparse", "latitude": 10.77,
                               "longitude": 106.70, "priceForDuration": 18000,
                               "snapshots": [
                                   {"lotId": lot_id, "vehicleType": "Car",
                                    "capturedAt": day, "availableSlots": 0}
                                   for day in ("2026-10-31T10:00:00Z", "2026-10-24T10:00:00Z")
                               ]}],
        }
        with patch.dict(os.environ, {"ARRIVAL_API_KEY": self.api_key,
                                      "AI_RECOMMENDATION_MODE": "legacy"}):
            status, response = self._call_post(payload, service_key=self.api_key)
        self.assertEqual(200, status)
        item = response["recommendedLots"][0]
        self.assertTrue(item["insufficientData"])
        self.assertIsNone(item["probabilityPercent"])
        self.assertFalse(item["warningHighFullRisk"])
        self.assertFalse(any("cảnh báo" in reason.lower() for reason in item["reasons"]))
        self.assertFalse(any("tối ưu" in reason.lower() for reason in item["reasons"]))

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
