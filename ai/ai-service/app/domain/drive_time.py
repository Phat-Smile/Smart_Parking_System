"""Drive time calculation and geospatial caching (FR-MAP-002, BR-08).

Pure mathematical formulas with fallback mechanisms and TTL in-memory caching.
Invariants:
- 60-second TTL cache keyed on rounded coordinates (3 decimal places ~ 110m).
- Haversine great-circle distance with urban traffic speed baseline (25 km/h + 2 min delay).
- Fails safe: always returns a valid drive time estimate or None, never raises.
"""

import math
import time
from dataclasses import dataclass
from typing import Dict, Tuple


EARTH_RADIUS_KM = 6371.0
DEFAULT_CACHE_TTL_SECONDS = 60.0
URBAN_SPEED_KMH = 25.0
BASE_TRAFFIC_DELAY_MINUTES = 2.0


def haversine_distance_km(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
    """Calculate the great-circle distance between two points on Earth in kilometers."""
    phi1 = math.radians(lat1)
    phi2 = math.radians(lat2)
    delta_phi = math.radians(lat2 - lat1)
    delta_lambda = math.radians(lon2 - lon1)

    a = (math.sin(delta_phi / 2.0) ** 2 +
         math.cos(phi1) * math.cos(phi2) * (math.sin(delta_lambda / 2.0) ** 2))
    c = 2.0 * math.atan2(math.sqrt(a), math.sqrt(1.0 - a))

    return EARTH_RADIUS_KM * c


def estimate_drive_time_minutes(distance_km: float) -> float:
    """Estimate driving time in minutes based on distance and urban traffic conditions."""
    if distance_km <= 0.05:
        return 1.0
    travel_time = (distance_km / URBAN_SPEED_KMH) * 60.0
    total_minutes = BASE_TRAFFIC_DELAY_MINUTES + travel_time
    return round(max(1.0, total_minutes), 1)


@dataclass
class CacheEntry:
    value: float
    timestamp: float


class DriveTimeCache:
    """Thread-safe in-memory cache with TTL for drive times between coordinates."""

    def __init__(self, ttl_seconds: float = DEFAULT_CACHE_TTL_SECONDS):
        self._ttl = ttl_seconds
        self._store: Dict[Tuple[float, float, float, float], CacheEntry] = {}

    @staticmethod
    def _round_coord(coord: float) -> float:
        return round(coord, 3)

    def get(self, lat1: float, lon1: float, lat2: float, lon2: float) -> float | None:
        key = (
            self._round_coord(lat1),
            self._round_coord(lon1),
            self._round_coord(lat2),
            self._round_coord(lon2),
        )
        entry = self._store.get(key)
        if entry is None:
            return None
        if (time.time() - entry.timestamp) > self._ttl:
            del self._store[key]
            return None
        return entry.value

    def set(self, lat1: float, lon1: float, lat2: float, lon2: float, value: float) -> None:
        key = (
            self._round_coord(lat1),
            self._round_coord(lon1),
            self._round_coord(lat2),
            self._round_coord(lon2),
        )
        self._store[key] = CacheEntry(value=value, timestamp=time.time())

    def clear(self) -> None:
        self._store.clear()
