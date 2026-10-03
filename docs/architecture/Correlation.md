# Correlation

## HTTP requests

`X-Correlation-ID` is the request correlation header at the Gateway, each .NET API, and the AI HTTP service. A single non-empty Guid value up to 128 characters is preserved. Missing, malformed, oversized, empty-Guid, or multi-value input is replaced with a generated Guid; the request is not rejected. Each host returns the accepted/generated value in the response header.

The .NET HTTP middleware stores the existing immutable `ParkingSystem.BuildingBlocks.Correlation.CorrelationContext` in request-scoped `HttpContext.Items` and exposes it through a request-scoped `IHttpContextAccessor` context. It does not use static mutable request state. The Gateway's YARP proxy forwards the canonical header. Direct API calls apply the same policy independently.

ProblemDetails includes both `traceId` and `correlationId`. With Phase 06B OpenTelemetry enabled, `traceId` is the W3C Activity TraceId; it is independent from the stable application-wide correlation Guid. A request log can also include the current `spanId`. When no Activity exists (including filtered health traces), trace fields are empty rather than copied or derived from the correlation value. Do not generate a new correlation ID inside application code or an HTTP endpoint.

## Cross-service propagation

For future HTTP-to-gRPC calls, send the current value as lowercase gRPC metadata `x-correlation-id`; do not add business clients or RPCs as part of this foundation. The current gRPC health services are unchanged.

The Phase 05 outbox publisher maps an integration event's `CorrelationId` to MassTransit's `CorrelationId` and `CausationId` to MassTransit's `InitiatorId`. On inbound messages, each existing Inbox consumer filter now passes MassTransit's `CorrelationId` and `InitiatorId` to the transport-neutral `InboxMessageDescriptor`, alongside the message ID and consumer key. The descriptor fields are in-memory metadata only; no persistence columns or migrations were added.

OpenTelemetry uses W3C `traceparent`/`tracestate` for trace identity and parentage. `TraceId` identifies the distributed trace and `SpanId` identifies an operation; neither replaces `X-Correlation-ID`. The Gateway/YARP HTTP client instrumentation propagates W3C context automatically while the existing YARP correlation forwarding remains unchanged. See [Observability](Observability.md) for exporters, privacy, and local backend configuration.

An HTTP flow's correlation ID should continue through application operations, outbox messages, RabbitMQ consumers, and any new outbox messages. Causation identifies the immediately preceding message or operation; it is not a replacement for correlation and must not be filled with a random value by HTTP middleware. The current Outbox does not persist W3C trace context, so correlation continuity across its delayed dispatch is supported but end-to-end trace-parent continuity is not claimed.
