#!/usr/bin/env bash
# BE-PK01 forensic compare only. Does not change Git, source, databases or branches.
set -Eeuo pipefail
BASE='88bc3069'
BRANCH='refs/heads/fix/BE2-02-gate-arrival-time'
MAIN_REPO="${BE2_MAIN_REPO:-/d/OJT26/Smart_Parking_System}"
if ! git -C "$MAIN_REPO" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo '[GATE_AUDIT] NOT_VERIFIED: main repo not found; no changes made.'
  exit 0
fi
if ! git -C "$MAIN_REPO" rev-parse --verify --quiet "${BASE}^{commit}" >/dev/null; then
  echo '[GATE_AUDIT] NOT_VERIFIED: original develop commit not available.'
  exit 0
fi
if ! git -C "$MAIN_REPO" rev-parse --verify --quiet "${BRANCH}^{commit}" >/dev/null; then
  echo '[GATE_AUDIT] NOT_VERIFIED: Gate branch not present locally.'
  exit 0
fi
printf '[GATE_AUDIT] BASE=%s; GATE_HEAD=%s\n' "$BASE" "$(git -C "$MAIN_REPO" rev-parse --short=12 "$BRANCH")"
echo '[GATE_AUDIT] Only filenames and change counts; no contents, secrets or customer data.'
FILES=(
  src/Services/Parking/Parking.Application/ParkingSessions/OpenParkingSession
  src/Services/Parking/Parking.Application/GateReadings
  src/Services/Parking/Parking.WebAPI/Endpoints/GateReadingEndpoints.cs
  tests/Parking/Parking.UnitTests/OpenParkingSessionTests.cs
  tests/Parking/Parking.UnitTests/GateReadingConfirmationTests.cs
)
# Compare commit-to-commit, not the user's uncommitted working tree.
git -C "$MAIN_REPO" diff --name-status "$BASE" "$BRANCH" -- "${FILES[@]}" || true
CHANGED="$(git -C "$MAIN_REPO" diff --name-only "$BASE" "$BRANCH" -- "${FILES[@]}" | wc -l | tr -d '[:space:]')"
if [ "$CHANGED" -gt 0 ]; then
  echo "[GATE_AUDIT] CHANGED_PATHS=$CHANGED; REVIEW_REQUIRED: compare timestamp/check-in semantics before merging."
else
  echo '[GATE_AUDIT] CHANGED_PATHS=0; branch has no committed differences within selected Gate files.'
fi
echo '[GATE_AUDIT] No branch checkout, Git write, DB write or migration performed.'
