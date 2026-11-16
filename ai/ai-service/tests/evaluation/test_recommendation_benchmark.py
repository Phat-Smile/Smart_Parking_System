"""40-Scenario Golden Benchmark Matrix for AI Recommendation Engine (AI-EVAL).

Validates:
- Group 1 (TC-EVAL-01 to TC-EVAL-15): Multi-criteria 40/40/20 utility scoring & tie-breaking.
- Group 2 (TC-EVAL-16 to TC-EVAL-25): Safety rule BR-08.a (30-point penalty, no Rank 1 for p < 30%).
- Group 3 (TC-EVAL-26 to TC-EVAL-32): Fallback weighting without ETA (BR-08.d) & data scarcity defaults.
- Group 4 (TC-EVAL-33 to TC-EVAL-40): Explanatory reasons phrasing and anti-hallucination assertions.
"""

import time
import unittest
from uuid import UUID

from app.domain.scoring import (
    CandidateScoreInput,
    rank_candidate_lots,
)
from app.domain.reasons import generate_recommendation_reasons


class RecommendationBenchmarkTests(unittest.TestCase):

    def _lot(self, idx: int, drive_time: float | None, prob: float, price: float) -> CandidateScoreInput:
        return CandidateScoreInput(
            lot_id=UUID(f"00000000-0000-0000-0000-{idx:012d}"),
            name=f"Bãi xe {idx}",
            drive_time_minutes=drive_time,
            probability_percent=prob,
            price=price,
        )

    # =========================================================================
    # Group 1: Multi-criteria 40/40/20 & Tie-breaking (TC-EVAL-01 to TC-EVAL-15)
    # =========================================================================

    def test_tc_eval_01_standard_triplet(self):
        lots = [
            self._lot(1, 10.0, 80.0, 20000.0),
            self._lot(2, 20.0, 60.0, 15000.0),
            self._lot(3, 5.0, 25.0, 30000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=3)
        self.assertEqual(ranked[0].lot_id, lots[0].lot_id)
        self.assertEqual(ranked[1].lot_id, lots[1].lot_id)
        self.assertEqual(ranked[2].lot_id, lots[2].lot_id)

    def test_tc_eval_02_all_equal_scores(self):
        lots = [
            self._lot(1, 15.0, 70.0, 20000.0),
            self._lot(2, 15.0, 70.0, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(2, len(ranked))
        self.assertAlmostEqual(ranked[0].score, ranked[1].score, places=2)

    def test_tc_eval_03_tie_breaking_favors_higher_probability(self):
        # Same drive time (0 normalized), same price (0 normalized), prob differs
        lots = [
            self._lot(1, 10.0, 75.0, 20000.0),
            self._lot(2, 10.0, 85.0, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)

    def test_tc_eval_04_tie_breaking_favors_lower_price(self):
        lots = [
            self._lot(1, 10.0, 80.0, 25000.0),
            self._lot(2, 10.0, 80.0, 15000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)

    def test_tc_eval_05_tie_breaking_favors_shorter_drive_time(self):
        lots = [
            self._lot(1, 20.0, 80.0, 20000.0),
            self._lot(2, 10.0, 80.0, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)

    def test_tc_eval_06_wide_price_range(self):
        lots = [
            self._lot(1, 10.0, 90.0, 150000.0),
            self._lot(2, 10.0, 90.0, 0.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)

    def test_tc_eval_07_wide_drive_time_range(self):
        lots = [
            self._lot(1, 60.0, 90.0, 20000.0),
            self._lot(2, 2.0, 90.0, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)

    def test_tc_eval_08_single_candidate(self):
        lots = [self._lot(1, 12.0, 75.0, 25000.0)]
        ranked = rank_candidate_lots(lots, top_k=1)
        self.assertEqual(1, len(ranked))
        self.assertEqual(1, ranked[0].rank)
        self.assertFalse(ranked[0].warning_high_full_risk)

    def test_tc_eval_09_high_probability_dominance(self):
        # 55% prob difference overcomes modest price difference
        lots = [
            self._lot(1, 10.0, 95.0, 30000.0),
            self._lot(2, 10.0, 40.0, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[0].lot_id, ranked[0].lot_id)

    def test_tc_eval_10_short_distance_dominance(self):
        lots = [
            self._lot(1, 3.0, 70.0, 20000.0),    # Close by (3 mins)
            self._lot(2, 35.0, 72.0, 20000.0),   # Far (35 mins) with negligible prob advantage
            self._lot(3, 40.0, 20.0, 20000.0),   # Reference baseline
        ]
        ranked = rank_candidate_lots(lots, top_k=3)
        self.assertEqual(lots[0].lot_id, ranked[0].lot_id)

    def test_tc_eval_11_balanced_candidate_beats_extremes(self):
        lots = [
            self._lot(1, 12.0, 80.0, 20000.0),   # Balanced: good across all 3
            self._lot(2, 2.0, 35.0, 50000.0),    # Very close but low prob & expensive
            self._lot(3, 40.0, 95.0, 10000.0),   # Far away but cheap & empty
        ]
        ranked = rank_candidate_lots(lots, top_k=3)
        self.assertEqual(lots[0].lot_id, ranked[0].lot_id)

    def test_tc_eval_12_ten_candidates_ranking_stability(self):
        lots = [self._lot(i, float(i * 3 + 2), float(95 - i * 5), float(10000 + i * 2000)) for i in range(1, 11)]
        ranked = rank_candidate_lots(lots, top_k=10)
        self.assertEqual(10, len(ranked))
        self.assertEqual(lots[0].lot_id, ranked[0].lot_id)

    def test_tc_eval_13_exact_score_bounds_between_0_and_100(self):
        lots = [
            self._lot(1, 5.0, 95.0, 10000.0),
            self._lot(2, 25.0, 50.0, 30000.0),
            self._lot(3, 50.0, 10.0, 80000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=3)
        for r in ranked:
            self.assertGreaterEqual(r.score, 0.0)
            self.assertLessEqual(r.score, 100.0)

    def test_tc_eval_14_lexicographical_id_tie_break_when_all_metrics_identical(self):
        lots = [
            self._lot(2, 10.0, 80.0, 20000.0),
            self._lot(1, 10.0, 80.0, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)

    def test_tc_eval_15_performance_benchmark_sub_15ms(self):
        lots = [self._lot(i, float(i % 30 + 1), float(30 + (i * 7) % 65), float(10000 + (i * 500) % 20000)) for i in range(1, 50)]
        t0 = time.perf_counter()
        for _ in range(50):
            _ = rank_candidate_lots(lots, top_k=3)
        elapsed_ms = (time.perf_counter() - t0) * 1000.0 / 50.0
        self.assertLess(elapsed_ms, 15.0, f"Average execution took {elapsed_ms:.2f}ms which exceeds 15ms target")

    # =========================================================================
    # Group 2: Safety Rule BR-08.a (TC-EVAL-16 to TC-EVAL-25)
    # =========================================================================

    def test_tc_eval_16_penalty_applied_when_prob_under_30(self):
        lots = [
            self._lot(1, 10.0, 80.0, 20000.0),
            self._lot(2, 10.0, 25.0, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        risky = next(r for r in ranked if r.lot_id == lots[1].lot_id)
        self.assertTrue(risky.warning_high_full_risk)
        self.assertEqual(lots[0].lot_id, ranked[0].lot_id)

    def test_tc_eval_17_boundary_prob_29_percent_penalized(self):
        lots = [
            self._lot(1, 10.0, 31.0, 20000.0),
            self._lot(2, 10.0, 29.9, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertFalse(ranked[0].warning_high_full_risk)
        self.assertTrue(ranked[1].warning_high_full_risk)

    def test_tc_eval_18_boundary_prob_30_percent_not_penalized(self):
        lots = [
            self._lot(1, 10.0, 30.0, 20000.0),
            self._lot(2, 10.0, 29.0, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        lot30 = next(r for r in ranked if r.lot_id == lots[0].lot_id)
        self.assertFalse(lot30.warning_high_full_risk)

    def test_tc_eval_19_prohibit_rank_1_for_risky_lot_even_with_perfect_time_and_price(self):
        lots = [
            self._lot(1, 1.0, 28.0, 5000.0),    # Super close, super cheap, but p < 30%
            self._lot(2, 30.0, 60.0, 50000.0),  # Far, expensive, but safe
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)
        self.assertFalse(ranked[0].warning_high_full_risk)
        self.assertEqual(lots[0].lot_id, ranked[1].lot_id)
        self.assertTrue(ranked[1].warning_high_full_risk)

    def test_tc_eval_20_all_lots_under_30_percent_fallback(self):
        lots = [
            self._lot(1, 10.0, 25.0, 20000.0),
            self._lot(2, 15.0, 15.0, 25000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        # All lots are risky; ranking still operates but all have warning
        self.assertTrue(all(r.warning_high_full_risk for r in ranked))

    def test_tc_eval_21_penalty_magnitude_is_30_points(self):
        # With 1 lot, normalized score is 100. Total utility = 100 - 30 = 70.
        lots = [self._lot(1, 10.0, 20.0, 20000.0)]
        ranked = rank_candidate_lots(lots, top_k=1)
        self.assertAlmostEqual(70.0, ranked[0].score, places=1)

    def test_tc_eval_22_risk_warning_field_is_boolean(self):
        lots = [self._lot(1, 10.0, 25.0, 20000.0)]
        ranked = rank_candidate_lots(lots, top_k=1)
        self.assertIsInstance(ranked[0].warning_high_full_risk, bool)

    def test_tc_eval_23_risky_lot_cannot_displace_rank_1_in_large_pool(self):
        lots = [
            self._lot(1, 1.0, 15.0, 1000.0),   # Risky outlier
            self._lot(2, 10.0, 85.0, 20000.0),
            self._lot(3, 12.0, 80.0, 22000.0),
            self._lot(4, 15.0, 75.0, 25000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=4)
        self.assertNotEqual(lots[0].lot_id, ranked[0].lot_id)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)

    def test_tc_eval_24_exact_zero_prob_penalized(self):
        lots = [
            self._lot(1, 10.0, 0.0, 20000.0),
            self._lot(2, 10.0, 50.0, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)
        self.assertTrue(ranked[1].warning_high_full_risk)

    def test_tc_eval_25_prob_99_percent_safe(self):
        lots = [self._lot(1, 10.0, 99.0, 20000.0)]
        ranked = rank_candidate_lots(lots, top_k=1)
        self.assertFalse(ranked[0].warning_high_full_risk)

    # =========================================================================
    # Group 3: Fallback & Missing ETA (TC-EVAL-26 to TC-EVAL-32)
    # =========================================================================

    def test_tc_eval_26_redistributes_weights_without_drive_time(self):
        # 67% prob + 33% price
        lots = [
            self._lot(1, None, 90.0, 30000.0),
            self._lot(2, None, 50.0, 10000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(2, len(ranked))
        self.assertIsNone(ranked[0].drive_time_minutes)

    def test_tc_eval_27_drive_time_none_handled_gracefully(self):
        lots = [self._lot(1, None, 80.0, 20000.0)]
        ranked = rank_candidate_lots(lots, top_k=1)
        self.assertIsNone(ranked[0].drive_time_minutes)
        self.assertEqual(1, ranked[0].rank)

    def test_tc_eval_28_drive_time_present_preserved_in_result(self):
        lots = [self._lot(1, 15.0, 80.0, 20000.0)]
        ranked = rank_candidate_lots(lots, top_k=1)
        self.assertEqual(15.0, ranked[0].drive_time_minutes)

    def test_tc_eval_29_fallback_tie_breaking_favors_probability(self):
        lots = [
            self._lot(1, None, 85.0, 20000.0),
            self._lot(2, None, 75.0, 20000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[0].lot_id, ranked[0].lot_id)

    def test_tc_eval_30_fallback_tie_breaking_favors_price(self):
        lots = [
            self._lot(1, None, 80.0, 25000.0),
            self._lot(2, None, 80.0, 15000.0),
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)

    def test_tc_eval_31_fallback_empty_candidate_list_returns_empty(self):
        ranked = rank_candidate_lots([], top_k=3)
        self.assertEqual([], ranked)

    def test_tc_eval_32_fallback_preserves_safety_rule_br08a(self):
        lots = [
            self._lot(1, None, 25.0, 10000.0),  # Cheap but risky
            self._lot(2, None, 60.0, 50000.0),  # Expensive but safe
        ]
        ranked = rank_candidate_lots(lots, top_k=2)
        self.assertEqual(lots[1].lot_id, ranked[0].lot_id)
        self.assertTrue(ranked[1].warning_high_full_risk)

    # =========================================================================
    # Group 4: Reasons Phrasing & Anti-Hallucination (TC-EVAL-33 to TC-EVAL-40)
    # =========================================================================

    def test_tc_eval_33_rank_1_safe_lot_has_optimal_badge(self):
        lot = self._lot(1, 10.0, 85.0, 20000.0)
        reasons = generate_recommendation_reasons(lot, rank=1, is_risky=False, all_candidates=[lot], has_drive_time=True)
        self.assertIn("Lựa chọn tối ưu tổng thể", reasons)

    def test_tc_eval_34_risky_lot_has_warning_reason_with_exact_percent(self):
        lot = self._lot(1, 10.0, 25.0, 20000.0)
        reasons = generate_recommendation_reasons(lot, rank=2, is_risky=True, all_candidates=[lot], has_drive_time=True)
        self.assertTrue(any("Nguy cơ hết chỗ sớm (xác suất ước tính 25%)" in r for r in reasons))

    def test_tc_eval_35_closest_lot_reason_includes_drive_time(self):
        lots = [self._lot(1, 5.0, 80.0, 20000.0), self._lot(2, 20.0, 80.0, 20000.0)]
        reasons = generate_recommendation_reasons(lots[0], rank=1, is_risky=False, all_candidates=lots, has_drive_time=True)
        self.assertTrue(any("Gần bạn nhất (khoảng 5 phút lái xe)" in r for r in reasons))

    def test_tc_eval_36_cheapest_lot_reason_includes_exact_price(self):
        lots = [self._lot(1, 10.0, 80.0, 15000.0), self._lot(2, 10.0, 80.0, 30000.0)]
        reasons = generate_recommendation_reasons(lots[0], rank=1, is_risky=False, all_candidates=lots, has_drive_time=True)
        self.assertTrue(any("Mức giá tiết kiệm nhất (15,000đ)" in r for r in reasons))

    def test_tc_eval_37_very_high_prob_reason_wording(self):
        lot = self._lot(1, 10.0, 85.0, 20000.0)
        reasons = generate_recommendation_reasons(lot, rank=1, is_risky=False, all_candidates=[lot], has_drive_time=True)
        self.assertIn("Tỷ lệ còn chỗ rất cao", reasons)

    def test_tc_eval_38_good_prob_reason_wording(self):
        lot = self._lot(1, 10.0, 70.0, 20000.0)
        reasons = generate_recommendation_reasons(lot, rank=2, is_risky=False, all_candidates=[lot], has_drive_time=True)
        self.assertIn("Khả năng còn chỗ tốt", reasons)

    def test_tc_eval_39_negative_assertion_no_deterministic_promises(self):
        lots = [self._lot(1, 5.0, 99.0, 10000.0), self._lot(2, 10.0, 20.0, 50000.0)]
        for lot in lots:
            for is_risky in [False, True]:
                for rank in [1, 2]:
                    reasons = generate_recommendation_reasons(lot, rank=rank, is_risky=is_risky, all_candidates=lots, has_drive_time=True)
                    combined = " ".join(reasons).lower()
                    self.assertNotIn("chắc chắn", combined)
                    self.assertNotIn("đảm bảo", combined)
                    self.assertNotIn("cam kết", combined)

    def test_tc_eval_40_fallback_reason_when_drive_time_missing(self):
        lot = self._lot(1, None, 75.0, 20000.0)
        reasons = generate_recommendation_reasons(lot, rank=1, is_risky=False, all_candidates=[lot], has_drive_time=False)
        self.assertIn("Chưa có thời gian lái xe (được sắp theo xác suất & giá)", reasons)


if __name__ == "__main__":
    unittest.main()
