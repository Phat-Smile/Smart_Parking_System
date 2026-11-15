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


def _quarter_difference(center_bucket: int, sample_bucket: int) -> int:
    """Return signed difference sample_bucket - center_bucket in [-1, 0, 1], or -999 if outside."""
    diff = (sample_bucket - center_bucket) % 96
    if diff == 95:
        return -1
    elif diff == 1:
        return 1
    elif diff == 0:
        return 0
    return -999


def estimate_arrival_availability(
    lot_id: UUID,
    vehicle_type: str,
    arrival_at: datetime,
    held_quota_at_arrival: int,
    snapshots: list[AvailabilitySnapshot],
) -> ArrivalEstimate:
    """Estimate Pr(free physical slot > held bookings) at arrival time using 3-window smoothing.

    Uses a symmetric 3-window 15-minute neighborhood around arrival time:
    - Center window (w0 = 0.50)
    - Adjacent windows (-15m, +15m) (w_prev = 0.25, w_next = 0.25)
    Midnight crossing shifts timestamps into the center frame for weekday matching.
    Weighted Jeffreys prior smoothing (Beta(0.5, 0.5)) prevents jumpy rankings without inventing artificial certainties.
    """
    if arrival_at.utcoffset() is None:
        raise ValueError("arrival_at must contain a timezone offset")
    if held_quota_at_arrival < 0:
        raise ValueError("held quota cannot be negative")

    center_bucket = _quarter_hour(arrival_at)
    weekday = arrival_at.astimezone(LOT_TIMEZONE).weekday()

    # Pre-filter by vehicle type, historical timing, and 3-window quarter difference
    candidates: list[tuple[AvailabilitySnapshot, float, bool]] = []
    for s in snapshots:
        if s.vehicle_type.casefold() != vehicle_type.casefold():
            continue
        if s.captured_at >= arrival_at:
            continue
        s_bucket = _quarter_hour(s.captured_at)
        delta = _quarter_difference(center_bucket, s_bucket)
        if delta == -999:
            continue
        weight = 0.50 if delta == 0 else 0.25
        # Weekday comparison across midnight: align snapshot time to center frame (option b)
        aligned_time = s.captured_at - timedelta(minutes=15 * delta)
        is_same_weekday = (aligned_time.astimezone(LOT_TIMEZONE).weekday() == weekday)
        candidates.append((s, weight, is_same_weekday))

    # Hierarchical matching:
    # 1. lot_weekday_15m
    matched = [(s, w) for s, w, is_same_day in candidates if s.lot_id == lot_id and is_same_day]
    source = "lot_weekday_15m"

    # 2. lot_daily_15m (all weekdays of the same lot)
    if not matched:
        matched = [(s, w) for s, w, _ in candidates if s.lot_id == lot_id]
        source = "lot_daily_15m"

    # 3. peer_daily_15m (peer lots)
    if not matched:
        matched = [(s, w) for s, w, _ in candidates if s.lot_id != lot_id]
        source = "peer_daily_15m"

    if matched:
        k_weighted = sum(w for s, w in matched if s.available_slots > held_quota_at_arrival)
        n_weighted = sum(w for _, w in matched)
        # Jeffreys prior Beta(0.5, 0.5): (k + 0.5) / (n + 1.0)
        # Deepens minimum floor to 10% and elevates ceiling to 90%, improving calibration.
        probability = (k_weighted + 0.5) / (n_weighted + 1.0)
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
