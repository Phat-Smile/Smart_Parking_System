#!/usr/bin/env python3
"""Simulated Arrival History Generator for Smart Parking Demo.

Generates realistic historical completed parking sessions over a configured
time window (28-56 days) for approved parking lots (A, B, C) in the demo catalog.

Rules & Invariants Enforced:
1. Strictly deterministic given the same seed (--seed).
2. Capacity constraint: Active vehicles never exceed zone capacity at any point in time.
3. Plate consistency: Normalized uppercase plates (SIMCxxx, SIMMxxx), max 20 chars,
   no overlapping sessions for the same plate across the entire time window.
4. Schema fidelity: Matches EF Core parking_sessions schema exactly.
   Status is 'Completed', entry_time < last_paid_at <= exit_time (within 15 min exit window).
5. Safe deletion: Produces Cleanup-ArrivalHistory.sql with prefix-targeted DELETE.
"""

import argparse
import datetime
import os
import random
import sys
import uuid
from typing import Dict, List, Tuple

from profiles import LOT_PROFILES, LotProfile


def format_timestamp(dt: datetime.datetime) -> str:
    """Format datetime to ISO 8601 with UTC offset (+00:00)."""
    return dt.strftime("%Y-%m-%d %H:%M:%S+00")


class ArrivalHistoryGenerator:
    def __init__(self, days: int = 35, seed: int = 42, end_date: datetime.datetime = None):
        if not (28 <= days <= 56):
            raise ValueError(f"Days must be between 28 and 56, got {days}")
        self.days = days
        self.seed = seed
        self.rng = random.Random(seed)

        # Fixed reference point for deterministic generation
        if end_date is None:
            self.end_time = datetime.datetime(2026, 11, 13, 23, 59, 59, tzinfo=datetime.timezone.utc)
        else:
            self.end_time = end_date
        self.start_time = self.end_time - datetime.timedelta(days=days)

        # Plate pools with availability tracker
        self.car_plates = [f"SIMC{i:03d}" for i in range(1, 51)]
        self.moto_plates = [f"SIMM{i:03d}" for i in range(1, 101)]
        self.plate_available_at: Dict[str, datetime.datetime] = {}

    def _get_available_plate(self, vehicle_type: str, at_time: datetime.datetime) -> str:
        """Find an available plate of the given type, or rotate to least recently used."""
        pool = self.car_plates if vehicle_type == "Car" else self.moto_plates
        available = [p for p in pool if self.plate_available_at.get(p, self.start_time) <= at_time]

        if available:
            # Deterministically choose from available
            chosen = self.rng.choice(available)
        else:
            # Fallback: pick the plate whose session ends earliest (if at_time >= that end time)
            sorted_by_avail = sorted(pool, key=lambda p: self.plate_available_at.get(p, self.start_time))
            chosen = sorted_by_avail[0]
            if self.plate_available_at.get(chosen, self.start_time) > at_time:
                # If even the earliest is still parked, create a new deterministic plate
                new_idx = len(pool) + 1
                prefix = "SIMC" if vehicle_type == "Car" else "SIMM"
                chosen = f"{prefix}{new_idx:03d}"
                pool.append(chosen)

        return chosen

    def generate_sessions_for_profile(self, profile: LotProfile) -> List[Dict]:
        """Generate sessions for a specific lot profile using track simulation to guarantee capacity."""
        sessions = []

        # Each capacity slot is simulated as an independent track of sequential non-overlapping sessions
        for slot_idx in range(profile.capacity):
            curr_time = self.start_time + datetime.timedelta(
                minutes=self.rng.randint(0, 120)
            )

            while curr_time < self.end_time:
                hour = curr_time.hour
                is_weekend = curr_time.weekday() >= 5
                weight = profile.weekday_hourly_weights[hour]
                if is_weekend:
                    weight *= profile.weekend_factor

                # Decide if a vehicle arrives in this interval
                # Scale arrival probability: if weight > 0.5, high chance
                arrival_prob = min(0.95, weight * 1.1)

                if self.rng.random() < arrival_prob:
                    # Vehicle arrives!
                    duration_minutes = self.rng.randint(
                        profile.min_duration_minutes, profile.max_duration_minutes
                    )
                    entry_time = curr_time
                    exit_time = entry_time + datetime.timedelta(minutes=duration_minutes)

                    if exit_time > self.end_time:
                        # Session would extend past simulation end; stop track
                        break

                    # Payment happens 2 to 10 minutes before exit (well within 15 min BR-09.d exit window)
                    payment_lead_minutes = self.rng.randint(2, min(10, max(2, duration_minutes // 2)))
                    last_paid_at = exit_time - datetime.timedelta(minutes=payment_lead_minutes)

                    plate = self._get_available_plate(profile.vehicle_type, entry_time)
                    # Plate cooldown: plate leaves lot and takes at least 20 mins to return
                    self.plate_available_at[plate] = exit_time + datetime.timedelta(minutes=20)

                    # Generate deterministic UUID and QR token
                    session_id = uuid.UUID(bytes=self.rng.randbytes(16), version=4)
                    qr_token = f"SIM_QR_{session_id.hex}"

                    sessions.append({
                        "id": str(session_id),
                        "lot_id": profile.lot_id,
                        "zone_id": profile.zone_id,
                        "plate_number": plate,
                        "vehicle_type": profile.vehicle_type,
                        "status": "Completed",
                        "entry_time": entry_time,
                        "exit_time": exit_time,
                        "last_paid_at": last_paid_at,
                        "qr_token": qr_token,
                    })

                    # Slot track becomes available after exit + random idle time (5 to 60 minutes)
                    idle_minutes = self.rng.randint(5, 60)
                    curr_time = exit_time + datetime.timedelta(minutes=idle_minutes)
                else:
                    # No arrival in this slice, advance 20-40 minutes
                    curr_time += datetime.timedelta(minutes=self.rng.randint(20, 40))

        # Sort chronologically by entry_time
        sessions.sort(key=lambda s: s["entry_time"])
        return sessions

    def generate_all(self) -> Tuple[List[Dict], Dict[str, int]]:
        """Generate sessions for all profiles."""
        all_sessions = []
        counts_by_lot = {}

        for profile in LOT_PROFILES:
            lot_sessions = self.generate_sessions_for_profile(profile)
            all_sessions.extend(lot_sessions)
            counts_by_lot[profile.name] = len(lot_sessions)

        # Sort all sessions by entry_time
        all_sessions.sort(key=lambda s: s["entry_time"])
        return all_sessions, counts_by_lot


def generate_sql_scripts(sessions: List[Dict], out_dir: str, days: int, seed: int):
    """Write Seed-ArrivalHistory.sql and Cleanup-ArrivalHistory.sql."""
    os.makedirs(out_dir, exist_ok=True)
    seed_file = os.path.join(out_dir, "Seed-ArrivalHistory.sql")
    cleanup_file = os.path.join(out_dir, "Cleanup-ArrivalHistory.sql")

    # 1. Write Seed SQL
    with open(seed_file, "w", encoding="utf-8") as f:
        f.write("-- =====================================================================\n")
        f.write("-- Seed-ArrivalHistory.sql\n")
        f.write(f"-- Deterministically generated by generate_arrival_history.py\n")
        f.write(f"-- Days: {days}, Seed: {seed}, Generated Sessions: {len(sessions)}\n")
        f.write("-- Target Table: parking.parking_sessions\n")
        f.write("-- DO NOT run directly in production. Intended for parking_demo development.\n")
        f.write("-- =====================================================================\n\n")

        # Insert in chunks of 100 to optimize PostgreSQL execution
        chunk_size = 100
        for i in range(0, len(sessions), chunk_size):
            chunk = sessions[i : i + chunk_size]
            f.write("INSERT INTO parking.parking_sessions\n")
            f.write("    (id, lot_id, zone_id, plate_number, vehicle_type, status, entry_time, exit_time, last_paid_at, qr_token)\n")
            f.write("VALUES\n")

            value_lines = []
            for s in chunk:
                entry_str = format_timestamp(s["entry_time"])
                exit_str = format_timestamp(s["exit_time"])
                paid_str = format_timestamp(s["last_paid_at"])
                line = (
                    f"    ('{s['id']}', '{s['lot_id']}', '{s['zone_id']}', '{s['plate_number']}', "
                    f"'{s['vehicle_type']}', '{s['status']}', TIMESTAMPTZ '{entry_str}', "
                    f"TIMESTAMPTZ '{exit_str}', TIMESTAMPTZ '{paid_str}', '{s['qr_token']}')"
                )
                value_lines.append(line)

            f.write(",\n".join(value_lines))
            f.write("\nON CONFLICT (id) DO NOTHING;\n\n")

    # 2. Write Cleanup SQL
    with open(cleanup_file, "w", encoding="utf-8") as f:
        f.write("-- =====================================================================\n")
        f.write("-- Cleanup-ArrivalHistory.sql\n")
        f.write("-- Safely removes all simulated arrival history sessions without affecting real sessions.\n")
        f.write("-- Target Table: parking.parking_sessions\n")
        f.write("-- =====================================================================\n\n")
        f.write("DELETE FROM parking.parking_sessions\n")
        f.write("WHERE plate_number LIKE 'SIMC%' OR plate_number LIKE 'SIMM%';\n")

    return seed_file, cleanup_file


def main():
    parser = argparse.ArgumentParser(description="Generate simulated arrival history for Smart Parking.")
    parser.add_argument("--days", type=int, default=35, help="Simulation duration in days (28 to 56, default: 35)")
    parser.add_argument("--seed", type=int, default=42, help="Random seed for deterministic generation (default: 42)")
    parser.add_argument("--out-dir", type=str, default=".", help="Output directory for SQL files (default: .)")
    parser.add_argument("--dry-run", action="store_true", help="Print summary without writing SQL files")

    args = parser.parse_args()

    print(f"Generating arrival history (days={args.days}, seed={args.seed})...")
    generator = ArrivalHistoryGenerator(days=args.days, seed=args.seed)
    sessions, counts = generator.generate_all()

    print(f"Total sessions generated: {len(sessions)}")
    for name, count in counts.items():
        print(f"  - {name}: {count} sessions")

    if not args.dry_run:
        seed_path, cleanup_path = generate_sql_scripts(sessions, args.out_dir, args.days, args.seed)
        print(f"Successfully written:")
        print(f"  - {seed_path}")
        print(f"  - {cleanup_path}")


if __name__ == "__main__":
    main()
