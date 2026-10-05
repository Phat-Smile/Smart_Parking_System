# Smart Parking System

Full-stack technical foundation for the Smart Parking System: one Next.js web app, a .NET YARP Gateway and five business service skeletons, Python AI, an IoT simulator, PostgreSQL, RabbitMQ, and observability services. Business User Stories are not implemented.

The repository uses Clean Architecture and microservice boundaries and currently
contains the completed technical foundation for future business Vertical Slices.

> Backend foundation status: **COMPLETE / FROZEN**
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

### Backend foundation

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

GitHub Actions runs the .NET, Python AI, and Next.js frontend foundation suites for pushes and pull requests targeting `develop` and feature branches. The workflow is in `.github/workflows/foundation-ci.yml`.

### Frontend foundation

The single Next.js / React / TypeScript application lives in `web/`. It includes a technical App Router shell, Gateway-only API client, TanStack Query provider, strict TypeScript, Tailwind design tokens, Mapbox/Three.js/SignalR technical boundaries, Vitest/Playwright tests, Docker image, and CI. No frontend business feature, Login/Register flow, parking data, or backend business endpoint is implemented. See [Frontend architecture](docs/architecture/Frontend.md) and [Web setup](web/README.md).
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
