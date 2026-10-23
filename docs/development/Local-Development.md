# Local development

## Clean source archive

From the repository root, create a source-only zip from the current commit:

```sh
git archive --format=zip --output=OJT2026_Individual-clean.zip HEAD
```

Keep the archive outside the repository or remove it after sharing. Manually zipping the working directory can include `.git`, ignored `.env` and `.local/` files, private keys, `bin/`, `obj/`, `TestResults/`, and `__pycache__/`. Never add private keys to tracked source.

## Prerequisites

- Docker Desktop or Docker Engine with Docker Compose v2
- .NET SDK 10.0.302 for running or validating .NET projects on the host
- Python 3.12 only when running the AI service directly on the host
- Node 24.16.0 and npm 11.13.0 for running the Next.js web app on the host

The Compose stack builds the AI container with Python 3.12. It does not change the Python version installed on the host.

The .NET Docker build stages use SDK `10.0.301` and target `net10.0`; host builds continue to use the SDK pinned by `global.json` (`10.0.302`). SDK `10.0.301` is used as a Docker build workaround while an [upstream Windows/Linux-container restore issue](https://github.com/dotnet/sdk/issues/55340) for `10.0.302` remains open.

## Environment setup

From the repository root, create `.env` from the example if it does not already exist. On PowerShell:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
```

The example PostgreSQL and RabbitMQ credentials are public local-development values. Do not reuse them outside this local stack. `.env` is ignored by Git.

The PostgreSQL port inside Docker is `5432`; the host port is `5433` by default. The five API containers receive `ConnectionStrings__DefaultConnection` from Compose, built from `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD`. Their connection uses `postgres:5432` on the Compose network. No database password is stored in the API `appsettings.json` files.

Set `SECURITY_OTP_PEPPER` in `.env` to your own random value (at least 32 characters) before starting Identity; it refuses to start without it. On PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

APIs started directly on the host (IDE or `dotnet run`) in the `Development` environment read the repository `.env` themselves: the connection string (`localhost:POSTGRES_HOST_PORT`), the `RabbitMq:*` settings (`localhost:RABBITMQ_HOST_PORT`), `Security:Otp:Pepper` from `SECURITY_OTP_PEPPER`, and the other keys such as `SMTP_HOST` or `GOOGLE_CLIENT_ID`. No credential lives in the `appsettings*.json` files, so do not add one there.

A value that is already set wins over `.env`, for example a process or user environment variable `ConnectionStrings__DefaultConnection`. Remove such a variable if it holds old values. Containers never read `.env` (it is excluded by `.dockerignore`); Compose passes the same values as environment variables.

## Start infrastructure

Start just PostgreSQL and RabbitMQ:

```sh
docker compose up -d postgres rabbitmq
docker compose ps
```

PostgreSQL becomes healthy when `pg_isready` accepts a connection. RabbitMQ becomes healthy when its node reports that it is running. RabbitMQ enables the Management and MQTT plugins from `deploy/rabbitmq/enabled_plugins` and configures the MQTT listener in `deploy/rabbitmq/rabbitmq.conf`.

## MQTT smoke test

Run the subscriber first in one PowerShell window, then run the publisher in another. The subscriber exits after receiving one message.

```powershell
docker run --rm --network parking-network eclipse-mosquitto:2 `
  mosquitto_sub `
  -h rabbitmq `
  -p 1883 `
  -u parking_dev `
  -P parking_dev_local_only `
  -t "smoke/test" `
  -C 1
```

```powershell
docker run --rm --network parking-network eclipse-mosquitto:2 `
  mosquitto_pub `
  -h rabbitmq `
  -p 1883 `
  -u parking_dev `
  -P parking_dev_local_only `
  -t "smoke/test" `
  -m "hello-smart-parking"
```

## Start the full skeleton stack

```sh
docker compose up --build
```

This builds and starts the Gateway, five .NET APIs, AI HTTP service, IoT simulator, PostgreSQL, RabbitMQ, OpenTelemetry Collector, Jaeger, and Prometheus. The five .NET APIs receive `RabbitMq__Host`, `RabbitMq__Port`, `RabbitMq__Username`, `RabbitMq__Password`, and `RabbitMq__VirtualHost` from the existing `RABBITMQ_*` variables and connect over authenticated AMQP on port `5672`. The IoT simulator receives `MQTT_HOST`, `MQTT_PORT`, `MQTT_USERNAME`, and `MQTT_PASSWORD` from the same local RabbitMQ settings and uses MQTT port `1883`; it does not connect to MQTT or generate traffic in this scaffold.

The five business APIs require valid PostgreSQL and RabbitMQ configuration to start. They do not open PostgreSQL during startup and do not apply migrations automatically. MassTransit starts one RabbitMQ AMQP bus per API and uses its built-in reconnect behavior. A temporarily unavailable broker does not prevent the API process from starting.

## Local observability

The applications export traces and metrics over OTLP/gRPC to `http://otel-collector:4317` inside Compose. The Collector accepts OTLP/gRPC on `4317` and OTLP/HTTP on `4318`, exports traces to Jaeger, and exposes translated metrics for Prometheus to scrape at `otel-collector:8889`. Applications do not send telemetry directly to Jaeger or Prometheus. There is no OTLP log pipeline; structured JSON console logs remain the application log output.

Open these local interfaces:

| Component | Default host address | Use |
| --- | --- | --- |
| Jaeger | `http://localhost:16686` | Search traces and inspect service names/spans |
| Prometheus | `http://localhost:9090` | Query Collector-exported application metrics |
| OTLP/gRPC receiver | `localhost:4317` | SDKs on the host |
| OTLP/HTTP receiver | `localhost:4318` | Host SDKs configured for OTLP HTTP/protobuf |

Change `JAEGER_UI_HOST_PORT`, `PROMETHEUS_HOST_PORT`, `OTEL_GRPC_HOST_PORT`, or `OTEL_HTTP_HOST_PORT` in `.env` if a host port is occupied. The internal Compose receiver ports stay `4317` and `4318`.

The .NET processes use these canonical `service.name` values: `parking-gateway`, `identity-api`, `parking-api`, `booking-api`, `payment-api`, and `notification-api`. AI uses `ai-service`. The resource namespace is `ParkingSystem`; Compose sets the environment to `Development`. Local sampling defaults to 100%. Production should use an environment-appropriate sampling policy and ratio.

Generate safe request traces without adding a business endpoint. A `404` from an unconfigured technical path is expected and is recorded as a diagnostic request span; health requests are filtered from traces:

```powershell
curl.exe -sS -o NUL -w "%{http_code}`n" http://localhost:5000/telemetry-check
curl.exe -sS -o NUL -w "%{http_code}`n" http://localhost:5001/telemetry-check
curl.exe -sS -o NUL -w "%{http_code}`n" http://localhost:8000/telemetry-check
```

Open Jaeger, select a service (for example `parking-gateway`), and find the request by its recent time. Direct API requests make their own service appear after a traced request. Prometheus scrapes the Collector every 10 seconds; search metric names for `http_server_request_duration` and runtime/process metric families after generating requests.

To check non-critical Collector outage behavior, stop only the Collector, check application endpoints and dependency status, then restart it. Do not restart applications as part of this check:

```powershell
docker compose stop otel-collector
curl.exe -i http://localhost:5000/health/live
curl.exe -i http://localhost:5000/health/ready
curl.exe -i http://localhost:5001/health/live
curl.exe -i http://localhost:5001/health/ready
curl.exe -i http://localhost:8000/health/live
curl.exe -i http://localhost:8000/health/ready
docker compose ps
docker compose start otel-collector
```

The five APIs' readiness still depends only on PostgreSQL and RabbitMQ. Gateway and AI keep their existing readiness semantics. Once Collector returns, OTLP exporters continue with later batches; no API restart is needed. Jaeger and Prometheus also remain outside business readiness checks. See [Observability](../architecture/Observability.md) for trace filtering, data privacy, resource attributes, and the durable Outbox trace-continuity boundary.

## Internal gRPC listeners

The five .NET APIs listen for REST on container port `8080` (HTTP/1.1) and internal gRPC on `8081` (HTTP/2 cleartext). The AI service runs FastAPI on `8000` and gRPC on `50051` concurrently. gRPC ports are exposed only to containers on `parking-network`; they are not published to the host. The Gateway remains REST-only.

All six gRPC hosts expose standard `grpc.health.v1.Health`; its empty service name reports `SERVING` while the gRPC listener is running. This technical status is independent of PostgreSQL/RabbitMQ readiness. Development reflection is enabled only in Development and currently lists only standard health/reflection protocol services. No business RPCs are present. See [gRPC communication foundation](../architecture/Grpc.md).

After starting the stack, run the gRPC network smoke test from PowerShell:

```powershell
./scripts/dev/GrpcHealthSmoke.ps1
```

## Health endpoint semantics

- `/health/live` reports whether the process is alive. It does not connect to PostgreSQL, RabbitMQ, another service, the AI service, the Gateway, or the internet.
- `/health/ready` for Identity, Parking, Booking, Payment, and Notification checks that API's own PostgreSQL DbContext, MassTransit bus health, and an authenticated AMQP connection to RabbitMQ. It returns HTTP 200 when both dependencies are healthy and HTTP 503 when either dependency check fails.
- Gateway readiness remains independent of downstream APIs. AI and IoT do not have PostgreSQL readiness checks.

When PostgreSQL or RabbitMQ is stopped, the five API processes remain running and their `/health/live` endpoints continue returning 200. Their `/health/ready` endpoints return 503. RabbitMQ readiness uses an authenticated AMQP handshake because the MassTransit bus health check alone does not verify broker availability for services without receive endpoints. After the dependency starts again, readiness recovers to 200 without restarting the APIs.

HTTP requests may send `X-Correlation-ID` with a Guid value (up to 128 characters). The Gateway and service hosts preserve a valid value or generate a new one for missing or invalid input, then return it in the same response header. Example:

```powershell
curl.exe -i -H "X-Correlation-ID: 6B0D3575-9F7F-4366-9C00-5F606E694331" http://localhost:5000/health/live
```

Errors use safe `application/problem+json` responses. See [error handling](../architecture/Error-Handling.md) and [correlation](../architecture/Correlation.md).

To exercise this behavior without deleting local data:

```powershell
docker compose stop postgres
docker compose start postgres
```

Wait for PostgreSQL to become healthy before checking that readiness has recovered. Do not use `docker compose down -v` for this check.

## Ports and health URLs

All published ports bind to `127.0.0.1` by default.

| Component | Host port | Health URL or access |
| --- | ---: | --- |
| API Gateway | 5000 | `http://localhost:5000/health/live`, `/health/ready` |
| Identity API | 5001 | `http://localhost:5001/health/live`, `/health/ready` |
| Parking API | 5002 | `http://localhost:5002/health/live`, `/health/ready` |
| Booking API | 5003 | `http://localhost:5003/health/live`, `/health/ready` |
| Payment API | 5004 | `http://localhost:5004/health/live`, `/health/ready` |
| Notification API | 5005 | `http://localhost:5005/health/live`, `/health/ready` |
| AI Service | 8000 | `http://localhost:8000/health/live`, `http://localhost:8000/health/ready` |
| Jaeger UI | 16686 | `http://localhost:16686` |
| Prometheus UI | 9090 | `http://localhost:9090` |
| OTLP/gRPC | 4317 | SDKs on the host connect to `localhost:4317`; Docker apps use `otel-collector:4317` |
| OTLP/HTTP | 4318 | Host SDKs can connect to `localhost:4318`; Docker apps use `otel-collector:4318` |
| .NET API gRPC | 8081 (container only) | `identity-api:8081`, `parking-api:8081`, `booking-api:8081`, `payment-api:8081`, `notification-api:8081` on `parking-network` |
| AI gRPC | 50051 (container only) | `ai-service:50051` on `parking-network` |
| PostgreSQL | 5433 (container: 5432) | Host tools use `localhost:5433`; containers use `postgres:5432`. Database `parking_system`, user `parking_dev` |
| RabbitMQ AMQP | 5672 | Host `localhost`, user `parking_dev`, password `parking_dev_local_only`, vhost `/` |
| RabbitMQ Management | 15672 | `http://localhost:15672` (same local RabbitMQ credentials) |
| RabbitMQ MQTT | 1883 | Host `localhost`, same local RabbitMQ credentials |

The .NET APIs use AMQP on `rabbitmq:5672` inside Compose (`localhost:5672` from the host). The IoT Simulator uses MQTT on `rabbitmq:1883`. RabbitMQ Management remains on `localhost:15672`; AMQP, MQTT, and Management are separate listeners/protocols.

Each .NET app currently has `/health/live` and `/health/ready` routes, but Compose does not treat those routes as dependency-aware checks. The AI image has an HTTP health check against `/health/ready`. PostgreSQL and RabbitMQ have their own container health checks.

Host ports can be changed in `.env` with `POSTGRES_HOST_PORT`, `RABBITMQ_HOST_PORT`, `RABBITMQ_MANAGEMENT_HOST_PORT`, `RABBITMQ_MQTT_HOST_PORT`, and the API port variables.

## Logs and stop commands

### Non-blocking local runtime warnings

- RabbitMQ may report that `management_metrics_collection` is deprecated. It does not prevent the local Management, AMQP, or MQTT listeners from starting.
- Gateway DataProtection keys are stored in the container filesystem and are local, ephemeral keys. Recreating the Gateway container may replace them. This skeleton has no authentication or cookie-protected business flows that depend on a persistent key ring.

These warnings are accepted for local development; they do not require Redis, a persistent key store, or RabbitMQ configuration workarounds in this phase.

Follow logs for the whole stack or selected services:

```sh
docker compose logs -f
docker compose logs -f postgres rabbitmq
```

Stop containers and remove the Compose network while preserving named volumes:

```sh
docker compose down
```

To intentionally reset local database and broker data, remove the named volumes too:

```sh
docker compose down -v
```

The reset command deletes the PostgreSQL and RabbitMQ data stored in this Compose stack, and the owner application documents that Identity keeps on the `parking_identity_owner_documents` volume (mounted at `/app/data/owner-documents`, which the Identity image creates for its non-root user).

## PostgreSQL ownership

Compose initializes one database named `parking_system`. Each service's explicit EF migration owns its schema, `outbox_messages` table, and migration history table. The API does not apply migrations during startup. See `docs/data/Data-Ownership.md` for schema ownership.

### Identity migration `AddUniqueIndexToOwnerTaxCode`

The migration `20261023091247_AddUniqueIndexToOwnerTaxCode` creates a unique index on `identity.accounts.owner_tax_code`, so it fails on a database in which two accounts already hold the same tax code (test data of older demos). Check before running `dotnet ef database update`:

```sql
select owner_tax_code, count(*) from identity.accounts where owner_tax_code is not null group by 1 having count(*) > 1;
```

Change the duplicate codes first if the query returns rows.

## Local JWT verification key

The .NET Gateway and APIs validate RS256 tokens with a public key. Before starting the full Compose stack, generate a local development key pair once:

```powershell
./scripts/security/New-DevelopmentJwtKey.ps1
```

The script creates `.local/security/jwt/private.pem` and `public.pem` and refuses to overwrite an existing file. Validators mount only `public.pem`, read-only. `identity-api` also mounts `private.pem`, read-only, because it signs access tokens. The private key is ignored by Git and excluded from Docker build contexts and images. Never paste either key into tracked files, logs, or documentation. Do not generate keys on application startup.

Gateway browser origins are configured in `.env` with comma-separated explicit origins:

```dotenv
GATEWAY_CORS_ALLOWED_ORIGINS=http://localhost:3000
```

Leave it empty until a frontend origin is selected; an empty list allows no browser origins. This local example does not define a production domain. See [Security](../architecture/Security.md) for route authorization and token validation policy.

Security plumbing can be exercised without production auth endpoints using the test-only hosts:

```powershell
dotnet test tests/WebFoundationTests/WebFoundationTests.csproj --filter FullyQualifiedName~SecurityFoundationTests
```

## Frontend local development

The one Next.js app is in `web/`. From the repository root:

```powershell
cd web
npm ci
npm run dev
```

Browse `http://localhost:3000`. The browser uses `NEXT_PUBLIC_API_BASE_URL=http://localhost:5000` for YARP Gateway requests; Docker-only hostname `gateway:8080` is not browser-resolvable. Run `npm run lint`, `npm run typecheck`, `npm run test:run`, and `npm run build` for the technical baseline. Install Chromium with `npx playwright install chromium` once, then run `npm run test:e2e` for shell and 404 smoke checks.

The local Compose Gateway explicitly allows origin `http://localhost:3000` by default through `GATEWAY_CORS_ALLOWED_ORIGINS`; unrelated origins are not allowed. Production origins remain configurable. `web/.env.example` contains only browser-visible variable names. The public Mapbox token is optional until an interactive map feature is used. Never copy a private Mapbox token or backend secret into `NEXT_PUBLIC_*`.

Compose also offers the production-style frontend container: `docker compose build web` and `docker compose up -d web`. The container serves `http://localhost:3000`. See [Frontend architecture](../architecture/Frontend.md) and [Web README](../../web/README.md).