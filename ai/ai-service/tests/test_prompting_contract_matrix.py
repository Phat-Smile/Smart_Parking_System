"""100 reproducible mock-provider contract scenarios; NOT a real-model quality evaluation."""

import unittest
from uuid import UUID

from app.application.prompting_recommendations import recommend_parking_lots_prompting
from tests.test_prompting_recommendations import StubProvider, NOW, lots, selection, IDS


class PromptingContractMatrixTests(unittest.TestCase):
    def test_100_synthetic_contract_scenarios(self):
        for case in range(100):
            with self.subTest(case=case):
                n = (case % 4) + 1
                candidates = lots(n, with_reliable_history=(case % 2 == 0))
                chosen = [c.lot_id for c in candidates[:min(3, n)]]
                if case % 5 == 0:
                    # LLM invents an ID and must be rejected.
                    chosen[-1] = UUID("ffffffff-ffff-ffff-ffff-ffffffffffff")
                    should_fail = True
                elif case % 5 == 1 and n > 1:
                    # Missing recommendation must not be silently padded by ranking.
                    chosen = chosen[:-1]
                    should_fail = True
                elif case % 5 == 2:
                    # Invalid additional field must not be rendered.
                    should_fail = True
                else:
                    should_fail = False
                response = selection(chosen, code="balanced")
                if case % 5 == 2:
                    response["recommendations"][0]["score"] = 100
                outcome = recommend_parking_lots_prompting(10.77, 106.7, "Car", NOW, 120,
                                                            candidates, StubProvider(response))
                self.assertEqual(should_fail, outcome.fallback_used)
                self.assertEqual(0 if should_fail else min(n, 3), len(outcome.recommended_lots))


if __name__ == "__main__":
    unittest.main()
