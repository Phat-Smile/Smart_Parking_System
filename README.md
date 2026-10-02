# Parking System Backend

Smart Parking backend skeleton. The repository contains architecture boundaries and service startup scaffolds; it does not implement the User Stories yet.

## Components

- Five .NET 10 business services: Identity, Parking, Booking, Payment, and Notification. Each has Domain, Application, Persistence, Infrastructure, and WebAPI projects.
- YARP API Gateway with empty route/cluster configuration.
- AI Service scaffold using Python 3.12, FastAPI health routes, and a standalone gRPC bootstrap.
- IoT Simulator scaffold.
- Shared technical BuildingBlocks.
- Ten per-service test projects and one executable ArchitectureTests project.

## Architecture invariants

Guest does not need an Account and is not a database role. Registered and guest entry flows are documented without implementation. SessionQR belongs to one ParkingSession, not to a User or Vehicle. See docs/architecture/ and docs/data/.

No service has project references to another business service. See docs/architecture/Dependency-Rules.md.

## Prerequisites

- .NET SDK 10.0.302 (pinned in global.json)
- Python 3.12 for the AI service
- Docker Desktop or Docker Engine with Docker Compose v2

## Build and test

From the repository root:

    dotnet restore ParkingSystem.slnx
    dotnet build ParkingSystem.slnx
    dotnet test ParkingSystem.slnx

ArchitectureTests reads the production service project references and enforces the dependency rules. Unit and integration projects contain only test-harness assembly-loading smoke checks; they contain no fabricated business tests. ArchitectureTests runs project-reference rules.

## Run a service scaffold

For example:

    dotnet run --project src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj

Each .NET WebAPI exposes /health/live and /health/ready. Liveness means the process can serve a request. Readiness currently checks application startup/request readiness only; no infrastructure dependency checks are configured. Swagger/OpenAPI is enabled in Development.

Gateway routes and clusters are intentionally empty. It has no fake destinations, production URLs, authentication implementation, or business logic.

## AI service

See ai/ai-service/README.md for the Python 3.12 structure and run commands. Health readiness means application startup only; it does not report PostgreSQL, RabbitMQ, or external dependency status.

## Docker quick start

Copy `.env.example` to `.env`. Its credentials are for local development only.

Start PostgreSQL and RabbitMQ:

    docker compose up -d postgres rabbitmq

Build and boot the full skeleton stack:

    docker compose up --build

Compose service names are `postgres`, `rabbitmq`, `gateway`, `identity-api`, `parking-api`, `booking-api`, `payment-api`, `notification-api`, `ai-service`, and `iot-simulator`.

The services publish to localhost: Gateway `5000`, Identity `5001`, Parking `5002`, Booking `5003`, Payment `5004`, Notification `5005`, and AI `8000`. All .NET services and the Gateway expose `/health/live` and `/health/ready`; AI has the same paths on port `8000`. PostgreSQL is on host port `5433` (container port `5432`), RabbitMQ AMQP on `5672`, Management UI at `http://localhost:15672`, and MQTT on `1883`.

PostgreSQL and RabbitMQ have Docker health checks. Application health routes report process/application startup only; having an infrastructure container available does not mean application persistence or messaging integration is implemented. The IoT simulator starts idle and sends no traffic.

See [docs/development/Local-Development.md](docs/development/Local-Development.md) for connection details, logs, health checks, and volume reset instructions.

## Not included yet

- PostgreSQL schemas, business tables, DbContext connections, or EF migrations
- Application messaging integration, MassTransit, or MQTT client integration
- Business features or business entities
- gRPC service contracts or integration-event contracts

Database schemas and persistence setup are planned for Phase 04. Application integrations and business behavior are handled in later phases.
