# AI Service

Python 3.12 scaffold with FastAPI health routes and a gRPC server bootstrap. It contains no AI business logic, prediction behavior, or trained model.

Structure:
- app/api: HTTP health routes
- app/application: reserved application layer
- app/domain: reserved domain layer
- app/grpc: gRPC server bootstrap; no service contract is registered yet
- app/infrastructure: reserved provider adapters
- tests: reserved test directory

Health semantics:
- /health/live reports that the HTTP process is serving requests.
- /health/ready reports application startup readiness only. It checks no PostgreSQL, RabbitMQ, or external dependency.

Install and run from this directory:

    python3.12 -m pip install -e .
    python3.12 -m uvicorn app.main:app --host 127.0.0.1 --port 8000

The root Docker Compose stack builds this service from `python:3.12-slim-bookworm` and exposes its health routes on `http://localhost:8000`. The image installs dependencies from `pyproject.toml` only. Its gRPC bootstrap remains a separate command and is not started in the HTTP container.

Run the standalone gRPC bootstrap:

    python3.12 -m app.grpc.server
