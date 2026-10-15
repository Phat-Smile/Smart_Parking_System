# FE_BE_API_CONTRACT_AUDIT.md
> Kiểm tra đồng bộ giữa API Design Docs, FE Implementation và BE Actual State
> Tính đến: 2026-10-15

---

## 1. Tóm tắt kết quả

| Loại | Số lượng |
|---|---|
| ✅ Match (FE-BE aligned) | 11 |
| ❌ Missing BE endpoint | 7 |
| ⚠️ Partial mismatch | 2 |
| 🔲 Chưa implement (FE chưa dùng) | 3 |

**Mức độ nghiêm trọng:** BE thiếu 7 endpoints mà FE cần hoặc API Design docs đã define — **block trực tiếp UI development.**

---

## 2. Ma trận kiểm tra chi tiết

### 2.1 Identity — Auth & Registration

| # | API Path | Method | API Design | FE Expected | BE Actual | Mismatch | Hành động | Priority |
|---|---|---|---|---|---|---|---|---|
| 1 | `/api/identity/accounts` | POST | ✅ Spec | Gọi `gatewayFetch` | ✅ Implement | None | — | — |
| 2 | `/api/identity/accounts/verify-email` | POST | ✅ Spec | Gọi `gatewayFetch` | ✅ Implement | None | — | — |
| 3 | `/api/identity/accounts/resend-otp` | POST | ✅ Spec | Gọi `gatewayFetch` | ✅ Implement | None | — | — |
| 4 | `/api/identity/accounts/forgot-password` | POST | ✅ Spec | Gọi `gatewayFetch` | ✅ Implement | None | — | — |
| 5 | `/api/identity/accounts/reset-password` | POST | ✅ Spec | Gọi `gatewayFetch` | ✅ Implement | None | — | — |
| 6 | `/api/identity/accounts/login` | POST | ✅ Spec | Gọi `gatewayFetch` + `setAuthSession` | ✅ Implement | **⚠️ Response shape** | BE trả `refreshToken` — FE đọc. Nhưng spec thêm `Set-Cookie: sp_refresh`. BE chưa set cookie. | P1 |
| 7 | `/api/identity/accounts/google` | POST | ✅ Spec | Gọi `gatewayFetch` | ✅ Implement | None | — | — |
| 8 | `/api/identity/accounts/refresh` | POST | ✅ Spec (cookie + body) | FE gửi body `{ refreshToken }` + `credentials: "include"` | ✅ Implement body only | **⚠️ Cookie not read** | BE chỉ đọc body. API spec và cấu trúc FE cho phép cookie. Cookie path mismatch. | P1 |
| 9 | `/api/identity/accounts/logout` | POST | ✅ Spec (cookie clear) | Gọi `gatewayFetch` | ✅ Implement | **⚠️ No cookie clear** | BE không set `Max-Age=0` cho cookie. | P2 |
| 10 | `/api/identity/accounts/logout-all` | POST | ✅ Spec | Gọi `gatewayFetch` | ✅ Implement | None | — | — |
| 11 | `/api/identity/accounts/change-password` | POST | ✅ Spec | Gọi `gatewayFetch` | ✅ Implement | None | — | — |

### 2.2 Identity — Profile & Sessions

| # | API Path | Method | API Design | FE Expected | BE Actual | Mismatch | Hành động | Priority |
|---|---|---|---|---|---|---|---|---|
| 12 | `/api/identity/accounts/me` | GET | ✅ `Identity-Get-Me.md` | FE cần sau login để lấy profile | ❌ **KHÔNG TỒN TẠI** | CRITICAL | Tạo `GetMeQuery` + endpoint | **P0** |
| 13 | `/api/identity/accounts/sessions` | GET | ✅ `Identity-Sessions-Contract.md` | FE list sessions | ❌ **KHÔNG TỒN TẠI** | HIGH | Tạo `ListSessionsQuery` + endpoint | P1 |
| 14 | `/api/identity/accounts/sessions/{sessionId}` | DELETE | ✅ `Identity-Revoke-Session.md` | FE revoke device | ❌ **KHÔNG TỒN TẠI** | HIGH | Tạo `RevokeSessionCommand` + endpoint | P1 |

### 2.3 Identity — Admin

| # | API Path | Method | API Design | FE Expected | BE Actual | Mismatch | Hành động | Priority |
|---|---|---|---|---|---|---|---|---|
| 15 | `/api/identity/admin/accounts/{id}/unlock` | POST | ✅ Spec | Admin unlock | ✅ Implement | None | — | — |
| 16 | `/api/identity/admin/accounts/{id}/role` | PUT | ✅ Spec | Admin change role | ✅ Implement | None | — | — |
| 17 | `/api/identity/admin/accounts` | GET | ✅ `Identity-Admin-Search-Accounts.md` | Admin search | ❌ **KHÔNG TỒN TẠI** | HIGH | `SearchAccountsQuery` + endpoint | P1 |
| 18 | `/api/identity/admin/accounts/{id}/suspend` | POST | ✅ `Identity-Admin-Suspend-Account.md` | Admin suspend | ❌ **KHÔNG TỒN TẠI** | HIGH | `SuspendAccountCommand` + endpoint | P1 |
| 19 | `/api/identity/admin/accounts/{id}/activate` | POST | ✅ `Identity-Admin-Activate-Account.md` | Admin activate | ❌ **KHÔNG TỒN TẠI** | HIGH | `ActivateAccountCommand` + endpoint | P1 |

