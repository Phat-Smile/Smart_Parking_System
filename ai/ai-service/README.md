# AI Service

Python 3.12 scaffold with FastAPI HTTP on port `8000` and standard gRPC health on internal port `50051`. FastAPI starts and stops the gRPC server in its application lifecycle, including in the Compose container. Development enables gRPC reflection. No AI business logic, prediction model, or business RPC exists.

`app/api` contains HTTP health routes; `app/grpc` contains the technical gRPC server. `app/application`, `app/domain`, and `app/infrastructure` are reserved for future slices. `tests` contains the foundation tests.

`/health/live` reports a running HTTP process. `/health/ready` reports AI application startup readiness; it does not check PostgreSQL, RabbitMQ, or telemetry backends.

Use Python 3.12 with the locked dependencies. From this directory:

```sh
python -m pip install -r requirements.lock
python -m uvicorn app.main:app --host 127.0.0.1 --port 8000 --no-access-log
python -m unittest discover -s tests -v
```

The HTTP command also starts gRPC on port `50051`. The root Compose stack builds the service from `python:3.12-slim-bookworm` and publishes only HTTP on `localhost:8000`; gRPC is available only on the Compose network. `pyproject.toml` is the human-maintained direct dependency declaration; `requirements.lock` pins the validated runtime and test environment. To regenerate it, build the AI image with reviewed `pyproject.toml` dependencies, run `python -m pip freeze` in that Python 3.12 image, omit the local `parking-ai-service @ file:///app` entry, then validate a clean environment before committing the lock.
