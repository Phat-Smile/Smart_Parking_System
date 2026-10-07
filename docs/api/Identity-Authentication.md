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
| `email` | Địa chỉ email đăng ký | string | NOT NULL | Bắt buộc, chuẩn RFC 5322, tối đa 254 ký tự, tự động lower |
| `password` | Mật khẩu tài khoản | string | NOT NULL | Bắt buộc, 8–72 ký tự, 1 hoa, 1 thường, 1 số, 1 ký tự đặc biệt |
| `phoneNumber` | Số điện thoại di động | string | NULL | Tùy chọn, đúng chuẩn di động Việt Nam |

> **Lưu ý**: Database EF Core quy định trường `date_of_birth` trong bảng `profiles` là **NULLABLE**, không yêu cầu nhập khi đăng ký.

### Response sample (201 Created)
```json
{
  "accountId": "7b92df78-c09a-4122-87f5-9610f4439c27",
  "email": "customer@smartparking.io",
  "fullName": "Nguyễn Văn An",
  "status": "PendingEmailVerification"
}
```

### Mã phản hồi (Status Codes)
- `201 Created`: Tạo tài khoản mới thành công (`isNewAccount: true`), hệ thống phát hành OTP ban đầu và ghi Outbox.
- `200 OK`: Phục hồi tài khoản `PendingEmailVerification` đã đăng ký trước đó (`isNewAccount: false`), không cấp lại OTP (người dùng dùng `/resend-otp` nếu mã hết hạn).
- `400 Bad Request`: Lỗi dữ liệu đầu vào (RFC 7807 ValidationProblem).
- `409 Conflict`: Email đã tồn tại và đã kích hoạt (`identity.email_already_registered`).

> **Bảo vệ Concurrency**: Chỉ mục duy nhất `ux_accounts_email` trên bảng `identity.accounts` đảm bảo khi 2 yêu cầu đăng ký cùng email gửi đồng thời, chỉ duy nhất 1 yêu cầu tạo thành công Account (TC-01), yêu cầu còn lại được bắt lỗi tại tầng ORM/DB và trả về `409 Conflict`.

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
- Tối đa **3 lần thử** sai. Lần sai thứ 3 sẽ vô hiệu hóa OTP lập tức.
- Chống Anti-Enumeration: Email không tồn tại/đã kích hoạt trả về `400 identity.otp_expired`.
- `200 OK`: Xác thực thành công.
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

### Response sample (200 OK)
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
- **Cooldown**: Bắt buộc cách nhau tối thiểu 60 giây (`CooldownSeconds = 60`).
- **Chống Anti-Enumeration**: Cả 3 trường hợp (Unknown email, Already verified, Pending verification) đều trả về `200 OK` generic với response giống nhau từ phía Client. Chỉ Pending mới thực sự sinh mã OTP và ghi Outbox (TC-07).
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
- **TTL Access Token**: Cấu hình qua `Security:Jwt:ExpirationMinutes` (mặc định 15 phút, `[OPEN DECISION]` 15m vs 24h).
- **Quy chuẩn Khóa tài khoản & Xử lý sai mật khẩu (FR-USER-008 & AUTH-02)**:
  - Sai lần 1: `401 Unauthorized` (`identity.invalid_credentials`).
  - Sai lần 2: `401 Unauthorized` (`identity.invalid_credentials`).
  - Sai lần 3: Tự động khóa tài khoản 15 phút (`LockoutEndUtc = Now + 15m`) và lập tức trả về `423 Locked` (`identity.account_temporarily_locked`) (TC-05).
  - Đang trong thời gian khóa 15m: `423 Locked`.
  - Hết 15m: Tự động xóa khóa, cho phép thử lại.
  - Đăng nhập đúng: Reset `FailedLoginAttempts = 0`, xóa `LockoutEndUtc`.
- **Phòng thủ Timing Attack**: Chạy dummy BCrypt verify khi email không tồn tại.
- **[DESIGN RECOMMENDATION] Concurrency Row Lock**: Sử dụng `SELECT Account FOR UPDATE` kết hợp `BCrypt.Verify()` bên trong transaction bảo vệ an toàn `FailedLoginAttempts`.
- `200 OK`: Đăng nhập thành công, khởi tạo `AuthSession` và cấp token.
- `401 Unauthorized`: Sai email hoặc mật khẩu (lần 1, 2) (`identity.invalid_credentials`).
- `403 Forbidden`: `identity.email_not_verified`, `identity.account_suspended`, `identity.account_disabled`.
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

### Response sample (200 OK)
Trả về cấu trúc tương tự Login (`accessToken`, `tokenType`, `expiresIn`, `refreshToken` mới, `account`).

### Quy tắc xử lý
- Khóa dòng bi quan trên `AuthSession` (`SELECT ... FOR UPDATE`).
- Phát hiện tái sử dụng Token (Token Reuse Detection): Nếu token cũ đã bị thu hồi ngoài khoảng Grace Period (10s) được gửi lại, hệ thống lập tức thu hồi toàn bộ phiên làm việc `AuthSession` và tất cả refresh token thuộc phiên đó.

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
- Thu hồi phiên làm việc `AuthSession` của thiết bị hiện tại và đánh dấu revoke toàn bộ token thuộc session.
- Phản hồi: `200 OK` (`{"message": "Logged out successfully."}`). Có tính chất Idempotent.

### POST /api/identity/accounts/logout-all
```http
POST /api/identity/accounts/logout-all
Authorization: Bearer <accessToken>
```
- Yêu cầu xác thực Bearer JWT.
- Thu hồi đồng loạt toàn bộ các phiên `AuthSession` đang hoạt động của người dùng trên mọi thiết bị.
- Phản hồi: `200 OK` (`{"message": "All active sessions have been logged out successfully."}`).
