# Tests

Each business service has a `UnitTests` and an `IntegrationTests` project under `tests/<Service>/`.
Shared projects sit at the root of `tests/`.

| Project | What it covers |
|---|---|
| `ArchitectureTests` | Dependency rules between layers and services (e.g. `Grpc.*` only in WebAPI hosts) |
| `WebFoundationTests` | HTTP errors and problem details, correlation ids, request logging, security, Gateway |
| `BuildingBlocks/ParkingSystem.Testing` | Shared Testcontainers fixtures (PostgreSQL, RabbitMQ) — not a test project |
| `Identity.*` | Registration, email OTP verify/resend, login and lockout, Outbox/Inbox, messaging registration |
| `Parking.*` | Parking lot catalog and detail, open and cancel parking session (handlers and HTTP) |
| `Booking.*`, `Payment.*`, `Notification.*` | Foundation checks so far (Outbox/Inbox, messaging); business tests come with their stories |

## Running

```powershell
dotnet test ParkingSystem.slnx                                 # everything (integration tests need Docker)
dotnet test tests/Identity/Identity.UnitTests                  # one project, no Docker needed
tests\Identity\Identity.UnitTests\bin\Debug\net10.0\Identity.UnitTests.exe --filter-class "<Namespace.Class>"
```

- Do not pass `-nologo` to `dotnet test`: the repository uses Microsoft.Testing.Platform and the flag makes it report "Zero tests ran".
- Integration tests start disposable PostgreSQL/RabbitMQ containers with dynamic ports; Docker Desktop must be running.
- The running-stack gRPC smoke check is a script, not a test: `scripts/dev/GrpcHealthSmoke.ps1`.
