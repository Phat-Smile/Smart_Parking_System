# AUTOTEST_CONTEXT.md
> Context file cho auto-test-demo — Identity Service API Testing
> Base URL: http://localhost:5000 (qua Gateway)

---

## 1. Mục đích

File này cung cấp đủ context để chạy test cases cho Identity Service API — bao gồm cả endpoints đã có và endpoints đang phát triển.

---

## 2. Test Environment

### 2.1 Prerequisites

```bash
# Infrastructure (phải chạy trước)
docker compose up -d postgres rabbitmq

# Apply migrations
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5433;Database=parking_system;Username=parkwise;Password=parkwise123"
dotnet ef database update --project src/Services/Identity/Identity.Persistence/Identity.Persistence.csproj --startup-project src/Services/Identity/Identity.WebAPI/Identity.WebAPI.csproj --context IdentityDbContext

# Run Identity service (PowerShell)
$env:ConnectionStrings__DefaultConnection = "Host=127.0.0.1;Port=5433;Database=parking_system;Username=parkwise;Password=parkwise123"
$env:RabbitMq__Host = "127.0.0.1"
$env:RabbitMq__Port = "5672"
$env:RabbitMq__Username = "guest"
$env:RabbitMq__Password = "guest"
$env:RabbitMq__VirtualHost = "/"
$env:ASPNETCORE_URLS = "http://localhost:5001"
dotnet run --project src/Services/Identity/Identity.WebAPI/Identity.WebAPI.csproj
```

### 2.2 Test Account Seeds

Xem: `scripts/database/Seed-IdentityAccounts.sql`

```
Admin:   admin@parkwise.vn / Admin@123456
User:    user@parkwise.vn  / User@123456
```

### 2.3 Base URLs

```
Direct Identity: http://localhost:5001
Via Gateway:     http://localhost:5000
API Prefix:      /api/identity
```

---

## 3. Auth Flow Context

### 3.1 Token Types

| Token | Storage | TTL | Notes |
|---|---|---|---|
| Access Token | JS memory (FE) | 15 min | RS256 JWT, Authorization: Bearer |
| Refresh Token | Body (current) / Cookie (planned) | 30 days | Rotation on each use |

### 3.2 Login Flow

```
POST /api/identity/accounts/login
→ 200: { accessToken, tokenType, expiresIn, refreshToken, account }
→ [Planned] Set-Cookie: sp_refresh=<token>; HttpOnly; SameSite=Strict
→ 401: identity.invalid_credentials
→ 423: identity.account_locked (sau 3 lần fail)
```

---

## 4. Test Cases (hiện tại đã có endpoint)

### TC-AUTH-001: Register → OTP → Login

```http
# Step 1: Register
POST /api/identity/accounts
Content-Type: application/json
{
  "email": "test_{{timestamp}}@example.com",
  "password": "Test@123456"
}
→ 201 Created

# Step 2: Verify email (OTP từ SMTP log hoặc LoggingEmailSender)
POST /api/identity/accounts/verify-email
{
  "email": "test_{{timestamp}}@example.com",
  "otp": "{{otp_from_log}}"
}
→ 200 OK

# Step 3: Login
POST /api/identity/accounts/login
{
  "email": "test_{{timestamp}}@example.com",
  "password": "Test@123456"
}
→ 200: { accessToken, refreshToken, account.status = "EmailVerified" }
```

**Expected:** Whole flow completes without error.

---

### TC-AUTH-002: Login lockout sau 3 lần sai

```http
POST /api/identity/accounts/login × 3
{
  "email": "user@parkwise.vn",
  "password": "WRONG_PASSWORD"
}

POST /api/identity/accounts/login × 1 (attempt #4)
→ 423 Locked
{ "type": "identity.account_locked", "retryAfterSeconds": 900 }
```

---

### TC-AUTH-003: Refresh Token rotation

```http
# First refresh
POST /api/identity/accounts/refresh
{ "refreshToken": "{{token_from_login}}" }
→ 200: { accessToken: NEW, refreshToken: NEW }

# Old token must be invalidated
POST /api/identity/accounts/refresh
{ "refreshToken": "{{old_token}}" }
→ 401: identity.invalid_refresh_token
```

---

### TC-AUTH-004: Resend OTP cooldown

