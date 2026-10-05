# Foundation baseline

Freeze date: **2026-10-03 (Asia/Saigon)**. Phase 07 is the final foundation checkpoint. This baseline records the validated implementation; business capabilities below are future requirements, not implemented features.

## Services and solution

- Five .NET business bounded contexts: Identity, Parking, Booking, Payment, Notification. Each has Domain, Application, Persistence, Infrastructure, and WebAPI projects.
- Gateway: external REST boundary using YARP; current catch-all routes and clusters under `/api/identity/`, `/api/parking/`, `/api/booking/`, `/api/payment/`, and `/api/notification/` forward to the five business APIs. No business endpoint implementation exists yet.
- AI: Python 3.12, FastAPI and internal gRPC; no business model or inference workflow.
- AI direct dependencies remain declared in `pyproject.toml`; the validated Python 3.12 runtime/test environment is pinned in `requirements.lock` and installed from that file by the image and CI.
- IoT Simulator: idle scaffold with MQTT environment configuration; `DEVICE_SIMULATION_ENABLED=false`. No publisher, consumer, device simulation, or gRPC client.
- Shared projects: transport-neutral `ParkingSystem.BuildingBlocks` and host-specific `ParkingSystem.AspNetCore`.
- Solution inventory: **41 .NET projects = 28 production + 12 test projects + 1 test-support library** (`ParkingSystem.Testing`). AI and IoT are separate Python components.
- Runtime: .NET 10, PostgreSQL 17, RabbitMQ/MassTransit, MQTT, gRPC, OpenTelemetry Collector, Jaeger and Prometheus. Compose has 13 services.

Do not split User, Vehicle, Wallet, QR, Realtime, Owner, or Staff into additional microservices. Ownership remains defined in [Service boundaries](Service-Boundaries.md).

## Dependency rules

An arrow below means “references”.

| Layer | Allowed project references |
| --- | --- |
| Domain | None in the current foundation |
| Application | Own Domain; generic BuildingBlocks |
| Persistence | Own Domain and Application |
| Infrastructure | Own Application; generic BuildingBlocks |
| WebAPI | Own Application, Persistence, Infrastructure; BuildingBlocks; AspNetCore host foundation |
| Gateway | AspNetCore host foundation |
| AspNetCore | Generic BuildingBlocks |
| Generic BuildingBlocks | No project references or framework dependencies |

Persistence and Infrastructure never reference each other. No cross-service implementation or Domain references are allowed. Generic BuildingBlocks contains only technical results/errors, clock, current-user abstraction, correlation and event metadata. HTTP/security/telemetry adapters belong to AspNetCore and are referenced only by hosts.

MassTransit/RabbitMQ implementations remain in service Infrastructure. EF/Npgsql persistence remains in Persistence. Current gRPC server packages are in WebAPI. Domain, Application and generic BuildingBlocks remain transport-neutral. See [Dependency rules](Dependency-Rules.md); the 14 architecture tests remain mandatory.

## Database ownership

Local database: `parking_system`. Identity, Parking, Booking, Payment and Notification own schemas `identity`, `parking`, `booking`, `payment` and `notification`, respectively. Cross-schema reads, joins and foreign keys are prohibited; cross-service relationships use IDs and contracts.

Each schema currently contains exactly:

- `outbox_messages`
- `inbox_messages`
- `__EFMigrationsHistory`

There are **15 tables total**, no application tables in `public`, and no business tables. Each service's migration history contains, in order, `AddOutboxFoundation`, `AddOutboxDispatchLeasing`, `AddInboxFoundation`. Phase 07 adds no migration. API startup never calls `EnsureCreated`, `Migrate` or `MigrateAsync`; migrations are applied explicitly.

Local password-authenticated connections use `GSS Encryption Mode=Disable`, including host design-time commands. This is a local setting, not a production SSL/GSS policy. See [Data ownership](../data/Data-Ownership.md) and [Database migrations](../development/Database-Migrations.md).

## Ports and communication

| Service | Container HTTP | Default host HTTP | Internal gRPC |
| --- | ---: | ---: | ---: |
| Gateway | 8080 | 5000 | None |
| Identity | 8080 | 5001 | 8081 |
| Parking | 8080 | 5002 | 8081 |
| Booking | 8080 | 5003 | 8081 |
| Payment | 8080 | 5004 | 8081 |
| Notification | 8080 | 5005 | 8081 |
| AI | 8000 | 8000 | 50051 |

Host bindings default to `127.0.0.1`. gRPC is internal to `parking-network`, uses local h2c, and exposes only standard health/reflection services. Health returns `SERVING` independently of PostgreSQL/RabbitMQ readiness. Reflection is Development-only. No business proto/RPC exists.

