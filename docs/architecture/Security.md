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

## Service key (gateway to service)

The Gateway adds an `X-Service-Key` header to every proxied request (replacing any value sent by the client). Each of the five business services checks it before authentication through `UseParkingSystemServiceSecurity()` and answers 403 when the header is missing or different. The comparison is constant time. `/health/*` is exempt so container probes keep working. The Gateway itself does not require the key from browsers.

`ServiceAuth:ServiceKey` (env `SERVICE_KEY`, see `.env.example`) set to empty disables the check; this is the local default. Set a long random value in every shared or production environment, the same value for the Gateway and all services. Service-to-service gRPC calls must send the same header once a service calls another service.

An architecture test fails if a service `Program.cs` does not call `UseParkingSystemServiceSecurity()`.

## Error and data handling

Unauthenticated protected requests receive 401 and authenticated but unauthorized requests receive 403, in the existing safe ProblemDetails format with status, title, traceId, correlationId, and a safe error code. Bearer challenge behavior and `WWW-Authenticate` are preserved. Token validation diagnostics, tokens, claims, cookies, credentials, and key material are not returned to clients.

Structured request logging records safe request metadata and identifiers only. Authorization headers, JWTs, subject claims, PII, and PEM contents are not added to logs, spans, or metrics. No authentication claims are automatically attached to telemetry. Database schemas, migrations, and business data are unchanged by this foundation.

---

## TECH-11: Cross-Service JWT Hardening & Key Confusion Immunity

Tất cả các microservices (`Identity`, `Parking`, `Booking`, `Payment`, `Notification`) và `Gateway` đều cấu hình tường minh phần `Security:Jwt` trong `appsettings.json` (Issuer, Audience, ClockSkewSeconds, PublicKeyPath) và xác thực độc lập thông qua bộ extension `AddParkingSystemJwtBearer()`.

Bộ kiểm thử an ninh `SecurityFoundationTests` (`tests/WebFoundationTests/SecurityFoundationTests.cs`) kiểm tra và bảo đảm 100% các rào chắn bảo mật sau:
1. **Algorithm Confusion Immunity**: Từ chối tuyệt đối token ký bằng thuật toán đối xứng (HMAC-SHA256 / `HS256`) giả mạo bằng raw public key bytes hoặc chuỗi PEM -> trả về HTTP 401 `auth.unauthorized`.
2. **Signature Bypass (`alg: none`)**: Từ chối tất cả các biến thể signature bypass như `none`, `None`, `NONE` -> trả về HTTP 401 `auth.unauthorized`.
3. **Issuer & Audience Spoofing**: Yêu cầu bắt buộc `Issuer = ParkingSystem.Identity` và `Audience = ParkingSystem`. Bất kỳ token nào từ issuer/audience lạ đều bị từ chối 401.
4. **Temporal Attack Prevention**: Xác thực nghiêm ngặt thời hạn sống (`exp`) với clock skew tối đa 30s. Token hết hạn hoặc thiếu `exp` bị từ chối 401.
5. **Untrusted Key Rejection**: Token ký bằng private key không khớp với `public.pem` phân phối bị từ chối 401.
6. **Credential & Secret Leakage Prevention**: Token JWT, khóa PEM, và header `Authorization: Bearer` bị tự động lọc sạch khỏi Serilog structured logs và OpenTelemetry activity traces.

---

## TECH-12: Multi-Layer Authorization & Per-Route Access Matrix

Hệ thống bảo vệ tài nguyên theo mô hình Defense-in-Depth qua 3 lớp:
1. **Lớp Gateway (`X-Service-Key`)**: Đảm bảo traffic chỉ đi qua Gateway hợp lệ. Bất kỳ request trực tiếp nào đến downstream API mà thiếu header hoặc sai secret key đều bị chặn 403 `auth.forbidden`.
2. **Lớp Token Claim Authentication**: Xác thực chữ ký RS256 và giải mã thông tin `sub` (User ID / Account ID) cùng `role` (`SystemAdmin`, `ParkingLotOwner`, `Staff`, `Customer`).
3. **Lớp Fine-Grained Resource Access Control (`IParkingLotAccess`)**: Chỉ có role trong JWT là chưa đủ để thao tác dữ liệu bãi đỗ. `IParkingLotAccess.CanOperateAsync(accountId, lotId)` kiểm tra trực tiếp quan hệ phân công / sở hữu thời gian thực trong cơ sở dữ liệu (`ParkingLotAssignments`). Nếu gỡ phân công nhân viên, quyền thao tác bị thu hồi ngay lập tức mà không cần đợi JWT hết hạn.

### Bảng Ma Trận Phân Quyền Theo Route (Per-Route Access Matrix)

