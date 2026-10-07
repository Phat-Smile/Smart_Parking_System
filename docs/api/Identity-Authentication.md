# Identity & Authentication API Specification

## Overview

Tài liệu đặc tả hợp đồng API cho toàn bộ các luồng Định danh & Xác thực (Identity & Authentication) của Smart Parking Management System.

Truy vết yêu cầu:
- **User Stories**:
  - `US-AUTH-001` — Đăng ký tài khoản khách hàng (`FR-USER-001`)
  - `US-AUTH-003` — Xác thực email bằng mã OTP & Gửi lại OTP (`FR-USER-001`, `NFR-SEC-004`)
  - `US-AUTH-004` — Đăng nhập bằng Email và Mật khẩu (`FR-USER-002`, `FR-USER-008`, `NFR-SEC-001`, `NFR-SEC-002`)
  - `US-AUTH-005` — Quản lý phiên, Refresh Token Rotation & Đăng xuất (`NFR-SEC-002`, `NFR-SEC-004`)
- **Main Flows**: `MF-01`, `MF-02`
- **Module Owner**: Identity Service / BE-1

---

## Danh mục Endpoints

| HTTP Method | Endpoint URL | Phân quyền (Permission) | Mô tả tóm tắt |
| :--- | :--- | :--- | :--- |
| **POST** | `/api/identity/accounts` | Public / Anonymous | Đăng ký tài khoản khách hàng mới |
| **POST** | `/api/identity/accounts/verify-email` | Public / Anonymous | Xác thực tài khoản qua mã OTP 6 số |
| **POST** | `/api/identity/accounts/resend-otp` | Public / Anonymous | Gửi lại mã OTP qua email |
| **POST** | `/api/identity/accounts/login` | Public / Anonymous | Đăng nhập, khởi tạo AuthSession và cấp JWT RS256 |
| **POST** | `/api/identity/accounts/refresh` | Public / Anonymous | Refresh Access Token & Xoay vòng Refresh Token |
| **POST** | `/api/identity/accounts/logout` | Public (kèm RefreshToken) | Đăng xuất phiên làm việc hiện tại |
| **POST** | `/api/identity/accounts/logout-all` | Bearer JWT (Authenticated) | Đăng xuất toàn bộ các phiên trên mọi thiết bị |

---

## 1. POST /api/identity/accounts (Đăng ký tài khoản)

### Request sample
```http
POST /api/identity/accounts
Content-Type: application/json

{
  "fullName": "Nguyễn Văn An",
  "email": "customer@smartparking.io",
  "password": "SecurePassword@123",
  "phoneNumber": "0901234567"
}
```

| Field | Description | Data Type | Nullable | Validation Rules |
| :--- | :--- | :---: | :---: | :--- |
| `fullName` | Họ và tên người dùng | string | NOT NULL | Bắt buộc, 2–100 ký tự, regex `^[\p{L}]+(?:[ '-][\p{L}]+)*$` |
| `email` | Địa chỉ email đăng ký | string | NOT NULL | Bắt buộc, chuẩn RFC 5322, tối đa 254 ký tự, tự động normalize chữ thường |
| `password` | Mật khẩu tài khoản | string | NOT NULL | Bắt buộc, 8–72 ký tự, 1 hoa, 1 thường, 1 số, 1 ký tự đặc biệt |
| `phoneNumber` | Số điện thoại di động | string | NULL | Tùy chọn, đúng chuẩn di động Việt Nam (0xxxxxxxxx hoặc +84xxxxxxxxx) |

> **Lưu ý**: Database EF Core quy định trường `date_of_birth` trong bảng `profiles` là **NULLABLE**, không yêu cầu nhập khi đăng ký.

### Response sample (201 Created — Đăng ký mới)
```json
{
  "accountId": "7b92df78-c09a-4122-87f5-9610f4439c27",
  "email": "customer@smartparking.io",
  "fullName": "Nguyễn Văn An",
  "status": "PendingEmailVerification",
  "isNewAccount": true
}
```

### Response sample (200 OK — Phục hồi tài khoản chờ kích hoạt / Dormant Recovery)
```json
{
  "accountId": "7b92df78-c09a-4122-87f5-9610f4439c27",
  "email": "customer@smartparking.io",
  "fullName": "Nguyễn Văn An",
  "status": "PendingEmailVerification",
  "isNewAccount": false
}
```

### Mã phản hồi (Status Codes)
- `201 Created`: Tạo tài khoản mới thành công (`isNewAccount: true`), hệ thống phát hành OTP ban đầu và ghi Outbox.
- `200 OK`: Phục hồi tài khoản `PendingEmailVerification` đã đăng ký trước đó (`isNewAccount: false`), không cấp lại OTP (người dùng dùng `/resend-otp` nếu mã hết hạn).
- `400 Bad Request`: Lỗi dữ liệu đầu vào (RFC 7807 ValidationProblem).
- `409 Conflict`: Email đã tồn tại và đã kích hoạt (`identity.email_already_registered`).

