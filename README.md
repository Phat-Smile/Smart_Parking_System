# Smart Parking System Backend

Backend foundation for the Smart Parking System.

The repository uses Clean Architecture and microservice boundaries and currently
contains the completed technical foundation for future business Vertical Slices.

> Foundation status: **COMPLETE / FROZEN**
>
> Phases 00–07 have been completed.
>
> Business User Stories are not implemented yet.

See:

- [Foundation baseline](docs/architecture/Foundation-Baseline.md)
- `docs/architecture/`
- `docs/data/`
- `docs/development/`

---

## Project Status

### Foundation

**COMPLETE**

The current foundation includes:

- Clean Architecture boundaries
- Microservice solution structure
- Docker local infrastructure
- PostgreSQL persistence foundation
- EF Core migrations
- Transactional Outbox
- Inbox / consumer idempotency
- RabbitMQ + MassTransit
- MQTT support for IoT Simulator
- gRPC health and Development reflection
- ProblemDetails error handling
- Correlation ID propagation
- Structured logging
- OpenTelemetry tracing and metrics
- OpenTelemetry Collector
- Jaeger
- Prometheus
- JWT RS256 validation foundation
- Authorization foundation
- CORS
- Security headers
- HTTP liveness and dependency-aware readiness; gRPC health checks
- Architecture and integration tests

### Business implementation

**NOT STARTED**

The next development stage is:

`Vertical Slice business implementation`

Business features must be implemented from feature branches:

```text
features/Implementation_<UserStoryName>
        ↓
Merge Request
        ↓
develop
```
