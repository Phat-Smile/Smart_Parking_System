"""Internal recommendation API contract (US-AI-001, FR-AI-001, BR-08).

Consumes candidate parking lots from Parking Service and returns Top-3 utility-ranked
recommendations with explainable reasons. Protected by ARRIVAL_API_KEY.
"""

from datetime import datetime
from typing import Literal
from uuid import UUID

from fastapi import APIRouter, Depends
from pydantic import BaseModel, Field, field_validator

from app.api.arrival_availability import require_service_key
from app.application.arrival_availability import AvailabilitySnapshot
from app.application.recommend_lots import (
    CandidateLotDto,
    recommend_parking_lots,
)


router = APIRouter(prefix="/internal/ai", tags=["recommendations"])


class CandidateSnapshotInput(BaseModel):
    lotId: UUID
    vehicleType: Literal["Car", "Motorbike"]
    capturedAt: datetime
    availableSlots: int = Field(ge=0)

    @field_validator("capturedAt")
    @classmethod
    def require_tz(cls, value: datetime) -> datetime:
        if value.utcoffset() is None:
            raise ValueError("Timestamp must contain timezone information")
        return value


class CandidateLotRequest(BaseModel):
    lotId: UUID
    name: str
    latitude: float = Field(ge=-90.0, le=90.0)
    longitude: float = Field(ge=-180.0, le=180.0)
    priceForDuration: float = Field(ge=0.0)
    heldQuota: int = Field(default=0, ge=0)
    heldQuotaKnown: bool = Field(default=True)
    snapshots: list[CandidateSnapshotInput] = Field(default_factory=list, max_length=5000)


class RecommendationRequest(BaseModel):
    customerLatitude: float = Field(ge=-90.0, le=90.0)
    customerLongitude: float = Field(ge=-180.0, le=180.0)
    vehicleType: Literal["Car", "Motorbike"]
    arrivalAt: datetime
    durationMinutes: int = Field(default=120, ge=15, le=1440)
    candidateLots: list[CandidateLotRequest] = Field(default_factory=list, max_length=10)

    @field_validator("arrivalAt")
    @classmethod
    def require_tz(cls, value: datetime) -> datetime:
        if value.utcoffset() is None:
            raise ValueError("Arrival timestamp must contain timezone information")
        return value


class RecommendedLotItem(BaseModel):
    lotId: UUID
    name: str
    rank: int
    driveTimeMinutes: float | None
    probabilityPercent: float | None = None
    price: float
    warningHighFullRisk: bool
    reasons: list[str]
    sampleCount: int = Field(default=0)
    insufficientData: bool = Field(default=False)
    baselineSource: str | None = None


class RecommendationResponse(BaseModel):
    recommendedLots: list[RecommendedLotItem]
    fallbackUsed: bool
    fallbackReason: str | None


@router.post("/recommendations", response_model=RecommendationResponse,
             dependencies=[Depends(require_service_key)])
def get_recommendations(request: RecommendationRequest) -> RecommendationResponse:
    candidates = [
        CandidateLotDto(
            lot_id=c.lotId,
            name=c.name,
            latitude=c.latitude,
            longitude=c.longitude,
            price_for_duration=c.priceForDuration,
            held_quota=c.heldQuota,
            held_quota_known=c.heldQuotaKnown,
            snapshots=[
                AvailabilitySnapshot(
                    lot_id=s.lotId,
                    vehicle_type=s.vehicleType,
                    captured_at=s.capturedAt,
                    available_slots=s.availableSlots,
                )
                for s in c.snapshots
            ],
        )
        for c in request.candidateLots
    ]

    result = recommend_parking_lots(
        customer_latitude=request.customerLatitude,
        customer_longitude=request.customerLongitude,
        vehicle_type=request.vehicleType,
        arrival_at=request.arrivalAt,
        duration_minutes=request.durationMinutes,
        candidates=candidates,
        top_k=3,
    )

    return RecommendationResponse(
        recommendedLots=[
            RecommendedLotItem(
                lotId=item.lot_id,
                name=item.name,
                rank=item.rank,
                driveTimeMinutes=item.drive_time_minutes,
                probabilityPercent=item.probability_percent,
                price=item.price,
                warningHighFullRisk=item.warning_high_full_risk,
                reasons=item.reasons,
                sampleCount=item.sample_count,
                insufficientData=item.insufficient_data,
                baselineSource=item.baseline_source,
            )
            for item in result.recommended_lots
        ],
        fallbackUsed=result.fallback_used,
        fallbackReason=result.fallback_reason,
    )