| Service | HTTP Method & Route | Quyền / Policy | Cơ Chế Kiểm Soát IDOR & Resource Scope |
|---|---|---|---|
| **Identity** | `POST /api/identity/accounts/login` | Public (Rate Limited) | TECH-10 IP Rate Limit (10 req/min) |
| **Identity** | `POST /api/identity/accounts` (Register) | Public (Rate Limited) | TECH-10 IP Rate Limit (5 req/min) |
| **Identity** | `GET /api/identity/accounts/me` | `RequireAuthenticatedUser` | Trả về thông tin account ứng với `ICurrentUser.SubjectId` |
| **Identity** | `POST /api/identity/owner-applications` | `RequireAuthenticatedUser` | User nộp hồ sơ xin quyền Owner |
| **Identity** | `GET /api/identity/admin/owner-applications` | `RequireRole("SystemAdmin")` | Chỉ Admin được xem danh sách hồ sơ Owner |
| **Identity** | `POST /api/identity/admin/owner-applications/{id}/approve` | `RequireRole("SystemAdmin")` | Chỉ Admin được phê duyệt hồ sơ Owner |
| **Identity** | `POST /api/identity/admin/owner-applications/{id}/reject` | `RequireRole("SystemAdmin")` | Chỉ Admin được từ chối hồ sơ Owner |
| **Parking** | `GET /api/parking/lots` | Public | Chỉ trả về các bãi xe có trạng thái `Approved` |
| **Parking** | `POST /api/parking/lots` | `RequireRole("ParkingLotOwner", "SystemAdmin")` | Chủ bãi khởi tạo bãi xe ở trạng thái `Pending` |
| **Parking** | `POST /api/parking/admin/lots/{id}/approve` | `RequireRole("SystemAdmin")` | Chỉ Admin duyệt bãi xe lên công khai |
| **Parking** | `POST /api/parking/lots/{id}/members` | `RequireRole("ParkingLotOwner", "SystemAdmin")` | `ParkingLotAccessGuard`: Chỉ Owner của chính bãi xe đó mới được phân công nhân viên |
| **Parking** | `POST /api/parking/sessions` (Open Session) | `RequireRole("Staff", "ParkingLotOwner", "SystemAdmin")` | `IParkingLotAccess.CanOperateAsync`: Nhân viên phải được phân công vào bãi xe mới được check-in |
| **Parking** | `POST /api/parking/sessions/verify-exit` | `RequireRole("Staff", "ParkingLotOwner", "SystemAdmin")` | `IParkingLotAccess.CanOperateAsync`: Nhân viên phải thuộc bãi xe mới được check-out |
| **Parking** | `GET /api/parking/sessions/by-qr/{code}` | `RequireRole("Staff", "ParkingLotOwner", "SystemAdmin")` | `IParkingLotAccess.CanOperateAsync`: Tra cứu phiên xe tại bãi được giao |
| **Parking** | `GET /api/parking/customer/sessions/me` | `RequireAuthenticatedUser` | Chỉ hiển thị các phiên đỗ của chính `ICurrentUser.SubjectId` |
| **Parking** | `GET /api/parking/lots/{id}/layout` | `RequireAuthenticatedUser` | Public / Xem sơ đồ slot theo bãi |
| **Parking** | `PUT /api/parking/lots/{id}/layout` | `RequireRole("ParkingLotOwner", "SystemAdmin")` | `ParkingLotAccessGuard`: Chỉ chủ sở hữu bãi xe mới được sửa sơ đồ bãi |
| **Booking** | `POST /api/booking/holds` | `RequireAuthenticatedUser` | User giữ chỗ tạm thời (10 phút), giới hạn theo capacity guard |
| **Booking** | `POST /api/booking/reservations/{id}/confirm` | `RequireAuthenticatedUser` | Xác nhận đặt chỗ thành công sau khi hold/thanh toán, trả mã QR |
| **Booking** | `GET /api/booking/reservations/me` | `RequireAuthenticatedUser` | Chỉ hiển thị danh sách đặt chỗ của chính `ICurrentUser.SubjectId` |
| **Booking** | `POST /api/booking/reservations/{id}/cancel` | `RequireAuthenticatedUser` | Chỉ cho phép chính chủ hủy đặt chỗ hợp lệ |
| **Payment** | `GET /api/payment/wallets/me` | `RequireAuthenticatedUser` | Xem số dư ví của chính `ICurrentUser.SubjectId` |
| **Payment** | `POST /api/payment/wallets/topup` | `RequireAuthenticatedUser` | Tạo giao dịch nạp tiền ví qua VNPay Sandbox |
| **Payment** | `GET /api/payment/vnpay/callback` | Public (HMAC Verified) | Webhook VNPay xác thực chữ ký số SHA512 & idempotency |

---

## WebSocket & Realtime Proxying qua YARP Gateway

Hệ thống hỗ trợ kết nối thời gian thực (Realtime WebSockets và SignalR Hubs) thông qua YARP Reverse Proxy:
1. **Cơ Chế Bắt Tay WebSocket**:
   - Client gửi HTTP/1.1 request với headers `Connection: Upgrade` và `Upgrade: websocket`.
   - YARP Gateway nhận diện yêu cầu nâng cấp giao thức, giữ nguyên các header và chuyển tiếp tới downstream microservice tương ứng (ví dụ: `Notification.WebAPI` hoặc `Parking.WebAPI`).
   - Downstream service trả về HTTP status `101 Switching Protocols`.
   - Kể từ thời điểm này, YARP thiết lập kết nối song công (full-duplex tunnel), cho phép truyền nhận các khung dữ liệu (frames) văn bản/nhị phân hai chiều trực tiếp mà không làm gián đoạn hay giải mã lại gói tin.
2. **SignalR Route Matching**:
   - SignalR client thực hiện 2 bước: bước 1 HTTP POST `negotiate` để nhận connectionToken, bước 2 WebSocket Upgrade `GET /api/<service>/hubs/{hubName}?id={token}`.
   - Nhờ cấu hình Route Catch-All của YARP (`/api/notification/{**catch-all}`, `/api/parking/{**catch-all}`), cả 2 bước `negotiate` và WebSocket upgrade đều được định tuyến chuẩn xác tới cùng cluster downstream mà không cần cấu hình thêm plugin ngoài.
3. **Bằng Chứng Kiểm Thử Tự Động**:
   - Được kiểm chứng tự động bằng test case `YarpWebSocketSpikeTests.Yarp_gateway_transparently_proxies_websocket_traffic_and_preserves_frames` (`tests/WebFoundationTests/YarpWebSocketSpikeTests.cs`). Kết quả kiểm thử: **HTTP 101 Switching Protocols handshake thành công, frame echo toàn vẹn, kết nối đóng êm (NormalClosure)**.