PostgreSQL uses container 5432 / host 5433. RabbitMQ uses AMQP 5672, Management 15672 and MQTT 1883. Management and MQTT plugins are enabled; MQTT requires existing local credentials and rejects anonymous clients. `smoke/test` is an infrastructure test topic only. No business event mapping, receive endpoint, queue or consumer exists. See [gRPC](Grpc.md) and [Local development](../development/Local-Development.md).

## Durable messaging

**Outbox:** the owning service's business transaction will write business state and Outbox rows atomically. The existing dispatcher claims an ordered batch with `FOR UPDATE SKIP LOCKED`, assigns a finite lease, and commits the claim transaction before publishing through MassTransit. Expired leases permit recovery. Updates check message ID, lease owner and pending state. Success marks processed; failures increment retry count and schedule bounded exponential backoff. Unknown events and malformed payloads remain pending with retry state. Polling remains one second by default.

Delivery is **at-least-once**: duplicate publication is possible after publish succeeds but the processed update does not commit. Exactly-once delivery is not guaranteed.

**Inbox:** composite key `(message_id, consumer_key)` and `INSERT ... ON CONFLICT DO NOTHING`. One scoped service DbContext transaction owns the claim, handler persistence, local Outbox writes, processed update and commit. Failure rolls everything back; a committed duplicate skips the handler. MassTransit `MessageId` is required, never replaced with a random ID. `ConsumerKey` is explicit, stable, required and bounded to 512 characters. External effects need their own idempotency strategy; avoid external calls inside this database transaction.

Custom Outbox/Inbox are the only persistence mechanisms; MassTransit EF Outbox/Inbox is not enabled. Inbox retention/cleanup and automatic domain-event capture are future work. See [Messaging](Messaging.md).

## HTTP, errors and correlation

Gateway is the browser-facing REST boundary. Direct local API/AI host ports are development diagnostics, not a production browser-access design.

Errors use safe `application/problem+json`, including status, title, traceId, correlationId and stable errorCode. Foundation support covers validation 400, unauthenticated 401, unauthorized 403 and unexpected 500. Generic status-only responses also receive a safe body; business-specific 404/409 exception mapping remains future policy. No stack trace, SQL, credential or submitted value is returned. See [Error handling](Error-Handling.md).

`X-Correlation-ID` accepts one non-empty valid Guid, up to 128 characters, or generates a replacement. Hosts return it and Gateway preserves it. Future gRPC clients forward lowercase `x-correlation-id`. Correlation is request-scoped and independent of W3C TraceId. Outbox persists CorrelationId/CausationId; publishing maps them to MassTransit CorrelationId/InitiatorId. See [Correlation](Correlation.md).

## Logging and observability

- Each .NET host has one JSON console provider. AI request/error logs are structured; Uvicorn emits one lifecycle sequence with access logging disabled.
- HTTP request logs use matched route templates or `<unmatched>`; raw route identifiers and query values are omitted.
- Successful EF `Database.Command` messages are suppressed at `Warning` in local containers; warnings and failed commands remain visible. No request-body, token, cookie, credential or PII logging.
- Applications send OTLP/gRPC traces and metrics to Collector `4317`; Collector exports traces to Jaeger and exposes metrics on `8889` for Prometheus. There is **no OTLP application log pipeline**.
- Service names: `parking-gateway`, `identity-api`, `parking-api`, `booking-api`, `payment-api`, `notification-api`, `ai-service`; namespace `ParkingSystem`.
- W3C `traceparent`/`tracestate` propagate through HTTP/YARP and instrumented transports. Successful HTTP health and gRPC health/reflection traffic is filtered from traces; health metrics may remain.
- Metrics cover HTTP and runtime/process measurements. No business metrics or sensitive/high-cardinality identity labels. Detailed SQL tracing is not enabled.
- AI native FastAPI automatic telemetry configuration is disabled; explicit application-owned OTLP/gRPC instrumentation is enabled once.
- **Durable Outbox limitation:** full W3C trace context is not persisted. Original HTTP trace-parent continuity across delayed dispatch is not guaranteed; CorrelationId cannot be used to invent a trace parent.

Jaeger UI defaults to host 16686; Prometheus to 9090; Collector receivers to 4317/4318. Jaeger local storage is in memory. See [Observability](Observability.md).

## Security and availability

Gateway and all five APIs independently validate JWTs: **RS256 only**, issuer `ParkingSystem.Identity`, audience `ParkingSystem`, required signature/expiration, issuer/audience/lifetime validation and 30-second clock skew. No `alg=none`, HS256 fallback, token issuance, refresh flow or business authorization policy. Future protected endpoints must opt into the existing authenticated policy; future public Identity auth endpoints explicitly allow anonymous access.

Validators receive only the public key. `.local/security/jwt/private.pem` remains ignored, untracked, outside build contexts, images and validator mounts. Local development defaults are public local-only credentials; production secrets must never enter tracked files.