```http
POST /api/identity/accounts/resend-otp
{ "email": "{{unverified_email}}" }
→ 200 OK

POST /api/identity/accounts/resend-otp  # within 60 seconds
{ "email": "{{unverified_email}}" }
→ 429 Too Many Requests
{ "retryAfterSeconds": {{remaining}} }
```

---

### TC-AUTH-005: Forgot password anti-enumeration

```http
POST /api/identity/accounts/forgot-password
{ "email": "nonexistent@example.com" }
→ 200 OK  (always — no enumeration)

POST /api/identity/accounts/forgot-password
{ "email": "user@parkwise.vn" }
→ 200 OK
```

---

### TC-ADMIN-001: Admin change role (existing endpoint)

```http
# Login as admin first
PUT /api/identity/admin/accounts/{{userId}}/role
Authorization: Bearer {{admin_access_token}}
{ "role": "Staff" }
→ 200 OK

# Admin cannot change own role
PUT /api/identity/admin/accounts/{{adminId}}/role
Authorization: Bearer {{admin_access_token}}
→ 403: identity.admin_cannot_change_own_role
```

---

## 5. Test Cases (endpoints CHƯA TỒN TẠI — sẽ fail hiện tại)

> Đây là baseline failures — sau khi implement xong, tests này phải pass.

### TC-PROFILE-001: GET /me (MISSING)

```http
GET /api/identity/accounts/me
Authorization: Bearer {{valid_access_token}}
→ [Expected] 200: { id, email, phoneNumber, fullName, role, status, hasPassword, googleLinked, createdAtUtc }
→ [Actual NOW] 404 Not Found (route không tồn tại)
```

---

### TC-SESSIONS-001: List sessions (MISSING)

```http
GET /api/identity/accounts/sessions
Authorization: Bearer {{valid_access_token}}
→ [Expected] 200: [{ sessionId, deviceInfo, lastUsedAt, isCurrentSession }]
→ [Actual NOW] 404 Not Found
```

---

### TC-SESSIONS-002: Revoke session (MISSING)

```http
DELETE /api/identity/accounts/sessions/{{sessionId}}
Authorization: Bearer {{valid_access_token}}
→ [Expected] 204 No Content
→ [Actual NOW] 404 Not Found
```

---

### TC-ADMIN-002: Admin search accounts (MISSING)

```http
GET /api/identity/admin/accounts?search=nguyen&role=Customer&page=1&pageSize=20
Authorization: Bearer {{admin_access_token}}
→ [Expected] 200: { items: [...], totalCount: N, page: 1, pageSize: 20 }
→ [Actual NOW] 404 Not Found
```

---

### TC-ADMIN-003: Admin suspend account (MISSING)

```http
POST /api/identity/admin/accounts/{{userId}}/suspend
Authorization: Bearer {{admin_access_token}}
→ [Expected] 200 OK
→ [Actual NOW] 404 Not Found

# Safeguard: Admin cannot suspend self (MISSING)
POST /api/identity/admin/accounts/{{adminId}}/suspend
→ [Expected] 403: identity.admin_cannot_suspend_self
```

---

## 6. Coverage Gap Summary

| Endpoint Group | Implemented | Testable Now |
|---|---|---|
| Register/Verify/OTP | ✅ 5 endpoints | ✅ Yes |
| Login/Google/Refresh | ✅ 3 endpoints | ✅ Yes |
| Logout/Change-password | ✅ 3 endpoints | ✅ Yes |
| Admin unlock/role | ✅ 2 endpoints | ✅ Yes |
| **GET /me** | ❌ Missing | ❌ No (will 404) |
| **Sessions CRUD** | ❌ Missing (2) | ❌ No |
| **Admin search/suspend/activate** | ❌ Missing (3) | ❌ No |
| **Cookie refresh** | ❌ Not set | Partial |

**Current test coverage của Identity trên develop: 257 unit tests** (không có integration tests cho missing endpoints).

---

## 7. Chạy tests hiện có

```bash
# Unit tests (không cần Docker)
dotnet test --project tests/Identity/Identity.UnitTests/Identity.UnitTests.csproj

# Integration tests (cần Docker + PostgreSQL + RabbitMQ)
dotnet test --project tests/Identity/Identity.IntegrationTests/Identity.IntegrationTests.csproj

# Architecture tests
dotnet test --project tests/ArchitectureTests/ArchitectureTests.csproj
```

---

## 8. Postman Collection Location

> Nếu có Postman collection cho Identity, đặt vào: `auto-test-demo/postman/` hoặc `tests/Identity/`
