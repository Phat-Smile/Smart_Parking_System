"""Application use case for Top-3 Parking Lot Recommendations (US-AI-001, FR-AI-001, BR-08).

Coordinates arrival availability forecasting, multi-criteria scoring,
warning flags, and natural language explanations. Unknown routing ETA stays None.
"""

from dataclasses import dataclass
from datetime import datetime
from uuid import UUID

from app.application.arrival_availability import (
    AvailabilitySnapshot,
    estimate_arrival_availability,
)
from app.domain.drive_time import DriveTimeCache
from app.domain.reasons import generate_recommendation_reasons
from app.domain.scoring import (
    CandidateScoreInput,
    ScoredLot,
    rank_candidate_lots,
)



@dataclass(frozen=True)
class CandidateLotDto:
    lot_id: UUID
    name: str
    latitude: float
    longitude: float
    price_for_duration: float
    held_quota: int
    snapshots: list[AvailabilitySnapshot]
    held_quota_known: bool = True
    drive_time_minutes: float | None = None


@dataclass(frozen=True)
class RecommendationResult:
    recommended_lots: list[ScoredLot]
    fallback_used: bool
    fallback_reason: str | None


def recommend_parking_lots(
    customer_latitude: float,
    customer_longitude: float,
    vehicle_type: str,
    arrival_at: datetime,
    duration_minutes: int,
    candidates: list[CandidateLotDto],
    top_k: int = 3,
    drive_time_cache: DriveTimeCache | None = None,
) -> RecommendationResult:
    """Recommend top_k parking lots matching user location, vehicle type and arrival time."""
    if not candidates:
        return RecommendationResult(
            recommended_lots=[],
            fallback_used=False,
            fallback_reason=None,
        )

    # Parking supplies OSRM durations; never infer an ETA from distance.
    # Preserve the optional cache argument for backward-compatible callers.
    score_inputs: list[CandidateScoreInput] = []
    for c in candidates:
        # 1. Arrival availability estimation (BR-08.e)
        # BR-08.d redistributes weights when not every candidate has an OSRM duration.
        held_to_use = c.held_quota if c.held_quota_known else 0
        arrival_estimate = estimate_arrival_availability(
            lot_id=c.lot_id,
            vehicle_type=vehicle_type,
            arrival_at=arrival_at,
            held_quota_at_arrival=held_to_use,
            snapshots=c.snapshots,
        )
        is_insufficient = arrival_estimate.insufficient_data or (not c.held_quota_known)

        score_inputs.append(CandidateScoreInput(
            lot_id=c.lot_id,
            name=c.name,
            drive_time_minutes=c.drive_time_minutes,
            # Legacy baseline remains a provisional ranking signal, even when data quality
            # is insufficient; the public response suppresses untrustworthy probability.
            probability_percent=arrival_estimate.probability_percent,
            price=c.price_for_duration,
            sample_count=arrival_estimate.sample_count,
            insufficient_data=is_insufficient,
            baseline_source=arrival_estimate.baseline_source,
        ))

    # 2. Score and rank candidates
    scored_lots = rank_candidate_lots(
        candidates=score_inputs,
        top_k=top_k,
        generate_reasons_fn=generate_recommendation_reasons,
    )

    return RecommendationResult(
        recommended_lots=scored_lots,
        fallback_used=False,
        fallback_reason=None,
    )