---

## 3. Chi tiết gap nghiêm trọng nhất

### GAP-01: GET /api/identity/accounts/me (P0)

**Vấn đề:** API Design (`Identity-Get-Me.md`) định nghĩa endpoint này trả về:
```json
{
  "id": "uuid",
  "email": "user@example.com",
  "phoneNumber": null,
  "fullName": "Nguyễn Văn A",
  "role": "Customer",
  "status": "EmailVerified",
  "hasPassword": true,
  "googleLinked": false,
  "createdAtUtc": "2026-10-01T00:00:00Z"
}
```

**BE thực tế:**
- Endpoint **không tồn tại** trong `AccountEndpoints.cs`
- `Profile` entity tồn tại trong DB (`identity.profiles` schema, migration done)
- Không có `GetMeQuery` handler

**FE tác động:**
- `loginParkWiseAccount.ts` trả về minimal `{ id, email, phoneNumber, status, role }` — không có `fullName`
- `authSession.ts` lưu account nhưng không có profile
- Màn hình Customer Profile cần data này

**Tests cần:**
- `GetMeQuery` unit test — returns profile, handles missing profile gracefully
- Integration test — returns 401 without JWT

---

### GAP-02: RefreshToken Cookie (P1)

**Vấn đề:**
- API Design spec: Login `Set-Cookie: sp_refresh=<token>; HttpOnly; SameSite=Strict; Path=/api/identity/accounts`
- Refresh endpoint nhận từ `Cookie: sp_refresh` (body là optional cho mobile)
- Logout clear cookie với `Max-Age=0`

**FE thực tế:**
- `authSession.ts` gửi `{ refreshToken: token }` trong body (token lấy từ JS memory `activeSession.refreshToken`)
- Sử dụng `credentials: "include"` — cookie ĐƯỢC gửi nếu server set
- Hybrid approach: FE vừa dùng memory vừa `credentials: include`

**BE thực tế:**
- `RefreshTokenCommand(string RefreshToken)` — body only, validator `NotEmpty`
- `LoginCommand` response không set cookie
- `Program.cs` không có `AddCookie` hay CORS để hỗ trợ cookie

**Tác động:**
- Cross-tab refresh sẽ fail (tab mới không có JS memory)
- Sau khi trang reload, FE mất session vì không có cookie fallback

---

### GAP-03: Sessions Management (P1)

| Endpoint | Status |
|---|---|
| `GET /api/identity/accounts/sessions` | ❌ Missing |
| `DELETE /api/identity/accounts/sessions/{sessionId}` | ❌ Missing |

`AuthSession` entity tồn tại trong domain, `identity.auth_sessions` table có migration. Query handler và endpoint **chưa viết**.

---

### GAP-04: Admin Endpoints (P1)

| Endpoint | Doc file | Status |
|---|---|---|
| `GET /api/identity/admin/accounts` | `Identity-Admin-Search-Accounts.md` | ❌ Missing |
| `POST /api/identity/admin/accounts/{id}/suspend` | `Identity-Admin-Suspend-Account.md` | ❌ Missing |
| `POST /api/identity/admin/accounts/{id}/activate` | `Identity-Admin-Activate-Account.md` | ❌ Missing |

---

## 4. Response Shape Mismatch (LoginResponse)

**FE định nghĩa** (`loginParkWiseAccount.ts:5-16`):
```typescript
interface LoginResponse {
  accessToken: string;
  tokenType: "Bearer";
  expiresIn: number;
  refreshToken: string;
  account: {
    id: string;
    email: string;
    phoneNumber: string | null;
    status: "EmailVerified" | "Active" | "Suspended" | "Disabled";
    role: "Customer" | "Staff" | "ParkingLotOwner" | "SystemAdmin";
  };
}
```

**Thiếu trong FE `account`:** `fullName`, `hasPassword`, `googleLinked` — những field này chỉ có từ `GET /me`. **FE cần gọi `/me` sau login** để populate profile.

---

## 5. Thứ tự fix đề xuất

```
P0 (Block ngay):
  1. GET /api/identity/accounts/me — FE không build được profile page

P1 (Sprint hiện tại):
  2. RefreshToken cookie — Set-Cookie khi login, đọc từ cookie khi refresh, clear khi logout
  3. GET /api/identity/accounts/sessions
  4. DELETE /api/identity/accounts/sessions/{sessionId}
  5. GET /api/identity/admin/accounts (search)
  6. POST /api/identity/admin/accounts/{id}/suspend
  7. POST /api/identity/admin/accounts/{id}/activate

P2 (Sprint sau):
  8. Profile CRUD (edit name/phone/DOB)
  9. Vehicle CRUD REST endpoints
```
