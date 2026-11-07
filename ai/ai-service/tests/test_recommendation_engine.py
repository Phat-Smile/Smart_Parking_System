"""Unit and property tests for AI parking recommendation engine (US-AI-001, FR-AI-001, BR-08).

Validates:
- Multi-criteria min-max utility scoring matching the exact specification proof example.
- 30-point penalty and warning flag when arrival probability < 30% (BR-08.a).
- Rank 1 invariant: a lot with p < 30% cannot be rank 1 when any candidate has p >= 30%.
- Weight redistribution when driving time is missing (BR-08.d: 0.67 prob, 0.33 price).
- Deterministic tie-breaking rules.
- Haversine distance and 60-second TTL coordinate caching.
- End-to-end recommend_parking_lots application use case.
"""

import time
import unittest
from datetime import datetime, timedelta, timezone
from uuid import UUID, uuid4

from app.application.arrival_availability import AvailabilitySnapshot
from app.application.recommend_lots import (
    CandidateLotDto,
    recommend_parking_lots,
)
from app.domain.drive_time import (
    DriveTimeCache,
    estimate_drive_time_minutes,
    haversine_distance_km,
)
from app.domain.reasons import generate_recommendation_reasons
from app.domain.scoring import (
    CandidateScoreInput,
    rank_candidate_lots,
)


