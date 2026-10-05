# Observability

## Local signal flow

```text
Gateway, APIs, AI service
  ├── OTLP traces ──> OpenTelemetry Collector ──> Jaeger
  └── OTLP metrics ─> OpenTelemetry Collector ──> Prometheus scrape endpoint
```

The local stack uses the official OpenTelemetry Collector Contrib distribution, Jaeger all-in-one, and Prometheus. Jaeger and Prometheus are local development backends; Jaeger uses in-memory storage and loses traces when its container restarts. Applications send traces to the Collector, never directly to Jaeger. The Collector has separate traces and metrics pipelines and no logs pipeline. Structured JSON console output remains the application log destination; OTLP log export is not configured.

## Service resources

Each process has a unique `service.name`. The .NET hosts read `OTEL_SERVICE_NAME` and fall back to the canonical name in the table. Their `service.version` comes from the entry assembly version and `deployment.environment` from the ASP.NET Core environment. AI reads `OTEL_SERVICE_NAME` and uses package version `0.1.0`; its deployment environment comes from `APP_ENVIRONMENT`.

| Process | `service.name` | `service.namespace` |
| --- | --- | --- |
| Gateway | `parking-gateway` | `ParkingSystem` |
| Identity API | `identity-api` | `ParkingSystem` |
| Parking API | `parking-api` | `ParkingSystem` |
| Booking API | `booking-api` | `ParkingSystem` |
| Payment API | `payment-api` | `ParkingSystem` |
| Notification API | `notification-api` | `ParkingSystem` |
| AI service | `ai-service` | `ParkingSystem` |

Compose sets `deployment.environment=Development`. Resource attributes are technical configuration only; do not add account, vehicle, session, booking, payment, authentication, or other user data.

## Trace context and correlation

HTTP and gRPC tracing uses the W3C trace context format (`traceparent` and `tracestate`). ASP.NET Core, `HttpClient`, YARP, FastAPI, and the AI gRPC server use OpenTelemetry instrumentation and standard propagation. No custom `X-Trace-ID` or `X-Span-ID` headers are introduced.

`X-Correlation-ID` remains the existing application-flow Guid and is independent from OpenTelemetry's 32-hex-character TraceId and 16-hex-character SpanId. The .NET request event includes `CorrelationId`, `TraceId`, and `SpanId` when an Activity exists. JSON console scopes are disabled because ASP.NET Core adds the raw request path to an ambient scope; request event fields provide the identifiers without exposing that path. Python request/error JSON logs expose `correlation_id`, `trace_id`, and `span_id`. Trace fields are empty when an operation is not represented by an Activity; they are not synthesized from the correlation value.

ASP.NET request traces retain safe method, route template, status, server, and duration information. Raw URL path/query attributes are cleared from .NET HTTP spans; the Python FastAPI server hook also clears raw path/query attributes. Request/response bodies and Authorization headers are not captured. HttpClient spans clear URL path/query fields. HTTP `/health/live` and `/health/ready`, and .NET and AI gRPC health/reflection requests, are excluded from traces. HTTP health status/duration metrics and JSON request logs remain enabled. FastAPI health exclusions match the full request URL.

The focused YARP test uses a test-only downstream route. It verifies a caller trace context flows through a Gateway server span, a YARP client span, and a downstream server span with one TraceId; it also verifies that the original `X-Correlation-ID` survives and differs from the TraceId. No production business endpoint was added for this test.

## Instrumentation and metrics

The .NET host-only `ParkingSystem.AspNetCore` foundation instruments ASP.NET Core HTTP requests, `HttpClient`, and .NET runtime metrics. The `MassTransit` ActivitySource is subscribed to once through its built-in diagnostics support; no extra instrumentation package or manually duplicated send/receive spans are added. With no business events or consumers yet, no business messaging spans are expected. ASP.NET Core gRPC server tracing uses the package's experimental gRPC instrumentation flag in the local Compose environment; standard gRPC health and development reflection remain functional.

The AI service instruments FastAPI HTTP requests, its existing `grpc.aio` server, and process/system metrics. FastAPI's native automatic telemetry configuration is disabled; the application-owned OpenTelemetry SDK setup is the sole provider/exporter registration and uses OTLP/gRPC. The FastAPI and gRPC instrumentation packages are pinned to the matching upstream `0.66b0` contrib release line; those instrumentation distributions are published as beta packages. Its HTTP request-duration/count metrics and process/runtime measurements use the same OTLP Collector route.

The Collector exposes its Prometheus exporter only on the Compose network at `otel-collector:8889`. Prometheus scrapes that endpoint every 10 seconds. HTTP dimensions use instrumentation-provided route/target and status/method values. The metrics pipeline removes `service.instance.id` before Prometheus export to avoid a new series per process instance; trace resources retain it. Do not add TraceId, CorrelationId, user/account identifiers, license plates, sessions, booking/payment IDs, or raw arbitrary URLs as metric labels.

Detailed EF Core/SQL instrumentation is intentionally omitted. This avoids exporting SQL text or parameters and leaves database spans for a separately reviewed configuration. No telemetry is stored in the application PostgreSQL database.

## Configuration and sampling

Compose applications use standard variables including:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
OTEL_EXPORTER_OTLP_PROTOCOL=grpc
OTEL_TRACES_SAMPLER=parentbased_traceidratio
OTEL_TRACES_SAMPLER_ARG=1.0
```

The endpoint uses Docker's internal service name. Processes started directly on the host should use `http://localhost:4317` (gRPC) or `http://localhost:4318` with an HTTP/protobuf exporter. The current .NET and AI exporters are configured for gRPC; use gRPC for host-launched processes unless the exporter configuration is changed.

The default local trace ratio is 100% for predictable smoke tests. Change the sampler and ratio using environment configuration for production; select the production policy based on expected volume and retention. `.env.example` contains only local non-secret values and configurable host ports.

## Availability and failure behavior

The Collector, Jaeger, and Prometheus are not business dependencies. They are absent from API `depends_on` and readiness checks. .NET tracing/metrics exporters are registered only when an OTLP endpoint is configured; AI telemetry exporters follow the same rule. Export is performed in the background, and a Collector outage does not gate HTTP/gRPC request handling or application readiness. After the Collector returns, SDK exporters retry later batches without requiring API restarts.

Stopping Jaeger or Prometheus can make the Collector's corresponding exporter/scrape target unavailable, but it does not change application readiness. The local stack has no OTLP log exporter.

## MassTransit and durable Outbox boundary

MassTransit v8 provides its own ActivitySource and propagates W3C trace context through its message headers when publishing and consuming; this foundation subscribes to those activities without adding duplicate spans. Future messaging flow is expected to be:

```text
HTTP Activity -> Outbox operation -> MassTransit publish span -> RabbitMQ -> future consumer span
```

The current durable Outbox persists `CorrelationId` and `CausationId`, but not W3C trace context. The dispatcher may run after the original HTTP Activity has ended. Correlation continuity exists; full original trace-parent continuity across delayed durable dispatch is **not claimed**. No columns or schema changes were made, and the dispatcher must not invent a parent span from a CorrelationId.

## Scope boundary

This phase adds tracing and metrics only. It adds no migration, application database schema/table/column, business entity/use case, business consumer/event, custom health endpoint, Grafana, authentication/authorization, SignalR, or business metric.