> **Bảo vệ Concurrency & Xử lý 23505 (R-01 Invariant)**: Chỉ mục duy nhất `ux_accounts_email` trên bảng `identity.accounts` đảm bảo khi 2 yêu cầu đăng ký cùng email gửi đồng thời, chỉ duy nhất 1 yêu cầu thực sự INSERT Account (TC-01). Yêu cầu thứ hai vi phạm khóa duy nhất (PostgreSQL `23505`) được `TrySaveChangesAsync` bắt và tiến hành re-query trạng thái thực tế của email. Nếu tài khoản vừa tạo đang ở trạng thái `PendingEmailVerification`, hệ thống trả về `200 OK` (Dormant Recovery, `isNewAccount: false`). Nếu tài khoản đã được xác thực, hệ thống trả về `409 Conflict`. Điều này đảm bảo tính nhất quán ngữ nghĩa kinh doanh: trạng thái dữ liệu quyết định phản hồi, không phụ thuộc vào độ lệch timing vài mili-giây của hai luồng đồng thời, và tuyệt đối không bao giờ rơi thành HTTP 500.

---

## 2. POST /api/identity/accounts/verify-email (Xác thực OTP)

### Request sample
```http
POST /api/identity/accounts/verify-email
Content-Type: application/json

{
  "email": "customer@smartparking.io",
  "otpCode": "849201"
}
```

### Response sample (200 OK)
```json
{
  "accountId": "7b92df78-c09a-4122-87f5-9610f4439c27",
  "email": "customer@smartparking.io",
  "status": "EmailVerified",
  "isVerified": true
}
```

### Quy tắc & Mã phản hồi
- Áp dụng khóa dòng PostgreSQL `SELECT ... FOR UPDATE` trên bảng `accounts`.
- Tối đa **3 lần thử** sai (`AttemptCount < 3`).
- **Cam kết Lưu vết (Persistence Commitment)**:
  - Khi nhập sai lần 1 hoặc 2: `AttemptCount++` được **commit vào DB** trước khi trả `400 Bad Request` (`identity.invalid_otp_code`).
  - Khi nhập sai lần 3: `AttemptCount=3` và mã OTP bị vô hiệu hóa (`InvalidatedAtUtc = Now`) được **commit vào DB** trước khi trả `400 Bad Request` (`identity.otp_max_attempts_exceeded`).
  - Mã lỗi 400 tuyệt đối không làm rollback số lần thử trong database.
- Chống Anti-Enumeration: Email không tồn tại hoặc đã kích hoạt trả về generic `400 identity.otp_expired`.
- `200 OK`: Xác thực thành công, cập nhật trạng thái `EmailVerified`, ghi sự kiện Outbox.
- `400 Bad Request`: `identity.invalid_otp_code`, `identity.otp_expired`, `identity.otp_max_attempts_exceeded`.

---

## 3. POST /api/identity/accounts/resend-otp (Gửi lại OTP)

### Request sample
```http
POST /api/identity/accounts/resend-otp
Content-Type: application/json

{
  "email": "customer@smartparking.io"
}
```

### Response sample (200 OK — Generic Contract)
```json
{
  "email": "customer@smartparking.io",
  "expiresInSeconds": 300,
  "cooldownSeconds": 60,
  "message": "A new verification code has been sent to your email."
}
```

### Quy tắc & Mã phản hồi
- **Hạn mức**: Tối đa **5 mã OTP / giờ** (`MaxOtpIssuedPerHour = 5`), tính cả mã lúc Đăng ký.
- **Cooldown 60s [PROPOSED]**: Bắt buộc cách nhau tối thiểu 60 giây (`CooldownSeconds = 60`).
- **Vô hiệu hóa OTP cũ**: Khi Resend thành công, mọi OTP active trước đó của tài khoản đều được đánh dấu `InvalidatedAtUtc = Now`. Chỉ duy nhất mã OTP mới nhất có hiệu lực.
- **Anti-Enumeration & Đánh đổi UX (R-02 Option B)**: Cả 3 trường hợp (Unknown email, Already verified, Pending verification) đều trả về `200 OK` generic mặc định để chống dò quét tài khoản; chỉ Pending mới thực sự sinh mã OTP, vô hiệu OTP cũ và ghi Outbox (TC-07). Tuy nhiên, đối với tài khoản `Pending` đang trong thời gian Cooldown 60s hoặc vượt quá 5 OTP/giờ, hệ thống trả về `429 Too Many Requests` để cung cấp phản hồi chính xác và đếm ngược thời gian chờ cho Frontend; đây là đánh đổi có chủ đích giữa UX và strict indistinguishability.
- `200 OK`: Gửi lại thành công (hoặc generic success chống dò quét).
- `429 Too Many Requests`: `identity.resend_cooldown_active`, `identity.resend_rate_limit_exceeded`.

