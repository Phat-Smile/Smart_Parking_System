"""Application use case for Top-3 Parking Lot Recommendations (US-AI-001, FR-AI-001, BR-08).

Coordinates arrival availability forecasting, drive time estimation, multi-criteria
scoring, warning flags, and natural language explanations.
"""

from dataclasses import dataclass
from datetime import datetime
from uuid import UUID

from app.application.arrival_availability import (
    AvailabilitySnapshot,
    estimate_arrival_availability,
)
from app.domain.drive_time import (
    DriveTimeCache,
    haversine_distance_km,
    estimate_drive_time_minutes,
)
from app.domain.reasons import generate_recommendation_reasons
from app.domain.scoring import (
    CandidateScoreInput,
    ScoredLot,
    rank_candidate_lots,
)


_drive_time_cache = DriveTimeCache()


@dataclass(frozen=True)
class CandidateLotDto:
    lot_id: UUID
    name: str
    latitude: float
    longitude: float
    price_for_duration: float
    held_quota: int
    snapshots: list[AvailabilitySnapshot]


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

    cache = drive_time_cache or _drive_time_cache
    score_inputs: list[CandidateScoreInput] = []
    fallback_used = False
    fallback_reasons: list[str] = []

    for c in candidates:
        # 1. Drive time estimation with caching
        drive_time = cache.get(customer_latitude, customer_longitude, c.latitude, c.longitude)
        if drive_time is None:
            dist_km = haversine_distance_km(customer_latitude, customer_longitude, c.latitude, c.longitude)
            drive_time = estimate_drive_time_minutes(dist_km)
            cache.set(customer_latitude, customer_longitude, c.latitude, c.longitude, drive_time)

        # 2. Arrival availability estimation (BR-08.e)
        arrival_estimate = estimate_arrival_availability(
            lot_id=c.lot_id,
            vehicle_type=vehicle_type,
            arrival_at=arrival_at,
            held_quota_at_arrival=c.held_quota,
            snapshots=c.snapshots,
        )

        score_inputs.append(CandidateScoreInput(
            lot_id=c.lot_id,
            name=c.name,
            drive_time_minutes=drive_time,
            probability_percent=arrival_estimate.probability_percent,
            price=c.price_for_duration,
        ))

    # 3. Score and rank candidates
    scored_lots = rank_candidate_lots(
        candidates=score_inputs,
        top_k=top_k,
        generate_reasons_fn=generate_recommendation_reasons,
    )

    return RecommendationResult(
        recommended_lots=scored_lots,
        fallback_used=fallback_used,
        fallback_reason="; ".join(fallback_reasons) if fallback_reasons else None,
    )