Gateway CORS uses configured explicit origins; the current empty list allows no browser origin. Authorization, Content-Type and X-Correlation-ID are permitted request headers; X-Correlation-ID is exposed. Credentials are not enabled. Gateway emits nosniff, no-referrer and DENY headers once. See [Security](Security.md).

HTTP liveness checks only the process. The five APIs' readiness checks their PostgreSQL DbContext, MassTransit bus and authenticated RabbitMQ connection. Gateway/AI retain independent readiness. PostgreSQL or RabbitMQ outage yields API live 200 / ready 503; recovery restores ready 200 without application restart. Collector, Jaeger, Prometheus, another API or a JWT issuer are not readiness dependencies. Their outages must leave live/ready 200. No runtime dependency is added solely for telemetry.

## Validated test and runtime baseline

Phase 07 validation on 2026-10-03:

- .NET tool restore, solution restore/build: PASS; 0 build warnings/errors.
- `.github/workflows/foundation-ci.yml` enforces the .NET suite and Python 3.12 suite using `requirements.lock` on develop and feature branch pushes/pull requests; it does not deploy.
- .NET: **45/45**, including **14/14 architecture**; no skips. Focused Identity integration regression: **4/4**.
- AI: **10 tests plus 3 subtests**, PASS on Python **3.12.15**, including grpc/OpenTelemetry imports.
- PostgreSQL/RabbitMQ integration tests use disposable Testcontainers with dynamic ports and cleanup; they do not use local host 5433 or populate the application database.
- Compose config/build/up: PASS; all 13 services run, PostgreSQL/RabbitMQ/AI healthy. All eight application containers recreated from current images without deleting persistent volumes.
- All seven HTTP hosts live/ready 200; all six gRPC hosts SERVING; authenticated MQTT publish/subscribe passes and anonymous access is rejected.
- Controlled PostgreSQL, RabbitMQ, Collector, Jaeger and Prometheus outages recover with no application restart. Fresh startup logs contain no duplicate lifecycle events or persistent runtime errors.
- Fresh service traces, Gateway-to-Identity trace parentage and correlation, Prometheus target UP, security headers and key isolation verified.
- Final application DB: Inbox/Outbox rows 0, public tables 0. Broker: queues/consumers/messages 0. No tracked secrets or generated runtime artifacts.

## Future vertical slices

Feature code belongs to its owning bounded context:

| Layer | Future additions |
| --- | --- |
| Domain | Entities, value objects, domain rules |
| Application | Commands, queries, use cases, validation |
| Persistence | Repositories, EF configurations, explicit migrations |
| Infrastructure | External integrations, message consumers, gRPC adapters as needed |
| WebAPI | Endpoints/controllers and host composition |
| Tests | Unit, integration and architecture coverage |

Use gRPC for synchronous cross-service contracts, RabbitMQ events for asynchronous communication, and REST through Gateway for browsers. SignalR/WebSocket is future realtime work. No cross-service database access or implementation references. Provider-owned versioned contracts belong under `contracts/`; see [gRPC](Grpc.md) and [Messaging](Messaging.md).

The existing architecture test confines gRPC packages to WebAPI. A future slice that needs an Infrastructure gRPC adapter must explicitly justify and review that narrow boundary/test change before adding transport packages; Phase 07 does not relax this rule.

Business source-of-truth priority: **directly confirmed rules > User Stories > Main Flow > Main Flow Overview > Context > technical inference**. Older Context content cannot override confirmed rules.

Frozen future invariants:

- Guest is anonymous, is not a persisted Identity role, receives no synthetic account, and goes directly to the lot. Guest does not use the registered-user Search/AI/Hold/Booking main flow.
- Hold is deposit-based; Scheduled Booking is 100% prepaid; no debt at exit.
- Session QR belongs only to ParkingSession, never User, Vehicle or plate. Each session has its own QR. Exit requires plate match, valid Session QR and completed payment; successful exit closes the session, invalidates QR and releases spot/capacity.

**Not implemented:** Register, Login, email OTP, Google Login, Refresh Token, Profile, Vehicle, parking business, Hold, Booking business, Parking Session/QR workflow, payment business, notification workflow, SignalR and business AI. No placeholder business entities, endpoints, events, consumers or RPCs are introduced by this freeze.

## Change and Git policy

Foundation changes require separate justification, review and regression: layer dependencies, service boundaries, Outbox/Inbox, transport, database ownership, correlation/error contracts, telemetry, JWT validation, ports and Docker topology must not be casually redesigned for a feature.

Future business work uses `features/Implementation_<UserStoryName>` → Merge Request → `develop`; do not implement directly on develop. Work to a logical checkpoint, validate, commit one logical change, then push without force. The annotated `foundation-setup-complete` tag marks the accepted Phase 07 commit; do not move it for later hygiene changes. No release branch is created. Starting a vertical slice requires a separate explicit request.
