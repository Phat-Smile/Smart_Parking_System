"""P0 statistical arrival-availability estimate on simulated 15-minute snapshots.

Parking owns physical slot counts; Booking owns held quotas. This estimator only
consumes caller-provided snapshots and never makes booking/admission decisions.
"""

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from uuid import UUID


# Vietnam does not observe DST. A fixed offset avoids requiring tzdata on Windows.
LOT_TIMEZONE = timezone(timedelta(hours=7), name="Asia/Ho_Chi_Minh")
MIN_RELIABLE_SAMPLES = 4


@dataclass(frozen=True)
class AvailabilitySnapshot:
    lot_id: UUID
    vehicle_type: str
    captured_at: datetime
    available_slots: int


@dataclass(frozen=True)
class ArrivalEstimate:
    probability_percent: float
    sample_count: int
    baseline_source: str
    estimated: bool
    insufficient_data: bool
    warning: str | None


def _quarter_hour(datetime_value: datetime) -> int:
    local = datetime_value.astimezone(LOT_TIMEZONE)
    return local.hour * 4 + local.minute // 15


def estimate_arrival_availability(
    lot_id: UUID,
    vehicle_type: str,
    arrival_at: datetime,
    held_quota_at_arrival: int,
    snapshots: list[AvailabilitySnapshot],
) -> ArrivalEstimate:
    """Estimate Pr(free physical slot > held bookings) at arrival time.

    First use the same lot, weekday and 15-minute slot; next use the same
    lot/time-of-day; then use peer lots for the same vehicle group and time;
    finally default to 50%. Historical snapshots are synthetic for OJT.
    Laplace smoothing prevents a single synthetic sample implying certainty.
    """
    if arrival_at.utcoffset() is None:
        raise ValueError("arrival_at must contain a timezone offset")
    if held_quota_at_arrival < 0:
        raise ValueError("held quota cannot be negative")

    bucket = _quarter_hour(arrival_at)
    weekday = arrival_at.astimezone(LOT_TIMEZONE).weekday()
    candidate = [s for s in snapshots if s.vehicle_type.casefold() == vehicle_type.casefold()
                 and s.captured_at < arrival_at
                 and _quarter_hour(s.captured_at) == bucket]
    matched = [s for s in candidate if s.lot_id == lot_id
               and s.captured_at.astimezone(LOT_TIMEZONE).weekday() == weekday]
    source = "lot_weekday_15m"
    if not matched:
        matched = [s for s in candidate if s.lot_id == lot_id]
        source = "lot_daily_15m"
    if not matched:
        matched = [s for s in candidate if s.lot_id != lot_id]
        source = "peer_daily_15m"

    if matched:
        free_at_arrival = sum(s.available_slots > held_quota_at_arrival for s in matched)
        probability = (free_at_arrival + 1) / (len(matched) + 2)
    else:
        probability = 0.5
        source = "default_50_percent"

    percent = round(probability * 100, 2)
    return ArrivalEstimate(
        probability_percent=percent,
        sample_count=len(matched),
        baseline_source=source,
        estimated=True,
        insufficient_data=len(matched) < MIN_RELIABLE_SAMPLES,
        warning="Khả năng còn chỗ thấp (ước tính)." if percent < 30 else None,
    )
