"""Internal baseline prediction contract; Parking and Booking supply input data."""

import os
from hmac import compare_digest
from datetime import datetime
from typing import Literal
from uuid import UUID

from fastapi import APIRouter, Depends, Header, HTTPException
from pydantic import BaseModel, Field, field_validator

from app.application.arrival_availability import (
    AvailabilitySnapshot,
    estimate_arrival_availability,
)


router = APIRouter(prefix="/internal/ai", tags=["arrival-availability"])


class SnapshotInput(BaseModel):
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


class ArrivalAvailabilityRequest(BaseModel):
    lotId: UUID
    vehicleType: Literal["Car", "Motorbike"]
    arrivalAt: datetime
    heldQuotaAtArrival: int = Field(ge=0)
    snapshots: list[SnapshotInput] = Field(default_factory=list, max_length=5000)

    @field_validator("arrivalAt")
    @classmethod
    def require_tz(cls, value: datetime) -> datetime:
        if value.utcoffset() is None:
            raise ValueError("Arrival timestamp must contain timezone information")
        return value


class ArrivalAvailabilityResponse(BaseModel):
    lotId: UUID
    vehicleType: str
    arrivalAt: datetime
    probabilityPercent: float
    sampleCount: int
    baselineSource: str
    estimated: bool
    insufficientData: bool
    warning: str | None


def require_service_key(x_arrival_service_key: str | None = Header(default=None)) -> None:
    expected = os.getenv("ARRIVAL_API_KEY", "")
    if not expected:
        raise HTTPException(status_code=503, detail="Arrival prediction is not configured")
    if not x_arrival_service_key or not compare_digest(x_arrival_service_key, expected):
        raise HTTPException(status_code=403, detail="Forbidden")


@router.post("/arrival-availability", response_model=ArrivalAvailabilityResponse,
             dependencies=[Depends(require_service_key)])
def get_arrival_availability(request: ArrivalAvailabilityRequest) -> ArrivalAvailabilityResponse:
    estimate = estimate_arrival_availability(
        request.lotId,
        request.vehicleType,
        request.arrivalAt,
        request.heldQuotaAtArrival,
        [AvailabilitySnapshot(s.lotId, s.vehicleType, s.capturedAt, s.availableSlots)
         for s in request.snapshots],
    )
    return ArrivalAvailabilityResponse(
        lotId=request.lotId,
        vehicleType=request.vehicleType,
        arrivalAt=request.arrivalAt,
        probabilityPercent=estimate.probability_percent,
        sampleCount=estimate.sample_count,
        baselineSource=estimate.baseline_source,
        estimated=estimate.estimated,
        insufficientData=estimate.insufficient_data,
        warning=estimate.warning,
    )