---

## 4. POST /api/identity/accounts/login (Đăng nhập)

### Request sample
```http
POST /api/identity/accounts/login
Content-Type: application/json

{
  "email": "customer@smartparking.io",
  "password": "SecurePassword@123"
}
```

### Response sample (200 OK)
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "refreshToken": "4a7b9c1d2e3f4a5b6c7d8e9f0a1b2c3d...",
  "account": {
    "id": "7b92df78-c09a-4122-87f5-9610f4439c27",
    "email": "customer@smartparking.io",
    "phoneNumber": "+84901234567",
    "status": "EmailVerified",
    "role": "Customer"
  }
}
```

### Quy tắc & Mã phản hồi
- **JWT Contract**: Ký số RS256, claims bắt buộc: `sub`, `email`, `role`, `jti`, `exp`, `iss`, `aud`.
- **TTL Access Token**: Cấu hình qua `Security:Jwt:ExpirationMinutes` (mặc định 15 phút = 900s, `[CURRENT DEFAULT / OPEN DECISION 15m vs 24h]`), đồng bộ với US-AUTH-005.
- **Quy chuẩn Khóa tài khoản & Xử lý sai mật khẩu (FR-USER-008 & AUTH-02)**:
  - Sai lần 1: `401 Unauthorized` (`identity.invalid_credentials`).
  - Sai lần 2: `401 Unauthorized` (`identity.invalid_credentials`).
  - Sai lần 3: Tự động khóa tài khoản 15 phút (`LockoutEndUtc = Now + 15m`), lưu DB và lập tức trả về `423 Locked` (`identity.account_temporarily_locked`) (TC-05).
  - Đang trong thời gian khóa 15m: `423 Locked`.
  - Hết 15m: Tự động xóa khóa, cho phép thử lại.
  - **Chuỗi đếm sai mật khẩu liên tiếp (R-05)**: Nhập đúng mật khẩu lập tức phá vỡ chuỗi sai và reset `FailedLoginAttempts = 0`, xóa `LockoutEndUtc`. Nếu tài khoản đang ở trạng thái `PendingEmailVerification`, lệnh reset này được commit vào DB trước khi trả về `403 Forbidden`.
- **Phòng thủ Timing Attack**: Chạy dummy BCrypt verify khi email không tồn tại.
- **Concurrency Row Lock**: Thực thi `SELECT Account FOR UPDATE` kết hợp `BCrypt.Verify()` bên trong transaction bảo vệ an toàn `FailedLoginAttempts`.
- **Anti-Enumeration đối với Trạng thái (R-04 & R-05)**: Xác thực mật khẩu qua BCrypt trước; chỉ khi mật khẩu đúng nhưng tài khoản chưa kích hoạt mới trả về `403 Forbidden (identity.email_not_verified)`. Việc tiết lộ trạng thái tạm khóa qua `423 Locked` là đánh đổi an ninh có chủ đích (Security Assumption) để đáp ứng UX thông báo người dùng theo FR-USER-008.
- `200 OK`: Đăng nhập thành công, khởi tạo `AuthSession` và cấp token.
- `401 Unauthorized`: Sai email hoặc mật khẩu (lần 1, 2) (`identity.invalid_credentials`).
- `403 Forbidden`: `identity.email_not_verified`, `identity.account_suspended` [PROPOSED], `identity.account_disabled` [PROPOSED].
- `423 Locked`: Tài khoản bị tạm khóa 15 phút (kích hoạt từ lần sai thứ 3).

---

## 5. POST /api/identity/accounts/refresh (Refresh Token Rotation)

### Request sample
```http
POST /api/identity/accounts/refresh
Content-Type: application/json

