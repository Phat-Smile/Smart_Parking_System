"""Explainability generator for parking recommendations (BR-08.c).

Produces concise, user-friendly reasons in Vietnamese for why a lot was recommended.
Invariants:
- Never displays raw utility scores (BR-08.c).
- Displays clear warning when arrival probability < 30% (BR-08.a).
- Informs user when driving time is unavailable (BR-08.d).
- Phrasing is strictly probabilistic and cautious (e.g. 'Tỷ lệ', 'Khả năng', 'Ước tính').
- Never promises slot certainty or uses deterministic terms like 'chắc chắn' or 'đảm bảo'.
- Matches the protobuf contract field 'repeated string reasons = 8' without contract modification.
"""

from typing import Sequence
from app.domain.scoring import CandidateScoreInput, WARNING_THRESHOLD_PERCENT


def generate_recommendation_reasons(
    candidate: CandidateScoreInput,
    rank: int,
    is_risky: bool,
    all_candidates: Sequence[CandidateScoreInput],
    has_drive_time: bool = True,
) -> list[str]:
    reasons: list[str] = []

    # 1. Rank 1 badge (safe lots only)
    if rank == 1 and not is_risky:
        reasons.append("Lựa chọn tối ưu tổng thể")

    # 2. Risk warning (BR-08.a: high risk of full capacity if probability < 30%)
    if is_risky:
        reasons.append(f"Cảnh báo: Nguy cơ hết chỗ sớm (xác suất ước tính {candidate.probability_percent:.0f}%)")

    # 3. Drive time explanation
    if has_drive_time and candidate.drive_time_minutes is not None:
        all_times = [c.drive_time_minutes for c in all_candidates if c.drive_time_minutes is not None]
        min_time = min(all_times) if all_times else None
        dt = int(round(candidate.drive_time_minutes))

        if min_time is not None and abs(candidate.drive_time_minutes - min_time) < 1e-6:
            reasons.append(f"Gần bạn nhất (khoảng {dt} phút lái xe)")
        elif dt <= 10:
            reasons.append(f"Thuận tiện di chuyển (khoảng {dt} phút)")
        else:
            reasons.append(f"Thời gian di chuyển ước tính {dt} phút")
    elif not has_drive_time:
        reasons.append("Chưa có thời gian lái xe (được sắp theo xác suất & giá)")

    # 4. Price explanation
    all_prices = [c.price for c in all_candidates]
    min_price = min(all_prices) if all_prices else None
    if min_price is not None and abs(candidate.price - min_price) < 1e-6:
        reasons.append(f"Mức giá tiết kiệm nhất ({int(candidate.price):,}đ)")
    elif candidate.price <= (sum(all_prices) / len(all_prices)):
        reasons.append("Mức giá hợp lý")

    # 5. Availability probability explanation (cautious probabilistic wording)
    if not is_risky:
        if candidate.probability_percent >= 80.0:
            reasons.append("Tỷ lệ còn chỗ rất cao")
        elif candidate.probability_percent >= 60.0:
            reasons.append("Khả năng còn chỗ tốt")

    return reasons
