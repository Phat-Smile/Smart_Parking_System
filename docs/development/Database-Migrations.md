# EF Core migrations

Each business service owns its EF Core migrations in its own Persistence project. The five contexts use the shared `parking_system` database while keeping tables and EF migration history isolated by schema. Application startup never calls `EnsureCreated`, `Migrate`, or `MigrateAsync`; apply production or local-development migrations explicitly with `dotnet ef database update`.

| Service | Context | Persistence project | Schema | Migrations |
| --- | --- | --- | --- | --- |
| Identity | `IdentityDbContext` | `src/Services/Identity/Identity.Persistence/Identity.Persistence.csproj` | `identity` | `AddOutboxFoundation`, `AddOutboxDispatchLeasing`, `AddInboxFoundation` |
| Parking | `ParkingDbContext` | `src/Services/Parking/Parking.Persistence/Parking.Persistence.csproj` | `parking` | `AddOutboxFoundation`, `AddOutboxDispatchLeasing`, `AddInboxFoundation` |
| Booking | `BookingDbContext` | `src/Services/Booking/Booking.Persistence/Booking.Persistence.csproj` | `booking` | `AddOutboxFoundation`, `AddOutboxDispatchLeasing`, `AddInboxFoundation` |
| Payment | `PaymentDbContext` | `src/Services/Payment/Payment.Persistence/Payment.Persistence.csproj` | `payment` | `AddOutboxFoundation`, `AddOutboxDispatchLeasing`, `AddInboxFoundation` |
| Notification | `NotificationDbContext` | `src/Services/Notification/Notification.Persistence/Notification.Persistence.csproj` | `notification` | `AddOutboxFoundation`, `AddOutboxDispatchLeasing`, `AddInboxFoundation` |

`AddOutboxFoundation` creates only its private technical `outbox_messages` table and pending-message index. `AddOutboxDispatchLeasing` adds the nullable `lock_id`, `locked_until_utc`, and `next_attempt_at_utc` columns to that table. No business table, new schema, or public history table is created. Each migration is recorded in the matching schema's `__EFMigrationsHistory`.

`AddInboxFoundation` creates only the service-owned `inbox_messages` table with composite primary key `(message_id, consumer_key)`. It adds no business table, cross-service foreign key, public table, or MassTransit EF Inbox state. The application handler, Inbox row, and any service-local custom Outbox rows share the same transaction through the service's existing DbContext.

The local `dotnet-ef` tool is pinned to EF Core `10.0.7` in `.config/dotnet-tools.json`. From the repository root, restore it with:

```powershell
dotnet tool restore
```

Each WebAPI startup project references `Microsoft.EntityFrameworkCore.Design` with `PrivateAssets="all"`, as required by EF tooling. The DbContext, Npgsql provider configuration, migration assembly, and migration history configuration remain in the corresponding Persistence project.

Design-time commands run on the host and connect to PostgreSQL at `localhost:5433`. Supply the local password through the process environment; replace the placeholder before running:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5433;Database=parking_system;Username=parking_dev;Password=<your-local-password>;GSS Encryption Mode=Disable'
```

Do not put a real password in this document, source code, or `appsettings.json`. The five API containers receive their runtime connection string from Compose and use `postgres:5432`.

## Identity

```powershell
dotnet ef migrations list `
  --project src/Services/Identity/Identity.Persistence/Identity.Persistence.csproj `
  --startup-project src/Services/Identity/Identity.WebAPI/Identity.WebAPI.csproj `
  --context IdentityDbContext

dotnet ef database update `
  --project src/Services/Identity/Identity.Persistence/Identity.Persistence.csproj `
  --startup-project src/Services/Identity/Identity.WebAPI/Identity.WebAPI.csproj `
  --context IdentityDbContext
```

## Parking

```powershell
dotnet ef migrations list `
  --project src/Services/Parking/Parking.Persistence/Parking.Persistence.csproj `
  --startup-project src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj `
  --context ParkingDbContext

dotnet ef database update `
  --project src/Services/Parking/Parking.Persistence/Parking.Persistence.csproj `
  --startup-project src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj `
  --context ParkingDbContext
```

## Booking

```powershell
dotnet ef migrations list `
  --project src/Services/Booking/Booking.Persistence/Booking.Persistence.csproj `
  --startup-project src/Services/Booking/Booking.WebAPI/Booking.WebAPI.csproj `
  --context BookingDbContext

dotnet ef database update `
  --project src/Services/Booking/Booking.Persistence/Booking.Persistence.csproj `
  --startup-project src/Services/Booking/Booking.WebAPI/Booking.WebAPI.csproj `
  --context BookingDbContext
```

## Payment

```powershell
dotnet ef migrations list `
  --project src/Services/Payment/Payment.Persistence/Payment.Persistence.csproj `
  --startup-project src/Services/Payment/Payment.WebAPI/Payment.WebAPI.csproj `
  --context PaymentDbContext

dotnet ef database update `
  --project src/Services/Payment/Payment.Persistence/Payment.Persistence.csproj `
  --startup-project src/Services/Payment/Payment.WebAPI/Payment.WebAPI.csproj `
  --context PaymentDbContext
```

## Notification

```powershell
dotnet ef migrations list `
  --project src/Services/Notification/Notification.Persistence/Notification.Persistence.csproj `
  --startup-project src/Services/Notification/Notification.WebAPI/Notification.WebAPI.csproj `
  --context NotificationDbContext

dotnet ef database update `
  --project src/Services/Notification/Notification.Persistence/Notification.Persistence.csproj `
  --startup-project src/Services/Notification/Notification.WebAPI/Notification.WebAPI.csproj `
  --context NotificationDbContext
```

All five services retain the same pending-message index definition: `ix_outbox_messages_pending` on `occurred_at_utc` with filter `processed_at_utc IS NULL`. It supports FIFO ordering over pending rows; the claim query evaluates lease and retry eligibility. No additional speculative index was added. Integration tests apply migrations only against disposable PostgreSQL Testcontainers instances; API startup does not run migrations.
