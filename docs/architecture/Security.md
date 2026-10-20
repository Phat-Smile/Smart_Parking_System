# Security Foundation

## Phase 06C scope

Phase 06C validates access tokens but does not issue them. Register, login, email OTP, Google login, password hashing, refresh-token persistence, profile management, business roles, and business APIs are not implemented. Tests can create short-lived test-only tokens.

Authentication establishes a validated principal. Authorization is applied by endpoint policy. The foundation contains one technical policy, `authenticated-user`; it defines no business roles or permissions.

## JWT validation and claims

The Gateway and Identity, Parking, Booking, Payment, and Notification APIs validate JWTs independently with ASP.NET Core JwtBearer. The canonical policy is RS256 only, issuer `ParkingSystem.Identity`, audience `ParkingSystem`, required signature and expiration, lifetime validation, and 30 seconds of clock skew. Missing, malformed, private-key, or undersized public-key configuration fails startup. There is no signing-secret fallback and validators do not issue tokens.

The canonical user identity claim is the standard `sub` claim. Inbound claim mapping is disabled. The existing transport-neutral `ICurrentUser` exposes the authenticated state and raw subject string through an ASP.NET Core adapter; Domain and Application do not depend on ASP.NET Core, `ClaimsPrincipal`, or JWT types. Tokens should not contain profile PII or uncontrolled business claims. The optional `role` claim has no policy meaning in this phase.

Future Identity authentication endpoints (register, login, email OTP verification, and applicable Google initiation/callback endpoints) must explicitly allow anonymous access. Future business HTTP endpoints should use `MapAuthenticatedGroup` or `RequireAuthenticatedUser`; a YARP route must explicitly select public or `authenticated-user` authorization. There is no fallback authorization policy, so unmapped paths remain 404.

## Key ownership

Identity owns the private signing key and is the only service that receives it. The Gateway and the other APIs receive only the public verification key. The local development generator creates `.local/security/jwt/private.pem` and `public.pem`; it refuses to overwrite either file. `.local/` is ignored by Git and Docker build contexts. Compose mounts `public.pem` read-only at `/run/parking-system/security/jwt/public.pem` for every API, and `private.pem` read-only at `/run/parking-system/security/jwt/private.pem` for `identity-api` only (`Security__Jwt__PrivateKeyPath`). Private key content must never be pasted into source, configuration examples, container images, logs, or telemetry.

Key rotation and JWKS/KID distribution are future work. No revocation or refresh-token lifecycle exists yet.

## Gateway, APIs, and gRPC

The Gateway validates protected requests, forwards the standard `Authorization: Bearer` header through YARP, and applies a route's declared policy. Each downstream API validates the same token again. Current local container-to-container traffic uses a trusted Compose network; user-context propagation is not machine identity. Production service-to-service security may use mTLS, workload identity, or service credentials in a future phase.

Future business gRPC service classes or methods can use ASP.NET Core authorization with `authenticated-user`. Clients send a token as `authorization: Bearer <token>` metadata. Standard `grpc.health.v1.Health` remains anonymous. Reflection remains Development-only and anonymous. No business gRPC method is present in production.

AI remains internal. Future business AI RPCs must be called by .NET services and must not be exposed directly to browser clients. The local FastAPI host port 8000 is a development diagnostic endpoint; the AI gRPC listener is internal to the Compose network. This is not the production network exposure policy.

## HTTP protections

Only the Gateway configures CORS. `Security:Cors:AllowedOrigins` is a comma-separated list of explicit HTTP/HTTPS origins. An empty list permits no browser origins. Allowed methods are GET, POST, PUT, PATCH, and DELETE; allowed request headers are Authorization, Content-Type, and X-Correlation-ID. Only X-Correlation-ID is exposed. Credentials are not enabled. CORS preflight runs before authorization while protected requests still require their route policy.

The Gateway adds `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, and `X-Frame-Options: DENY`. A JSON API does not define a Content-Security-Policy for a future frontend. Local containers use HTTP and gRPC h2c; production TLS belongs at a correctly configured ingress or deployment boundary. HTTPS redirection and HSTS are not enabled in local containers.

JWT APIs use the Authorization header rather than cookies, so this phase adds no cookie or antiforgery middleware. Revisit CSRF policy if credentials move to cookies. Browser access-token and refresh-token storage remains a VS01 design decision. The Gateway rate-limits the public Auth routes per client IP with a one-minute fixed window (TECH-10): login 10, register 5, resend-otp 5, verify-email 10, refresh 30, forgot-password 5, reset-password 10, google 10. Each limit can be overridden with `RateLimiting:Auth:<policy>`. Over the limit the Gateway answers 429 with `Retry-After` and `errorCode = gateway.rate_limited`, and the request never reaches Identity. `X-Forwarded-For` is not trusted because no trusted proxy sits in front of the Gateway yet. There is no global limit on other routes.

## Error and data handling

Unauthenticated protected requests receive 401 and authenticated but unauthorized requests receive 403, in the existing safe ProblemDetails format with status, title, traceId, correlationId, and a safe error code. Bearer challenge behavior and `WWW-Authenticate` are preserved. Token validation diagnostics, tokens, claims, cookies, credentials, and key material are not returned to clients.

Structured request logging records safe request metadata and identifiers only. Authorization headers, JWTs, subject claims, PII, and PEM contents are not added to logs, spans, or metrics. No authentication claims are automatically attached to telemetry. Database schemas, migrations, and business data are unchanged by this foundation.
