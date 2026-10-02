# Notification WebAPI

ASP.NET Core composition root with OpenAPI/Swagger in Development and health endpoints. Feature routes are intentionally absent.

- /health/live: process can serve an HTTP request.
- /health/ready: checks the Notification PostgreSQL DbContext and returns HTTP 503 when PostgreSQL is unavailable. It does not check RabbitMQ or other services.
