"""Check for EF Core migration drift across all microservices."""

import os
import subprocess
import sys

SERVICES = [
    (
        "Identity",
        "src/Services/Identity/Identity.Persistence",
        "src/Services/Identity/Identity.WebAPI",
    ),
    (
        "Parking",
        "src/Services/Parking/Parking.Persistence",
        "src/Services/Parking/Parking.WebAPI",
    ),
    (
        "Booking",
        "src/Services/Booking/Booking.Persistence",
        "src/Services/Booking/Booking.WebAPI",
    ),
    (
        "Payment",
        "src/Services/Payment/Payment.Persistence",
        "src/Services/Payment/Payment.WebAPI",
    ),
    (
        "Notification",
        "src/Services/Notification/Notification.Persistence",
        "src/Services/Notification/Notification.WebAPI",
    ),
]


def check_service(name: str, project: str, startup_project: str) -> tuple[bool, str]:
    cmd = [
        "dotnet",
        "ef",
        "migrations",
        "has-pending-model-changes",
        "--project",
        project,
        "--startup-project",
        startup_project,
        "--no-build",
    ]
    env = dict(os.environ)
    if "ConnectionStrings__DefaultConnection" not in env:
        env["ConnectionStrings__DefaultConnection"] = "Host=localhost;Database=ci_dummy"

    try:
        result = subprocess.run(
            cmd,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            env=env,
        )
        if result.returncode == 0:
            return True, f"[{name}] OK - No pending model changes."
        output = (result.stdout + "\n" + result.stderr).strip()
        return False, f"[{name}] FAILED - Exit code {result.returncode}:\n{output}"
    except Exception as exc:
        return False, f"[{name}] EXCEPTION: {exc}"


def main() -> int:
    print("=== EF Core Migration Drift Check ===")
    has_drift = False

    for name, project, startup_project in SERVICES:
        ok, msg = check_service(name, project, startup_project)
        print(msg)
        if not ok:
            has_drift = True

    if has_drift:
        print("\nERROR: EF Core model drift detected! Create a migration for model changes before merging.", file=sys.stderr)
        return 1

    print("\nSUCCESS: All services have up-to-date EF Core migrations.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
