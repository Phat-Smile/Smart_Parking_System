# TECH-02 — Shared Parking seed: implementation & acceptance log

- Work item: TECH-02 (S1, BE-2/Khoa)
- Source snapshot: `Smart_Parking_System.zip` supplied in this chat, not live Git repository
- Status: **PARTIAL — not yet accepted**
- No modifications to existing `scripts/database/Seed-ParkingCatalog.sql`: its four fixture UUIDs are preserved.

## Scope actually implemented

1. A guarded `run_parking_seed.py` (dry-run by default; needs explicit Development/Testing environment and disposable database acknowledgement).
2. SQL assertions in `Validate-ParkingCatalogSeed.sql`: four known IDs, three Approved, one Pending, expected names, types, geocoordinates.
3. Seven Python unit tests in `tests/database/test_seed_runner.py` (guards, dry-run, transaction command, SQL contract).
4. `ParkingSeedIntegrationTests.cs`: PostgreSQL 17 Testcontainers + EF migrations, apply same seed twice, check public readers exclude Pending.
5. Updated `Parking.IntegrationTests.csproj` to copy the two SQL files into test output.

## Tested as part of preparation

| Check | Evidence/status |
| --- | --- |
| `python -m unittest discover -s tests/database -v` on source snapshot with original catalog SQL present | **7/7 PASS** (2026-10-12, preparation environment) |
| `python scripts/database/run_parking_seed.py` | Requires running locally; dry-run only |
| Testcontainers PostgreSQL / EF migrations / SQL assertions | **NOT RUN** in preparation environment (`dotnet`/Docker unavailable) |
| Idempotency against a real database | **NOT VERIFIED** until integration test passes |
| Seed Zones/Spots/Pricing | **BLOCKED** on ownership and migrations from BE-4 / BE-5 |

## User-side acceptance checklist

- [ ] Branch was based on updated `develop` and includes only TECH-02 files.
- [ ] Confirm the existing SQL fixture `scripts/database/Seed-ParkingCatalog.sql` matches expected IDs.
- [ ] Python test command reports seven tests and OK.
- [ ] Run `python scripts/database/run_parking_seed.py` to verify safe dry-run.
- [ ] Docker and .NET SDK available; PostgreSQL migration applied to an **isolated/disposable** local database.
- [ ] Run `dotnet test tests/Parking/Parking.IntegrationTests/Parking.IntegrationTests.csproj` (including new seed integration test) and archive passing output.
- [ ] Run seed twice on disposable database; verify 4 expected fixture IDs / 3 Approved / 1 Pending; no duplicate records.
- [ ] Confirm with BE-4/BE-5 which schema for zones/spots/prices has been merged and who will seed each table.
- [ ] Reviewer approves; only then mark TECH-02 complete in master backlog.

## Do not do

- Do not seed shared, staging, or production databases without explicit owner authorization.
- Do not overwrite or fabricate tables owned by other teams to make tests green.
- Do not commit credentials, `.env` or production identifiers.
- Do not claim TECH-02 complete solely from the seven Python unit tests.
