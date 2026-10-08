# Solution filters

Open one of these instead of `ParkingSystem.slnx` when you work on a single service.
Visual Studio and C# Dev Kit then load about 13 of the 41 projects, which keeps
IntelliSense, build and RAM usage down.

| File | Loads |
|---|---|
| `Identity.slnf` | Identity (5) + Identity tests (2) + BuildingBlocks + Gateway + shared tests |
| `Parking.slnf` | Parking (5) + Parking tests (2) + BuildingBlocks + Gateway + shared tests |
| `Booking.slnf` | Booking (5) + Booking tests (2) + BuildingBlocks + Gateway + shared tests |
| `Payment.slnf` | Payment (5) + Payment tests (2) + BuildingBlocks + Gateway + shared tests |
| `Notification.slnf` | Notification (5) + Notification tests (2) + BuildingBlocks + Gateway + shared tests |

- Visual Studio: File > Open > Project/Solution > pick the `.slnf`.
- VS Code (C# Dev Kit): set `"dotnet.defaultSolution": "solution-filters/Identity.slnf"` in your local `.vscode/settings.json`.
- CLI: `dotnet build solution-filters/Identity.slnf`.

Multi-project start profiles live in `ParkingSystem.slnLaunch` (Visual Studio 2022 17.11+:
the startup dropdown next to the Start button). Each profile debugs only the service you
work on and starts the others without the debugger.
