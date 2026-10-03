# gRPC contracts

No business contracts or generated clients are defined yet. Standard gRPC health and development-only reflection use the protocol's standard descriptors and are not business contracts.

When approved, each provider owns its contract under `<provider>/v1/` (for example, `booking/v1/`). The protobuf package includes the bounded context and major version. Consumers use a versioned published contract artifact rather than referencing a provider implementation project. Additive wire-compatible changes stay within a major version; breaking changes introduce a new major version. See [gRPC communication foundation](../../docs/architecture/Grpc.md) for the ownership and evolution policy.

