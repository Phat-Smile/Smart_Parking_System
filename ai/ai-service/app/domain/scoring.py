"""Domain logic for multi-criteria parking utility scoring and ranking (BR-08).

Pure functions without external dependencies or I/O.
Invariants:
- Normalizes driving time, arrival probability, and price into 0..100 scales.
- Applies 30-point penalty and warning flag when probability < 30% (BR-08.a).
- Enforces rank 1 restriction: a lot with p < 30% cannot be rank 1 if any lot has p >= 30%.
- Adjusts weights if driving time is unavailable (BR-08.d: 0.67 prob, 0.33 price).
- Deterministic tie-breaking on probability desc, price asc, drive time asc, lot_id asc.
"""

from dataclasses import dataclass, field
from uuid import UUID


WARNING_THRESHOLD_PERCENT = 30.0
LOW_PROBABILITY_PENALTY = 30.0
DEFAULT_WEIGHT_DRIVE_TIME = 0.40
DEFAULT_WEIGHT_PROBABILITY = 0.40
DEFAULT_WEIGHT_PRICE = 0.20

FALLBACK_WEIGHT_PROBABILITY = 0.67
FALLBACK_WEIGHT_PRICE = 0.33


@dataclass(frozen=True)
class CandidateScoreInput:
    lot_id: UUID
    name: str
    drive_time_minutes: float | None
    probability_percent: float
    price: float
    sample_count: int = 0
    insufficient_data: bool = False
    baseline_source: str | None = None


@dataclass(frozen=True)
class ScoredLot:
    lot_id: UUID
    name: str
    rank: int
    drive_time_minutes: float | None
    probability_percent: float
    price: float
    score: float
    warning_high_full_risk: bool
    reasons: list[str] = field(default_factory=list)
    sample_count: int = 0
    insufficient_data: bool = False
    baseline_source: str | None = None


def _min_max_scale_higher_is_better(value: float, min_val: float, max_val: float) -> float:
    if max_val <= min_val:
        return 100.0
    return 100.0 * (value - min_val) / (max_val - min_val)


def _min_max_scale_lower_is_better(value: float, min_val: float, max_val: float) -> float:
    if max_val <= min_val:
        return 100.0
    return 100.0 * (max_val - value) / (max_val - min_val)


def rank_candidate_lots(
    candidates: list[CandidateScoreInput],
    top_k: int = 3,
    generate_reasons_fn=None,
) -> list[ScoredLot]:
    """Score, penalize, break ties, and rank candidate lots according to BR-08."""
    if not candidates:
        return []

    # Check if drive time is available for all candidates
    has_drive_time = all(c.drive_time_minutes is not None for c in candidates)
    if has_drive_time:
        w_time = DEFAULT_WEIGHT_DRIVE_TIME
        w_prob = DEFAULT_WEIGHT_PROBABILITY
        w_price = DEFAULT_WEIGHT_PRICE
        t_values = [c.drive_time_minutes for c in candidates if c.drive_time_minutes is not None]
        min_t, max_t = min(t_values), max(t_values)
    else:
        # BR-08.d: Missing drive time redistributes weights to probability 0.67 and price 0.33
        w_time = 0.0
        w_prob = FALLBACK_WEIGHT_PROBABILITY
        w_price = FALLBACK_WEIGHT_PRICE
        min_t, max_t = 0.0, 0.0

    p_values = [c.probability_percent for c in candidates]
    min_p, max_p = min(p_values), max(p_values)

    c_values = [c.price for c in candidates]
    min_c, max_c = min(c_values), max(c_values)

    scored_items = []
    has_safe_lot = any(c.probability_percent >= WARNING_THRESHOLD_PERCENT for c in candidates)

    for c in candidates:
        # 1. Min-max normalization (0..100)
        s_prob = _min_max_scale_higher_is_better(c.probability_percent, min_p, max_p)
        s_price = _min_max_scale_lower_is_better(c.price, min_c, max_c)
        if has_drive_time and c.drive_time_minutes is not None:
            s_time = _min_max_scale_lower_is_better(c.drive_time_minutes, min_t, max_t)
        else:
            s_time = 0.0

        # 2. Raw multi-criteria score
        raw_score = (w_time * s_time) + (w_prob * s_prob) + (w_price * s_price)

        # 3. Penalty and warning flag for low arrival probability (BR-08.a)
        is_risky = c.probability_percent < WARNING_THRESHOLD_PERCENT
        if is_risky:
            final_score = max(0.0, raw_score - LOW_PROBABILITY_PENALTY)
        else:
            final_score = raw_score

        scored_items.append({
            "candidate": c,
            "final_score": round(final_score, 2),
            "is_risky": is_risky,
            "drive_time": c.drive_time_minutes,
            "prob": c.probability_percent,
            "price": c.price,
            "lot_id_str": str(c.lot_id),
        })

    # 4. Sorting with deterministic tie-breaking:
    # Primary: final_score descending
    # Tie 1: probability descending
    # Tie 2: price ascending
    # Tie 3: drive_time ascending (None treated as infinity)
    # Tie 4: lot_id string ascending
    def sort_key(item):
        c = item["candidate"]
        time_val = item["drive_time"] if item["drive_time"] is not None else float("inf")
        return (
            -item["final_score"],
            -item["prob"],
            item["price"],
            time_val,
            item["lot_id_str"],
        )

    scored_items.sort(key=sort_key)

    # 5. Enforce Rank 1 rule (BR-08.a):
    # A lot with p < 30% cannot be ranked #1 when there is any lot with p >= 30%.
    if has_safe_lot and scored_items[0]["is_risky"]:
        for idx in range(1, len(scored_items)):
            if not scored_items[idx]["is_risky"]:
                safe_item = scored_items.pop(idx)
                scored_items.insert(0, safe_item)
                break

    # 6. Build final ScoredLot list for top_k
    result = []
    for rank_idx, item in enumerate(scored_items[:top_k], start=1):
        c = item["candidate"]
        reasons = []
        if generate_reasons_fn:
            reasons = generate_reasons_fn(
                candidate=c,
                rank=rank_idx,
                is_risky=item["is_risky"],
                all_candidates=candidates,
                has_drive_time=has_drive_time,
            )

        result.append(ScoredLot(
            lot_id=c.lot_id,
            name=c.name,
            rank=rank_idx,
            drive_time_minutes=c.drive_time_minutes,
            probability_percent=c.probability_percent,
            price=c.price,
            score=item["final_score"],
            warning_high_full_risk=item["is_risky"],
            reasons=reasons,
            sample_count=c.sample_count,
            insufficient_data=c.insufficient_data,
            baseline_source=c.baseline_source,
        ))

    return result
