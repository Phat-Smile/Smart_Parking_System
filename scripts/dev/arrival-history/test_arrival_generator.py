"""Unit tests for the Arrival History Generator (WP-AI-D1).

Tests determinism, capacity constraints, plate overlap constraints,
schema invariants, and SQL output safety.
"""

import datetime
import os
import tempfile
import unittest

from generate_arrival_history import ArrivalHistoryGenerator, generate_sql_scripts
from profiles import LOT_PROFILES


class TestArrivalHistoryGenerator(unittest.TestCase):
    def setUp(self):
        self.days = 30
        self.seed = 12345
        self.generator = ArrivalHistoryGenerator(days=self.days, seed=self.seed)
        self.sessions, self.counts = self.generator.generate_all()

    def test_determinism(self):
        """Same seed and days must generate exactly identical sessions."""
        gen2 = ArrivalHistoryGenerator(days=self.days, seed=self.seed)
        sessions2, counts2 = gen2.generate_all()

        self.assertEqual(len(self.sessions), len(sessions2))
        self.assertEqual(self.counts, counts2)
        for s1, s2 in zip(self.sessions, sessions2):
            self.assertEqual(s1["id"], s2["id"])
            self.assertEqual(s1["plate_number"], s2["plate_number"])
            self.assertEqual(s1["entry_time"], s2["entry_time"])
            self.assertEqual(s1["exit_time"], s2["exit_time"])
            self.assertEqual(s1["last_paid_at"], s2["last_paid_at"])

    def test_capacity_constraint_never_exceeded(self):
        """At no point in time does active occupancy exceed zone capacity."""
        # Check per lot profile
        for profile in LOT_PROFILES:
            lot_sessions = [s for s in self.sessions if s["lot_id"] == profile.lot_id]

            # Collect timeline events: (+1 at entry_time, -1 at exit_time)
            events = []
            for s in lot_sessions:
                events.append((s["entry_time"], 1))
                events.append((s["exit_time"], -1))

            # Sort events: if same time, exit (-1) before entry (+1)
            events.sort(key=lambda x: (x[0], x[1]))

            current_occupancy = 0
            max_occupancy = 0
            for dt, delta in events:
                current_occupancy += delta
                if current_occupancy > max_occupancy:
                    max_occupancy = current_occupancy
                self.assertLessEqual(
                    current_occupancy,
                    profile.capacity,
                    f"Occupancy {current_occupancy} exceeded capacity {profile.capacity} at {dt} for {profile.name}",
                )

            self.assertGreater(max_occupancy, 0, f"Profile {profile.name} should have non-zero max occupancy")

    def test_no_overlapping_sessions_for_same_plate(self):
        """No vehicle plate can be parked in multiple places at the same time."""
        plate_sessions = {}
        for s in self.sessions:
            plate = s["plate_number"]
            plate_sessions.setdefault(plate, []).append(s)

        for plate, sessions in plate_sessions.items():
            # Sort chronologically by entry_time
            sessions.sort(key=lambda s: s["entry_time"])
            for i in range(len(sessions) - 1):
                prev_exit = sessions[i]["exit_time"]
                next_entry = sessions[i + 1]["entry_time"]
                self.assertLessEqual(
                    prev_exit,
                    next_entry,
                    f"Plate {plate} has overlapping sessions: exit {prev_exit} vs entry {next_entry}",
                )

    def test_schema_invariants(self):
        """Verify EF Core schema constraints: exit > entry, last_paid within 15 min, plate format."""
        qr_tokens = set()

        for s in self.sessions:
            # 1. Temporal invariants
            self.assertLess(s["entry_time"], s["exit_time"])
            self.assertLess(s["entry_time"], s["last_paid_at"])
            self.assertLessEqual(s["last_paid_at"], s["exit_time"])
            exit_window = s["exit_time"] - s["last_paid_at"]
            self.assertLessEqual(exit_window, datetime.timedelta(minutes=15))

            # 2. Status & Vehicle type
            self.assertEqual(s["status"], "Completed")
            self.assertIn(s["vehicle_type"], ["Car", "Motorbike"])

            # 3. Plate format & length
            self.assertLessEqual(len(s["plate_number"]), 20)
            self.assertTrue(s["plate_number"].startswith("SIMC") or s["plate_number"].startswith("SIMM"))
            self.assertEqual(s["plate_number"], s["plate_number"].upper())

            # 4. QR Token uniqueness & length
            self.assertLessEqual(len(s["qr_token"]), 64)
            self.assertNotIn(s["qr_token"], qr_tokens)
            qr_tokens.add(s["qr_token"])

    def test_sql_generation_and_cleanup_files(self):
        """Test SQL output formatting and cleanup statement validity."""
        with tempfile.TemporaryDirectory() as tmpdir:
            seed_file, cleanup_file = generate_sql_scripts(self.sessions, tmpdir, self.days, self.seed)

            self.assertTrue(os.path.exists(seed_file))
            self.assertTrue(os.path.exists(cleanup_file))

            # Verify seed SQL content
            with open(seed_file, "r", encoding="utf-8") as f:
                seed_content = f.read()
                self.assertIn("INSERT INTO parking.parking_sessions", seed_content)
                self.assertIn("ON CONFLICT (id) DO NOTHING;", seed_content)
                self.assertIn("SIMC", seed_content)
                self.assertIn("SIMM", seed_content)

            # Verify cleanup SQL content
            with open(cleanup_file, "r", encoding="utf-8") as f:
                cleanup_content = f.read()
                self.assertIn("DELETE FROM parking.parking_sessions", cleanup_content)
                self.assertIn("WHERE plate_number LIKE 'SIMC%' OR plate_number LIKE 'SIMM%';", cleanup_content)


if __name__ == "__main__":
    unittest.main()
