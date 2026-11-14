"""Lot profiles and personality configurations for arrival history simulation.

All IDs match scripts/database/Seed-ParkingCatalog.sql and Seed-ParkingLayout.sql.
"""

from dataclasses import dataclass
from typing import List


@dataclass(frozen=True)
class LotProfile:
    lot_id: str
    name: str
    zone_id: str
    vehicle_type: str  # "Car" or "Motorbike"
    capacity: int
    plate_prefix: str
    # 24 hourly arrival probability weights (hour 0 to 23)
    weekday_hourly_weights: List[float]
    weekend_factor: float  # Multiplier for weekend activity
    min_duration_minutes: int
    max_duration_minutes: int


LOT_PROFILES: List[LotProfile] = [
    # Lot A: Central MultiStorey - 2 Car spots
    LotProfile(
        lot_id="10000000-0000-0000-0000-000000000001",
        name="Bãi đỗ Trung tâm A",
        zone_id="30000000-0000-0000-0000-000000000001",
        vehicle_type="Car",
        capacity=2,
        plate_prefix="SIMC",
        weekday_hourly_weights=[
            0.05, 0.02, 0.01, 0.01, 0.02, 0.05,  # 00:00 - 05:59
            0.15, 0.45, 0.90, 0.85, 0.60, 0.50,  # 06:00 - 11:59 (morning peak 8-10)
            0.70, 0.55, 0.50, 0.60, 0.85, 0.95,  # 12:00 - 17:59 (lunch & afternoon peak 16-18)
            0.75, 0.50, 0.35, 0.20, 0.15, 0.08   # 18:00 - 23:59
        ],
        weekend_factor=0.55,
        min_duration_minutes=30,
        max_duration_minutes=240,
    ),
    # Lot B: Outdoor Motorbike - 20 Motorbike capacity
    LotProfile(
        lot_id="10000000-0000-0000-0000-000000000002",
        name="Bãi đỗ Ngoài trời B",
        zone_id="30000000-0000-0000-0000-000000000002",
        vehicle_type="Motorbike",
        capacity=20,
        plate_prefix="SIMM",
        weekday_hourly_weights=[
            0.02, 0.01, 0.01, 0.01, 0.05, 0.15,  # 00:00 - 05:59
            0.40, 0.90, 0.95, 0.70, 0.60, 0.65,  # 06:00 - 11:59 (morning rush 7-9)
            0.75, 0.60, 0.55, 0.65, 0.85, 0.95,  # 12:00 - 17:59 (evening rush 16-18)
            0.80, 0.60, 0.40, 0.25, 0.15, 0.05   # 18:00 - 23:59
        ],
        weekend_factor=0.80,
        min_duration_minutes=20,
        max_duration_minutes=180,
    ),
    # Lot C: Underground Office Car - 1 Car spot
    LotProfile(
        lot_id="10000000-0000-0000-0000-000000000003",
        name="Bãi đỗ Tầng hầm C",
        zone_id="30000000-0000-0000-0000-000000000003",
        vehicle_type="Car",
        capacity=1,
        plate_prefix="SIMC",
        weekday_hourly_weights=[
            0.00, 0.00, 0.00, 0.00, 0.00, 0.00,  # 00:00 - 05:59
            0.05, 0.30, 0.95, 0.40, 0.10, 0.05,  # 06:00 - 11:59 (arrives 8-9)
            0.05, 0.05, 0.05, 0.05, 0.10, 0.20,  # 12:00 - 17:59
            0.05, 0.00, 0.00, 0.00, 0.00, 0.00   # 18:00 - 23:59
        ],
        weekend_factor=0.05,
        min_duration_minutes=180,
        max_duration_minutes=540,
    ),
]