{
  "refreshToken": "4a7b9c1d2e3f4a5b6c7d8e9f0a1b2c3d..."
}
```
> **Validation Constraint**: Trường `refreshToken` bắt buộc, chuỗi không rỗng, tối đa 512 ký tự (vi phạm trả về `400 Bad Request` `validation_error`).

### Response sample (200 OK - RefreshTokenResponse)
```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "refreshToken": "9f8e7d6c5b4a3a2b1c0d9e8f7a6b5c4d...",
  "account": {
    "id": "7b92df78-c09a-4122-87f5-9610f4439c27",
    "email": "customer@smartparking.io",
    "phoneNumber": "+84901234567",
    "status": "EmailVerified",
    "role": "Customer"
  }
}
```

### Quy tắc xử lý & Bảo mật
- **Lookup Token bao quát (A005-01)**: Query `FindByHashAsync(tokenHash)` tìm kiếm cả token active lẫn token đã bị thu hồi/rotate. Tuyệt đối không lọc `revoked_at_utc IS NULL` để đảm bảo cơ chế Token Reuse Detection không bị vô hiệu hóa.
- **Khóa dòng bi quan & Reload sau Khóa (A005-02)**: Lấy khóa bi quan `SELECT * FROM "identity"."auth_sessions" WHERE "id" = @sessionId FOR UPDATE`. Ngay sau khi lấy khóa, gọi `refreshTokens.ReloadAsync(token)` để nạp lại trạng thái token mới nhất từ DB trong transaction, loại bỏ stale entity state khi có 2 refresh requests cùng token chạy đồng thời.
- **Strict Single-Use Rotation (Bỏ Grace Window cho OJT)**: Mỗi refresh token chỉ được sử dụng thành công 1 lần. Khi token đã rotate/thu hồi được gửi lại, hệ thống lập tức kích hoạt Token Reuse Detection $\rightarrow$ thu hồi ngay lập tức chính `AuthSession` bị xâm phạm (`ReuseDetected`) và các token con $\rightarrow$ commit DB $\rightarrow$ trả về `401 Unauthorized` (`identity.refresh_token_reused`).
- **Frontend Single-Flight**: Client bắt buộc triển khai mutex/single-flight refresh để không gửi đồng thời nhiều refresh request.
- **Account Status Validation [PROPOSED] (A005-03)**: Kiểm tra trạng thái tài khoản. Nếu `Suspended` [PROPOSED] $\rightarrow$ 403 `identity.account_suspended`; nếu `Disabled` [PROPOSED] $\rightarrow$ 403 `identity.account_disabled`; nếu chưa xác thực email $\rightarrow$ 403 `identity.email_not_verified`. Tất cả trường hợp này đều thu hồi session và active tokens trong DB trước khi trả lỗi.

---

## 6. POST /api/identity/accounts/logout & logout-all (Đăng xuất)

### POST /api/identity/accounts/logout
```http
POST /api/identity/accounts/logout
Content-Type: application/json

{
  "refreshToken": "4a7b9c1d2e3f4a5b6c7d8e9f0a1b2c3d..."
}
```
- **Validation Contract (A005-05)**: Nếu `refreshToken` bị thiếu, rỗng hoặc vượt quá 512 ký tự $\rightarrow$ trả về `400 Bad Request` (`validation_error`).
- **Generic 200 OK & Idempotent**: Khi token có format hợp lệ, hệ thống luôn trả về `200 OK` (`{"message": "Logged out successfully."}`) cho mọi trường hợp (token active hợp lệ, token đã thu hồi trước đó, token không tồn tại hoặc hết hạn) nhằm đảm bảo tính lũy kế và chống rò rỉ trạng thái token (Anti-Enumeration).
- **Phạm vi thu hồi**: Khóa dòng `AuthSession`, thu hồi `AuthSession` của thiết bị hiện tại (`session.Revoke("Logout")`) và toàn bộ refresh token active thuộc session.
- **Ngữ nghĩa Access Token sau Logout**: JWT Access Token là self-contained (stateless). Việc Logout ngăn chặn cấp token mới; Access Token hiện tại đã cấp vẫn có hiệu lực cục bộ cho đến khi hết hạn TTL (15 phút). Phạm vi OJT không dùng JWT blacklist phân tán, kiểm soát rủi ro thông qua thời hạn token ngắn (15 phút / 900 giây).

### POST /api/identity/accounts/logout-all
```http
POST /api/identity/accounts/logout-all
Authorization: Bearer <accessToken>
```
- **Xác thực & Chống IDOR**: Yêu cầu Bearer JWT hợp lệ. Định danh `AccountId` được trích xuất trực tiếp từ claim `JWT.sub` (`currentUser.SubjectId`), tuyệt đối không nhận từ body hay query.
- **Thu hồi đồng loạt**: Chấm dứt toàn bộ các phiên `AuthSession` và toàn bộ các `RefreshToken` đang hoạt động của người dùng trên mọi thiết bị (`RevokeAllSessionsAsync` với lý do `"LogoutAll"`).
- **Tuần tự hóa Concurrency (A005-06)**: Sau khi `logout-all` commit, không được tồn tại bất kỳ active refresh token hay auth session nào của account sinh ra từ một request refresh bắt đầu trước hoặc đồng thời với `logout-all`.
- **Phản hồi**: `200 OK` (`{"message": "All active sessions have been logged out successfully."}`).
