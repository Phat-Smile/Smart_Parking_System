# gRPC communication foundation

## Runtime boundary

The five .NET business APIs host two listeners in the same process:

| Listener | Container port | Protocol | Consumers |
| --- | ---: | --- | --- |
| REST | 8080 | HTTP/1.1 | API Gateway and local REST clients |
| Internal gRPC | 8081 | HTTP/2 cleartext (h2c) | Future service-to-service clients on the Compose network |

The AI service runs FastAPI on HTTP port `8000` and gRPC on port `50051` concurrently. The Gateway remains REST-only. The gRPC listeners are exposed to the Compose network but are not published to host ports. Local Docker-to-Docker communication uses plaintext; production TLS or mTLS is a later deployment concern.

## Health and reflection

Each of the five .NET hosts and the AI service implements the standard `grpc.health.v1.Health` protocol and reports the empty service name as `SERVING` while its gRPC listener is running. This technical gRPC status is independent of PostgreSQL and RabbitMQ readiness; existing REST `/health/live` and `/health/ready` semantics remain unchanged.

Server reflection is registered only when a host is in Development. In Production it is not registered. Reflection lists only standard health and reflection services (`grpc.health.v1.Health` and the runtime's standard `grpc.reflection.v1` and/or `v1alpha` service); no business RPC is implemented in this phase.

Run the container-network check after starting the stack:

```powershell
./scripts/dev/GrpcHealthSmoke.ps1
```

It checks Health/Check returns `SERVING` and reflection lists only standard health/reflection services on the five .NET APIs and AI service.

## Contract ownership and evolution

No business `.proto` files or generated clients belong to this foundation. When a synchronous business contract is approved, the provider service owns its source under `contracts/grpc/<provider>/v1/`. The protobuf `package` must include the bounded context and major version, for example `parking.booking.v1`. Contract source and generated artifacts are versioned; consumers depend on the provider's published contract artifact, never on another service's implementation project.

Within the same major version, changes must remain wire-compatible and additive. A breaking contract change introduces a new major package and directory such as `v2`, allows a migration period where needed, and removes the old version only after consumers have moved. Transport client/channel code belongs in the calling service's infrastructure or host composition; Domain, Application, Persistence, and shared BuildingBlocks remain free of gRPC transport dependencies.

Do not put REST Gateway routing, authentication policy, or business logic into reflection/health setup. Business methods require an approved service-owned contract and a later implementation phase.
