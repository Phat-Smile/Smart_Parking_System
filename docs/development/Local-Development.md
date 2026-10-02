# Local development

## Prerequisites

- Docker Desktop or Docker Engine with Docker Compose v2
- .NET SDK 10.0.302 for running or validating .NET projects on the host
- Python 3.12 only when running the AI service directly on the host

The Compose stack builds the AI container with Python 3.12. It does not change the Python version installed on the host.

The .NET Docker build stages use SDK `10.0.301` and target `net10.0`; host builds continue to use the SDK pinned by `global.json` (`10.0.302`). SDK `10.0.301` is used as a Docker build workaround while an [upstream Windows/Linux-container restore issue](https://github.com/dotnet/sdk/issues/55340) for `10.0.302` remains open.

## Environment setup

From the repository root, create `.env` from the example if it does not already exist. On PowerShell:

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
```

The example PostgreSQL and RabbitMQ credentials are public local-development values. Do not reuse them outside this local stack. `.env` is ignored by Git.

The PostgreSQL port inside Docker is `5432`; the host port is `5433` by default. The five API containers receive `ConnectionStrings__DefaultConnection` from Compose, built from `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD`. Their connection uses `postgres:5432` on the Compose network. No database password is stored in the API `appsettings.json` files.

Tools and API processes running directly on the host use `localhost:5433`. For example, to run an API directly from PowerShell with the local development credentials:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5433;Database=parking_system;Username=parking_dev;Password=parking_dev_local_only'
$env:RabbitMq__Host = 'localhost'
$env:RabbitMq__Port = '5672'
$env:RabbitMq__Username = 'parking_dev'
$env:RabbitMq__Password = '<copy from your local .env>'
$env:RabbitMq__VirtualHost = '/'
```

The API requires these `RabbitMq__*` process variables when run directly on the host. Read the password from the ignored local `.env`; do not commit or copy it into project files.

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

This builds and starts the Gateway, five .NET APIs, AI HTTP service, IoT simulator, PostgreSQL, and RabbitMQ. The five .NET APIs receive `RabbitMq__Host`, `RabbitMq__Port`, `RabbitMq__Username`, `RabbitMq__Password`, and `RabbitMq__VirtualHost` from the existing `RABBITMQ_*` variables and connect over authenticated AMQP on port `5672`. The IoT simulator receives `MQTT_HOST`, `MQTT_PORT`, `MQTT_USERNAME`, and `MQTT_PASSWORD` from the same local RabbitMQ settings and uses MQTT port `1883`; it does not connect to MQTT or generate traffic in this scaffold.

The five business APIs require valid PostgreSQL and RabbitMQ configuration to start. They do not open PostgreSQL during startup and do not apply migrations automatically. MassTransit starts one RabbitMQ AMQP bus per API and uses its built-in reconnect behavior. A temporarily unavailable broker does not prevent the API process from starting.

## Health endpoint semantics

- `/health/live` reports whether the process is alive. It does not connect to PostgreSQL, RabbitMQ, another service, the AI service, the Gateway, or the internet.
- `/health/ready` for Identity, Parking, Booking, Payment, and Notification checks that API's own PostgreSQL DbContext, MassTransit bus health, and an authenticated AMQP connection to RabbitMQ. It returns HTTP 200 when both dependencies are healthy and HTTP 503 when either dependency check fails.
- Gateway readiness remains independent of downstream APIs. AI and IoT do not have PostgreSQL readiness checks.

When PostgreSQL or RabbitMQ is stopped, the five API processes remain running and their `/health/live` endpoints continue returning 200. Their `/health/ready` endpoints return 503. RabbitMQ readiness uses an authenticated AMQP handshake because the MassTransit bus health check alone does not verify broker availability for services without receive endpoints. After the dependency starts again, readiness recovers to 200 without restarting the APIs.

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
| PostgreSQL | 5433 (container: 5432) | Host tools use `localhost:5433`; containers use `postgres:5432`. Database `parking_system`, user `parking_dev` |
| RabbitMQ AMQP | 5672 | Host `localhost`, user `parking_dev`, password `parking_dev_local_only`, vhost `/` |
| RabbitMQ Management | 15672 | `http://localhost:15672` (same local RabbitMQ credentials) |
| RabbitMQ MQTT | 1883 | Host `localhost`, same local RabbitMQ credentials |

The .NET APIs use AMQP on `rabbitmq:5672` inside Compose (`localhost:5672` from the host). The IoT Simulator uses MQTT on `rabbitmq:1883`. RabbitMQ Management remains on `localhost:15672`; AMQP, MQTT, and Management are separate listeners/protocols.

Each .NET app currently has `/health/live` and `/health/ready` routes, but Compose does not treat those routes as dependency-aware checks. The AI image has an HTTP health check against `/health/ready`. PostgreSQL and RabbitMQ have their own container health checks.

Host ports can be changed in `.env` with `POSTGRES_HOST_PORT`, `RABBITMQ_HOST_PORT`, `RABBITMQ_MANAGEMENT_HOST_PORT`, `RABBITMQ_MQTT_HOST_PORT`, and the API port variables.

## Logs and stop commands

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

The reset command deletes the PostgreSQL and RabbitMQ data stored in this Compose stack.

## PostgreSQL ownership

Compose initializes one database named `parking_system`. Each service's explicit EF migration owns its schema, `outbox_messages` table, and migration history table. The API does not apply migrations during startup. See `docs/data/Data-Ownership.md` for schema ownership.
