"""Dependency-free guard and contract tests; DB integration still pending."""
import importlib.util
from pathlib import Path
import sys
import unittest
from unittest.mock import patch, Mock

FILE = Path(__file__).resolve().parents[2] / "scripts/database/run_parking_seed.py"
spec = importlib.util.spec_from_file_location("parking_seed", FILE)
seed = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = seed
spec.loader.exec_module(seed)


class SeedRunnerTests(unittest.TestCase):
    def test_dry_run_succeeds_without_environment(self):
        with patch.dict("os.environ", {}, clear=True):
            self.assertEqual(seed.main([]), 0)

    def test_production_refused(self):
        env = {"PGDATABASE": "parking_system", "PGHOST": "localhost", "PGUSER": "dev",
               "ASPNETCORE_ENVIRONMENT": "Production"}
        with patch.dict("os.environ", env, clear=True):
            self.assertEqual(seed.main(["--apply", "--confirm-disposable", seed.ACK]), 2)

    def test_requires_explicit_ack(self):
        env = {"PGDATABASE": "parking_system", "PGHOST": "localhost", "PGUSER": "dev",
               "ASPNETCORE_ENVIRONMENT": "Development"}
        with patch.dict("os.environ", env, clear=True):
            self.assertEqual(seed.main(["--apply"]), 2)

    def test_refuses_default_postgres_database(self):
        env = {"PGDATABASE": "postgres", "PGHOST": "localhost", "PGUSER": "dev",
               "ASPNETCORE_ENVIRONMENT": "Testing"}
        with patch.dict("os.environ", env, clear=True):
            self.assertEqual(seed.main(["--apply", "--confirm-disposable", seed.ACK]), 2)

    def test_requires_psql_binary(self):
        env = {"PGDATABASE": "parking_system", "PGHOST": "localhost", "PGUSER": "dev",
               "ASPNETCORE_ENVIRONMENT": "Development"}
        with patch.dict("os.environ", env, clear=True), patch.object(seed.shutil, "which", return_value=None):
            self.assertEqual(seed.main(["--apply", "--confirm-disposable", seed.ACK]), 2)

    def test_executes_atomic_sql_and_validation(self):
        env = {"PGDATABASE": "parking_system", "PGHOST": "localhost", "PGUSER": "dev",
               "ASPNETCORE_ENVIRONMENT": "Development"}
        with patch.dict("os.environ", env, clear=True), \
             patch.object(seed.shutil, "which", return_value="/usr/bin/psql"), \
             patch.object(seed.subprocess, "run", return_value=Mock(returncode=0)) as run:
            self.assertEqual(seed.main(["--apply", "--confirm-disposable", seed.ACK]), 0)
            command = run.call_args.args[0]
            self.assertIn("--single-transaction", command)
            self.assertIn("ON_ERROR_STOP=1", command)
            self.assertTrue(any("Validate-ParkingCatalogSeed.sql" in arg for arg in command))
            self.assertNotIn("parking_dev_local_only", " ".join(command))

    def test_sql_contains_expected_count_and_upsert(self):
        sql = seed.SEED.read_text(encoding="utf-8")
        checks = seed.VALIDATE.read_text(encoding="utf-8")
        self.assertIn("ON CONFLICT (id) DO UPDATE", sql)
        self.assertIn("actual_total <> 4", checks)
        self.assertIn("actual_approved <> 3", checks)
        self.assertIn("actual_pending <> 1", checks)
        for suffix in ("0001", "0002", "0003", "0004"):
            self.assertIn("10000000-0000-0000-0000-00000000" + suffix, sql)
            self.assertIn("10000000-0000-0000-0000-00000000" + suffix, checks)


if __name__ == "__main__":
    unittest.main()
