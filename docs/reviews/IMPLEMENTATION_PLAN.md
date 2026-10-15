# IMPLEMENTATION_PLAN.md
> Kế hoạch triển khai cho BE-1 (Lộc) — Sprint hiện tại
> Tính đến 2026-10-15

---

## Scope của plan này

Plan chỉ bao gồm tasks của **BE-1 Lộc** trong Identity Service — không bao gồm work của các thành viên khác.

**CẦN APPROVED trước khi bắt đầu implement.**

---

## Nhóm A — Unblock FE ngay (P0 — implement trước tiên)

### A-1. `GET /api/identity/accounts/me`

**Mô tả:** Trả về thông tin account + profile của user đang đăng nhập.

**Layers cần thêm:**

```
Identity.Application/
└── Accounts/
    └── GetMe/
        ├── GetMeQuery.cs              (record GetMeQuery : IRequest<GetMeResult>)
        ├── GetMeQueryHandler.cs       (inject IIdentityDbContext, HttpContext via ICurrentUser)
        └── GetMeResult.cs             (record với id, email, phoneNumber, fullName, role, status, hasPassword, googleLinked, createdAtUtc)

Identity.WebAPI/
└── AccountEndpoints.cs               (thêm 1 GET route: .MapGet("/me", ...).RequireAuthorization())
```

**Logic handler:**
1. Lấy `accountId` từ JWT claim
2. Query `accounts` LEFT JOIN `profiles` by `account_id`
3. Map sang `GetMeResult` — `hasPassword = account.PasswordHash != null`, `googleLinked = account.GoogleId != null`
4. Return 200 OK hoặc 404 nếu account không tồn tại

**Tests cần:**
- `GetMeQueryHandlerTests.cs`
  - `Returns_profile_for_authenticated_user` — happy path
  - `Returns_null_fullName_when_profile_not_set` — account chưa complete profile
  - `Returns_hasPassword_false_for_google_only_account`
  - `Returns_googleLinked_true_when_google_id_present`
- Integration: `GET /me` returns 401 without JWT

**Thời gian ước tính:** 2–3h

---

### A-2. Refresh Token Cookie (Set-Cookie khi login)

**Mô tả:** Khi login thành công, server set `sp_refresh` cookie HttpOnly.

**Files cần sửa:**

```
Identity.WebAPI/
├── AccountEndpoints.cs       — Login route: sau khi gọi handler, set cookie
└── Program.cs                — Thêm cookie policy config (nếu cần SameSite=Strict)

Identity.Application/
└── Accounts/Login/
    └── LoginCommandHandler.cs — (không đổi logic, chỉ handler trả data, endpoint set cookie)
```

**Cookie spec:**
```http
Set-Cookie: sp_refresh=<token>; HttpOnly; SameSite=Strict; Path=/api/identity/accounts; Max-Age=2592000
```

**Refresh endpoint:** đọc từ cookie trước, body làm fallback:
```csharp
// AccountEndpoints.cs — refresh route
var tokenFromCookie = ctx.Request.Cookies["sp_refresh"];
var tokenFromBody = req?.RefreshToken;
var token = tokenFromCookie ?? tokenFromBody;
```

**Logout:** clear cookie với `Max-Age=0`.

**Thời gian ước tính:** 2h

---

## Nhóm B — Sessions Management (P1)

### B-1. `GET /api/identity/accounts/sessions`

**Mô tả:** List các AuthSession đang active của user (devices đang đăng nhập).

**Layers cần thêm:**

```
Identity.Application/
└── Accounts/
    └── ListSessions/
        ├── ListSessionsQuery.cs
        ├── ListSessionsQueryHandler.cs
        └── SessionDto.cs  (sessionId, deviceInfo, lastUsedAt, createdAt, isCurrentSession)
```

**Logic:** Query `auth_sessions` WHERE `account_id = currentUser AND revoked_at IS NULL`, mark `isCurrentSession` bằng cách so `jti` claim.

**Tests:** Unit test — returns active sessions only, marks current session.

**Thời gian:** 1.5h

---

### B-2. `DELETE /api/identity/accounts/sessions/{sessionId}`

**Mô tả:** Revoke một AuthSession cụ thể (đăng xuất thiết bị khác).

