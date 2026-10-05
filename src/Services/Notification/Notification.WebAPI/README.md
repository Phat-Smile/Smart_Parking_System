# Notification WebAPI

ASP.NET Core composition root with OpenAPI/Swagger in Development and health endpoints. Feature routes are intentionally absent.

- /health/live: process can serve an HTTP request.
- /health/ready: checks the Notification PostgreSQL DbContext, MassTransit bus, and authenticated RabbitMQ AMQP connection. It returns HTTP 503 when PostgreSQL or RabbitMQ is unavailable; telemetry backends and other services are not readiness dependencies.
