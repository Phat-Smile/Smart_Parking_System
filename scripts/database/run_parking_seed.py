#!/usr/bin/env python3
"""Safeguarded idempotent TECH-02 catalog seed for disposable databases only.

Requires a local `psql` client for --apply. Pass PostgreSQL connection details
through libpq PG* environment variables, never command-line credential flags.
Does NOT provision BE-4 zones/spots or BE-5 pricing schema.
"""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parent
SEED = ROOT / "Seed-ParkingCatalog.sql"
VALIDATE = ROOT / "Validate-ParkingCatalogSeed.sql"
ACK = "I_UNDERSTAND_THIS_MUTATES_DATA"


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true", help="Execute on a disposable database (otherwise dry run)")
    parser.add_argument("--confirm-disposable", default="", metavar="ACKNOWLEDGEMENT")
    args = parser.parse_args(argv)
    if not args.apply:
        print("DRY RUN: psql would run Seed-ParkingCatalog.sql then Validate-ParkingCatalogSeed.sql")
        print("Use --apply --confirm-disposable " + ACK + " with a disposable PGDATABASE")
        return 0

    environment = os.environ.get("ASPNETCORE_ENVIRONMENT") or os.environ.get("DOTNET_ENVIRONMENT")
    dbname = os.environ.get("PGDATABASE", "").strip()
    if environment not in ("Development", "Testing"):
        print("BLOCKED: ASPNETCORE_ENVIRONMENT or DOTNET_ENVIRONMENT must be Development or Testing")
        return 2
    if args.confirm_disposable != ACK:
        print("BLOCKED: exact disposable-database acknowledgement is required")
        return 2
    if not dbname or dbname.lower() in ("postgres", "template0", "template1"):
        print("BLOCKED: explicitly set PGDATABASE to a disposable project database")
        return 2
    if not os.environ.get("PGHOST") or not os.environ.get("PGUSER"):
        print("BLOCKED: set PGHOST and PGUSER via libpq environment variables")
        return 2
    executable = shutil.which("psql")
    if executable is None:
        print("BLOCKED: psql not installed; see README-Seed-Shared.md Docker alternative")
        return 2

    # -X disables untrusted personal .psqlrc; -1 guarantees seed+assertions commit together.
    # No credentials are added to argv; PostgreSQL libpq environment variables are inherited.
    command = [executable, "-X", "-v", "ON_ERROR_STOP=1", "--single-transaction",
               "-f", str(SEED), "-f", str(VALIDATE)]
    print("Running TECH-02 on explicitly confirmed disposable database; details redacted.", flush=True)
    return subprocess.run(command, check=False).returncode


if __name__ == "__main__":
    raise SystemExit(main())