```
Identity.Application/
└── Accounts/
    └── RevokeSession/
        ├── RevokeSessionCommand.cs    (sessionId, accountId)
        ├── RevokeSessionCommandHandler.cs
        └── RevokeSessionCommandValidator.cs (sessionId not empty)
```

**Logic:** Load session, verify `account_id` match (không revoke session người khác), set `revoked_at = now`.

**Tests:** 
- Happy path — session revoked
- `SessionBelongsToAnotherUser_Returns_NotFound`
- `SessionAlreadyRevoked_Returns_NotFound`

**Thời gian:** 1.5h

---

## Nhóm C — Admin Endpoints (P1)

### C-1. `GET /api/identity/admin/accounts` — Search accounts

**Mô tả:** Paginated search với filter by keyword/role/status.

```
Identity.Application/
└── Admin/
    └── SearchAccounts/
        ├── SearchAccountsQuery.cs       (search, role?, status?, page, pageSize)
        ├── SearchAccountsQueryHandler.cs
        └── SearchAccountsResult.cs      (items, totalCount, page, pageSize)
```

**Logic:** `IQueryable<Account>` với dynamic filters, LEFT JOIN profiles, `Skip/Take` paging.

**Tests:**
- Filter by keyword (email, fullName, phone)
- Filter by role
- Filter by status
- Pagination boundaries (page 1, last page, empty)

**Thời gian:** 3h

---

### C-2. `POST /api/identity/admin/accounts/{id}/suspend`

**Mô tả:** Suspend account — revoke all sessions, block login.

```
Identity.Application/
└── Admin/
    └── SuspendAccount/
        ├── SuspendAccountCommand.cs           (accountId, adminAccountId)
        ├── SuspendAccountCommandHandler.cs
        └── SuspendAccountCommandValidator.cs
```

**Business rules:**
- Admin không suspend chính mình
- Admin không suspend Admin khác
- Account phải là `EmailVerified` hoặc `Active` (không suspend đã `Disabled`)
- Sau suspend: revoke tất cả `AuthSession` của account đó

**Invariants (từ API doc):**
- Error `identity.admin_cannot_suspend_self`
- Error `identity.cannot_suspend_admin`

**Thời gian:** 2.5h

---

### C-3. `POST /api/identity/admin/accounts/{id}/activate`

**Mô tả:** Kích hoạt lại account đang Suspended về EmailVerified.

```
Identity.Application/
└── Admin/
    └── ActivateAccount/
        ├── ActivateAccountCommand.cs
        └── ActivateAccountCommandHandler.cs
```

**Thời gian:** 1.5h

---

## Thứ tự thực hiện đề xuất

```
Day 1:
  A-1. GET /me                           (2h implementation + 1h test)
  A-2. Cookie refresh                    (2h implementation + 0.5h test)

Day 2:
  B-1. List sessions                     (1.5h)
  B-2. Revoke session                    (1.5h)
  C-2. Admin Suspend account             (2.5h)
  C-3. Admin Activate account            (1.5h)

Day 3:
  C-1. Admin Search accounts             (3h)
  Regression tests + integration tests   (2h)
```

**Tổng ước tính:** ~18h (3 ngày làm việc)

---

## Migration cần tạo không?

| Task | Migration cần? |
|---|---|
| GET /me | ❌ Không — `profiles` table đã tồn tại |
| Cookie refresh | ❌ Không — không thay đổi DB |
| List sessions | ❌ Không — `auth_sessions` table đã tồn tại |
| Revoke session | ❌ Không — chỉ update `revoked_at` field |
| Admin search | ❌ Không |
| Admin suspend | ❌ Không |
| Admin activate | ❌ Không |

**Không cần migration mới cho tất cả tasks nhóm A-C.**

---

## Architecture test cần cập nhật không?

| Task | Arch test update cần? |
|---|---|
| Tất cả tasks trên | ❌ Không — không thêm consumer (`IConsumer<`), không cross-service reference |

---

## Điểm chú ý

1. **Cookie SameSite vs CORS:** Nếu FE chạy trên domain khác với Gateway, `SameSite=Strict` sẽ block cookie cross-site. Cần confirm dev environment setup (FE port vs Gateway port).

2. **RefreshToken body fallback:** Giữ body fallback để mobile clients (không dùng browser cookie) vẫn hoạt động được.

3. **`isCurrentSession` detection:** Cần lấy `jti` từ JWT của request hiện tại và so với `auth_sessions.token_id`.
