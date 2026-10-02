# Booking WebAPI

ASP.NET Core composition root with OpenAPI/Swagger in Development and health endpoints. Feature routes are intentionally absent.

- /health/live: process can serve an HTTP request.
- /health/ready: checks the Booking PostgreSQL DbContext, MassTransit bus, and authenticated RabbitMQ AMQP connection; returns HTTP 503 when PostgreSQL or RabbitMQ is unavailable.
