# Parking WebAPI

ASP.NET Core composition root with OpenAPI/Swagger in Development and health endpoints.

Feature routes:

- `GET /api/parking/lots`: public Parking Catalog endpoint; returns approved parking lots only.
- `GET /api/parking/lots/{parkingLotId}`: public Parking Detail endpoint; returns an approved parking lot or `404`.

Foundation routes:

- `/health/live`: process can serve an HTTP request.
- `/health/ready`: checks the Parking PostgreSQL DbContext, MassTransit bus, and authenticated RabbitMQ AMQP connection. It returns HTTP 503 when PostgreSQL or RabbitMQ is unavailable; telemetry backends and other services are not readiness dependencies.

