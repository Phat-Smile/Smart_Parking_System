# Integration events

Every event that crosses a service boundary (Outbox → RabbitMQ → Inbox) is declared in **one place**:
the `ParkingSystem.Contracts.Events` project in this folder. Service projects never hold cross-service event types,
and `src/BuildingBlocks` keeps only technical primitives (`IIntegrationEvent`, envelopes).

```
contracts/events/ParkingSystem.Contracts.Events/
  Identity/       UserVerificationOtpIssuedIntegrationEvent, AccountEmailVerifiedIntegrationEvent
  Parking/        (owner: Parking)
  Booking/        (owner: Booking)
  Payment/        (owner: Payment)
  Notification/   (owner: Notification)
```

## Rules

| Rule | Detail |
|---|---|
| Folder = owning service | The service that **publishes** the event owns its folder; only that service changes it. |
| Namespace | `ParkingSystem.Contracts.Events.<OwningService>` |
| Type name | Past-tense fact + `IntegrationEvent`: `ParkingSessionOpenedIntegrationEvent`, not `OpenSessionCommand`. |
| Shape | `public sealed record` with primitive / `Guid` / `DateTimeOffset` members only — no domain types, no behaviour. The project has no references (enforced by `ArchitectureTests`). |
| Wire name and version | Registered in the publisher's Infrastructure: `services.AddIntegrationEvent<T>("<service>.<kebab-fact>.v<n>", n)`. The Outbox stores this name, not the CLR name. |
| Breaking change | Add a new record `...V2` with a new wire name (`.v2`); keep the old one until every consumer moved. Never rename or remove members of a published record. |
| Who references the project | `<Service>.Application` (to enqueue) and `<Service>.Infrastructure` (to register publishers / consumers). Domain, Persistence and WebAPI do not. |
| Docker | A service that references the project copies `contracts/events/` in its Dockerfile (see Identity and Notification). |
| Sensitive data | Keep secrets out of events where possible; an event is stored in the Outbox table and travels through RabbitMQ. |

## Current events

| Wire name | Publisher | Consumer |
|---|---|---|
| `identity.user-verification-otp-issued.v1` | Identity (register, resend OTP) | Notification — sends the OTP email |
| `identity.account-email-verified.v1` | Identity (verify email) | none yet |
| `booking.reservation-confirmed.v1` | Booking (confirm reservation) | Notification — sends booking confirmation |
