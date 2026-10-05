# Tests

Each business service has UnitTests and IntegrationTests projects. Current foundation coverage includes architecture boundary rules, PostgreSQL migrations and persistence, Outbox leasing/retry/publishing, Inbox idempotency, RabbitMQ/MassTransit, gRPC health/reflection, HTTP errors/correlation, observability, and JWT/CORS/security behavior. Some service UnitTests still contain assembly-loading smoke checks; Identity also has messaging registration tests. Integration tests use disposable Testcontainers and dynamic ports. No business tests exist yet.
