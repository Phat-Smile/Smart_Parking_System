# Parking WebAPI

ASP.NET Core composition root with OpenAPI/Swagger in Development and health endpoints.

Feature routes:

- `GET /api/parking/lots`: public Parking Catalog endpoint; returns approved parking lots only.

Foundation routes:

- `/health/live`: process can serve an HTTP request.
- `/health/ready`: checks the Parking PostgreSQL DbContext and returns HTTP 503 when PostgreSQL is unavailable. It does not check RabbitMQ or other services.