class RecommendationEngineTests(unittest.TestCase):

    def test_br08_specification_proof_example(self):
        """Validates the exact mathematical example from Section 5 of the AI plan.

        Lots:
        - Lot A: 10 min drive, 80% prob, 20,000 VND
        - Lot B: 20 min drive, 60% prob, 15,000 VND
        - Lot C: 5 min drive, 25% prob (<30%), 30,000 VND

        Expected:
        - Lot A: normalized (time 66.7, prob 100, price 66.7) -> total 80.0 -> Rank 1
        - Lot B: normalized (time 0, prob 63.6, price 100) -> total 45.45 -> Rank 2
        - Lot C: normalized (time 100, prob 0, price 0) -> raw 40.0 - penalty 30 = 10.0 -> Rank 3 (warning)
        """
        lot_a_id = UUID("11111111-1111-1111-1111-111111111111")
        lot_b_id = UUID("22222222-2222-2222-2222-222222222222")
        lot_c_id = UUID("33333333-3333-3333-3333-333333333333")

        candidates = [
            CandidateScoreInput(
                lot_id=lot_a_id,
                name="Bãi đỗ A (Trung tâm)",
                drive_time_minutes=10.0,
                probability_percent=80.0,
                price=20000.0,
            ),
            CandidateScoreInput(
                lot_id=lot_b_id,
                name="Bãi đỗ B (Giá rẻ)",
                drive_time_minutes=20.0,
                probability_percent=60.0,
                price=15000.0,
            ),
            CandidateScoreInput(
                lot_id=lot_c_id,
                name="Bãi đỗ C (Gần nhất nhưng dễ đầy)",
                drive_time_minutes=5.0,
                probability_percent=25.0,
                price=30000.0,
            ),
        ]

        results = rank_candidate_lots(
            candidates,
            top_k=3,
            generate_reasons_fn=generate_recommendation_reasons,
        )

        self.assertEqual(3, len(results))

        # Rank 1: Lot A
        self.assertEqual(1, results[0].rank)
        self.assertEqual(lot_a_id, results[0].lot_id)
        self.assertAlmostEqual(80.0, results[0].score, delta=0.5)
        self.assertFalse(results[0].warning_high_full_risk)
        self.assertTrue(any("tối ưu" in r.lower() for r in results[0].reasons))

        # Rank 2: Lot B
        self.assertEqual(2, results[1].rank)
        self.assertEqual(lot_b_id, results[1].lot_id)
        self.assertAlmostEqual(45.45, results[1].score, delta=0.5)
        self.assertFalse(results[1].warning_high_full_risk)
        self.assertTrue(any("tiết kiệm" in r.lower() for r in results[1].reasons))

        # Rank 3: Lot C
        self.assertEqual(3, results[2].rank)
        self.assertEqual(lot_c_id, results[2].lot_id)
        self.assertAlmostEqual(10.0, results[2].score, delta=0.5)
        self.assertTrue(results[2].warning_high_full_risk)
        self.assertTrue(any("cảnh báo" in r.lower() for r in results[2].reasons))

    def test_rank_one_restriction_prevents_risky_lot_from_first_place(self):
        """BR-08.a: A lot with p < 30% cannot be rank 1 when any candidate has p >= 30%."""
        risky_lot_id = uuid4()
        safe_lot_id = uuid4()

        # Risky lot has super low price and 0 min drive time, yielding high raw score
        # even after 30 penalty points: raw ~70 - 30 = 40. Safe lot score ~35.
        candidates = [
            CandidateScoreInput(
                lot_id=risky_lot_id,
                name="Bãi Cực Rẻ Nhưng Đầy",
                drive_time_minutes=1.0,
                probability_percent=20.0,  # < 30%
                price=5000.0,
            ),
            CandidateScoreInput(
                lot_id=safe_lot_id,
                name="Bãi An Toàn",
                drive_time_minutes=15.0,
                probability_percent=70.0,  # >= 30%
                price=25000.0,
            ),
        ]

        results = rank_candidate_lots(candidates, top_k=2)

        # Invariant: Safe lot MUST be rank 1!
        self.assertEqual(safe_lot_id, results[0].lot_id)
        self.assertEqual(1, results[0].rank)
        self.assertFalse(results[0].warning_high_full_risk)

        # Risky lot demoted to rank 2
        self.assertEqual(risky_lot_id, results[1].lot_id)
        self.assertEqual(2, results[1].rank)
        self.assertTrue(results[1].warning_high_full_risk)

    def test_all_lots_risky_allows_ranking_with_clear_warning(self):
        """BR-08.a: If all lots are < 30%, ranking proceeds normally with warnings on all lots."""
        id1, id2 = uuid4(), uuid4()
        candidates = [
            CandidateScoreInput(id1, "Lot 1", drive_time_minutes=5.0, probability_percent=20.0, price=10000.0),
            CandidateScoreInput(id2, "Lot 2", drive_time_minutes=10.0, probability_percent=15.0, price=20000.0),
        ]

        results = rank_candidate_lots(candidates, top_k=2, generate_reasons_fn=generate_recommendation_reasons)
        self.assertEqual(2, len(results))
        self.assertTrue(results[0].warning_high_full_risk)
        self.assertTrue(results[1].warning_high_full_risk)
        self.assertTrue(any("cảnh báo" in r.lower() for r in results[0].reasons))

    def test_missing_drive_time_redistributes_weights(self):
        """BR-08.d: When drive time is None, weights adapt to 0.67 prob and 0.33 price."""
        id_high_prob = uuid4()
        id_low_price = uuid4()

        candidates = [
            CandidateScoreInput(id_high_prob, "High Prob", drive_time_minutes=None, probability_percent=90.0, price=30000.0),
            CandidateScoreInput(id_low_price, "Low Price", drive_time_minutes=None, probability_percent=50.0, price=10000.0),
        ]

        results = rank_candidate_lots(candidates, top_k=2, generate_reasons_fn=generate_recommendation_reasons)

        self.assertEqual(2, len(results))
        # High prob gets 0.67 * 100 + 0.33 * 0 = 67.0
        # Low price gets 0.67 * 0 + 0.33 * 100 = 33.0
        # High prob should be rank 1
        self.assertEqual(id_high_prob, results[0].lot_id)
        self.assertAlmostEqual(67.0, results[0].score, delta=0.5)
        self.assertTrue(any("chưa có thời gian lái xe" in r.lower() for r in results[0].reasons))

    def test_deterministic_tie_breaking(self):
        """Identical scores break ties deterministically on prob desc, price asc, time asc, id asc."""
        id_a = UUID("00000000-0000-0000-0000-000000000001")
        id_b = UUID("00000000-0000-0000-0000-000000000002")

        candidates = [
            CandidateScoreInput(id_b, "Lot B", drive_time_minutes=10.0, probability_percent=70.0, price=20000.0),
            CandidateScoreInput(id_a, "Lot A", drive_time_minutes=10.0, probability_percent=70.0, price=20000.0),
        ]

        results = rank_candidate_lots(candidates, top_k=2)
        # Identical criteria -> broken by lot_id ascending ("...0001" before "...0002")
        self.assertEqual(id_a, results[0].lot_id)
        self.assertEqual(id_b, results[1].lot_id)

    def test_haversine_distance_and_drive_time_cache(self):
        """Validates Haversine distance calculation and 60-second TTL coordinate cache."""
        # Ben Thanh Market (10.7725, 106.6980) to Bitexco Financial Tower (10.7717, 106.7044) ~ 0.7 km
        dist_km = haversine_distance_km(10.7725, 106.6980, 10.7717, 106.7044)
        self.assertGreater(dist_km, 0.5)
        self.assertLess(dist_km, 1.2)

        drive_minutes = estimate_drive_time_minutes(dist_km)
        self.assertGreaterEqual(drive_minutes, 1.0)
        self.assertLess(drive_minutes, 10.0)

        # Test cache
        cache = DriveTimeCache(ttl_seconds=0.1)
        self.assertIsNone(cache.get(10.7725, 106.6980, 10.7717, 106.7044))
        cache.set(10.7725, 106.6980, 10.7717, 106.7044, drive_minutes)

        # Hit
        self.assertEqual(drive_minutes, cache.get(10.7725, 106.6980, 10.7717, 106.7044))
        # Nearby coordinates within 3 decimal places should hit same key
        self.assertEqual(drive_minutes, cache.get(10.772501, 106.698002, 10.771701, 106.704402))

        # Wait for TTL expiry
        time.sleep(0.15)
        self.assertIsNone(cache.get(10.7725, 106.6980, 10.7717, 106.7044))

    def test_end_to_end_recommend_parking_lots_application(self):
        """End-to-end recommendation flow with arrival availability snapshots and held quotas."""
        now = datetime(2026, 11, 7, 8, 0, tzinfo=timezone.utc)
        lot1_id, lot2_id = uuid4(), uuid4()

        snapshots1 = [
            AvailabilitySnapshot(lot1_id, "Car", now - timedelta(days=7), available_slots=10),
            AvailabilitySnapshot(lot1_id, "Car", now - timedelta(days=14), available_slots=8),
        ]
        snapshots2 = [
            AvailabilitySnapshot(lot2_id, "Car", now - timedelta(days=7), available_slots=0),
            AvailabilitySnapshot(lot2_id, "Car", now - timedelta(days=14), available_slots=0),
        ]

        candidates = [
            CandidateLotDto(
                lot_id=lot1_id,
                name="Bãi Phố Đi Bộ",
                latitude=10.775,
                longitude=106.700,
                price_for_duration=20000.0,
                held_quota=1,
                snapshots=snapshots1,
            ),
            CandidateLotDto(
                lot_id=lot2_id,
                name="Bãi Chợ Cũ",
                latitude=10.770,
                longitude=106.702,
                price_for_duration=15000.0,
                held_quota=5,
                snapshots=snapshots2,
            ),
        ]

        result = recommend_parking_lots(
            customer_latitude=10.773,
            customer_longitude=106.701,
            vehicle_type="Car",
            arrival_at=now,
            duration_minutes=120,
            candidates=candidates,
            top_k=3,
        )

        self.assertEqual(2, len(result.recommended_lots))
        self.assertEqual(1, result.recommended_lots[0].rank)
        self.assertEqual(2, result.recommended_lots[1].rank)
        # Lot 1 has available slots > held quota, so high prob -> Rank 1
        self.assertEqual(lot1_id, result.recommended_lots[0].lot_id)
        self.assertFalse(result.recommended_lots[0].warning_high_full_risk)


if __name__ == "__main__":
    unittest.main()
