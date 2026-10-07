# Identity & Authentication — Architecture Overview & API Index

## 1. Giới thiệu & Định vị Tài liệu (Architectural Positioning)

Tài liệu này đóng vai trò là **Chỉ mục Kiến trúc (Index & Overview)** cho toàn bộ phân hệ Định danh & Xác thực (Identity & Authentication) của nền tảng Smart Parking Management System. 

> [!IMPORTANT]
> **Quy Chuẩn Nguồn Sự Thật Duy Nhất (Single Source of Truth - SSOT)**:
> Mọi chi tiết kỹ thuật chuyên sâu, quy tắc validation từng trường, bảng mã lỗi chi tiết và sơ đồ PlantUML của từng tính năng được duy trì độc quyền tại các tài liệu đặc tả chuẩn hóa sau:
> 1. [US-AUTH-001: Đăng ký tài khoản khách hàng](../../.notes/us-auth-001-account-registration-api-design-spec.md)
> 2. [US-AUTH-003: Xác thực Email OTP & Gửi lại mã](../../.notes/us-auth-003-email-otp-api-design-spec.md)
> 3. [US-AUTH-004: Đăng nhập bằng Email và Mật khẩu](../../.notes/us-auth-004-login-api-design-spec.md)
> 4. [US-AUTH-005: Quản lý Phiên, Refresh Token Rotation & Đăng xuất](../../.notes/us-auth-005-refresh-token-and-logout-api-design-spec.md)
>
> Mọi thay đổi về business logic, schema hợp đồng hoặc luồng tương tác đều **bắt buộc** phải được cập nhật trước tại file Markdown SSOT tương ứng.

---

## 2. Danh mục Hợp đồng API (Endpoints Index)

| HTTP Method | Endpoint URL | Phân quyền (Permission) | Tóm tắt Chức năng | Đặc tả Gốc (SSOT Link) |
| :--- | :--- | :--- | :--- | :--- |
| **POST** | `/api/identity/accounts` | Public / Anonymous | Đăng ký tài khoản mới & Dormant Recovery (200/201) | [US-AUTH-001 Spec](../../.notes/us-auth-001-account-registration-api-design-spec.md) |
| **POST** | `/api/identity/accounts/verify-email` | Public / Anonymous | Kích hoạt tài khoản qua OTP 6 chữ số | [US-AUTH-003 Spec](../../.notes/us-auth-003-email-otp-api-design-spec.md) |
| **POST** | `/api/identity/accounts/resend-otp` | Public / Anonymous | Gửi lại mã OTP qua email (Anti-Spam & Rate Limit) | [US-AUTH-003 Spec](../../.notes/us-auth-003-email-otp-api-design-spec.md) |
| **POST** | `/api/identity/accounts/login` | Public / Anonymous | Xác thực mật khẩu BCrypt, cấp Session & JWT RS256 | [US-AUTH-004 Spec](../../.notes/us-auth-004-login-api-design-spec.md) |
| **POST** | `/api/identity/accounts/refresh` | Public (kèm RefreshToken) | Cấp lại JWT & Xoay vòng Refresh Token (Rotation) | [US-AUTH-005 Spec](../../.notes/us-auth-005-refresh-token-and-logout-api-design-spec.md) |
| **POST** | `/api/identity/accounts/logout` | Public (kèm RefreshToken) | Đăng xuất phiên hiện tại (Idempotent 200 OK) | [US-AUTH-005 Spec](../../.notes/us-auth-005-refresh-token-and-logout-api-design-spec.md) |
| **POST** | `/api/identity/accounts/logout-all` | Bearer JWT (Authenticated) | Đăng xuất toàn bộ các phiên trên mọi thiết bị | [US-AUTH-005 Spec](../../.notes/us-auth-005-refresh-token-and-logout-api-design-spec.md) |

---

## 3. Các Nguyên Tắc Kiến Trúc & Bất Biến Bảo Mật Cốt Lõi (Core Invariants)

### 3.1. Phân Tầng Chuẩn Mực (Clean Architecture Boundary)
- **Tầng Application (`Identity.Application`)**: Hoàn toàn trung lập với hệ quản trị cơ sở dữ liệu và ORM. Không phụ thuộc vào `Microsoft.EntityFrameworkCore`, `ChangeTracker`, hay `Npgsql.PostgresException`.
- **Tầng Persistence (`Identity.Persistence`)**: Đóng gói toàn bộ logic xử lý hạ tầng dữ liệu. Khi gặp lỗi vi phạm khóa duy nhất email (`SqlState == "23505"` và `ConstraintName == AccountConfiguration.EmailIndexName`), Persistence tự động rollback transaction lỗi (nếu có), dọn dẹp `ChangeTracker` và trả về kết quả ngữ nghĩa `SaveAccountResult.DuplicateEmailConflict`. Tầng Application thực hiện re-query trên clean context để xác định chính xác phản hồi nghiệp vụ (`200 OK` Dormant Recovery hoặc `409 Conflict`).

### 3.2. Thứ Tự Khóa Toàn Cục (Global Lock Ordering Invariant)
Để bảo vệ toàn vẹn dữ liệu trong các tình huống tranh chấp tài nguyên cao độ giữa nhiều thiết bị:
$$\mathbf{Account \ (FOR \ UPDATE)} \longrightarrow \mathbf{AuthSession \ (FOR \ UPDATE)} \longrightarrow \mathbf{RefreshToken}$$
- **Quy tắc bắt buộc**: Mọi luồng tương tác với Session và Token (`POST /refresh`, `POST /logout`, `POST /logout-all`) đều phải tuân thủ thứ tự khóa này.
- **Mục tiêu**: **Loại bỏ lock inversion giữa các luồng Auth và giảm thiểu tối đa rủi ro deadlock** khi người dùng thực hiện refresh đồng thời với logout trên các thiết bị khác nhau.

### 3.3. Thời Hạn Sống của Token & Quản Lý Phiên (Session Lifecycle)
1. **JWT Access Token**:
   - Được ký số bằng thuật toán bất đối xứng **RS256** (RSA SHA-256).
   - Claims bắt buộc: `sub`, `email`, `role`, `jti`, `iss`, `aud`, `exp`.
   - **Thời hạn đề xuất**: `[RECOMMENDED: 15 minutes / OPEN DECISION: 15m vs 24h — Awaiting Tech Lead / Mentor Approval]`. 
   - *Lý do*: Do downstream microservices xác thực JWT dạng Stateless và hệ thống không áp dụng distributed token blacklist, TTL 15 phút giúp giới hạn cửa sổ rủi ro tồn lưu (revocation lag window) sau khi logout tối đa chỉ là 15 phút mà không làm tăng độ trễ mạng.
2. **Refresh Token**:
   - Chuỗi ngẫu nhiên CSPRNG 256-bit, lưu dưới dạng mã băm SHA-256 (`TokenHash`).
   - Áp dụng cơ chế **Strict Single-Use Token Rotation**: mỗi token chỉ được dùng 1 lần duy nhất; ngay khi refresh thành công, token cũ lập tức bị thu hồi và thay thế bằng token mới.
   - **Scoped Reuse Detection**: Nếu một token đã bị thu hồi được gửi lại, hệ thống lập tức thu hồi khẩn cấp phiên làm việc bị rò rỉ (`SessionId`), bảo vệ tài khoản mà không gây ảnh hưởng đến phiên của các thiết bị khác.
   - **Thời hạn**: Sliding window 7 ngày không hoạt động, bị chặn cứng bởi thời hạn tuyệt đối 30 ngày của `AuthSession` (`AbsoluteExpiresAtUtc`).

### 3.4. Yêu Cầu Phía Client (Frontend Single-Flight Pattern)
- Do cơ chế Strict Rotation không áp dụng Grace Window (nhằm triệt tiêu rủi ro replay attack), Frontend **bắt buộc** phải triển khai cơ chế **Single-Flight Refresh** (Axios Interceptor Mutex / Promise Deduplication).
- Khi nhiều request API nhận lỗi 401 Unauthorized cùng lúc, Frontend chỉ được phép phát đi duy nhất 1 request `POST /refresh`. Các request còn lại phải chờ kết quả và sử dụng lại token mới, tránh race condition dẫn đến việc tự kích hoạt Reuse Detection.

### 3.5. Kiểm Soát Lỗi Đăng Nhập & Khóa Tài Khoản (Lockout Policy)
- Theo quy định `FR-USER-008`: Sau **3 lần nhập sai mật khẩu liên tiếp** (`FailedLoginAttempts >= 3`), tài khoản tự động bị khóa tạm thời trong **15 phút**.
- Endpoint Login trả về mã lỗi HTTP **423 Locked** (`identity.account_temporarily_locked`).
- Nhập đúng mật khẩu trước khi đạt ngưỡng 3 lần sẽ lập tức xóa chuỗi đếm sai (`FailedLoginAttempts = 0`).

---

## 4. Chuẩn Định Dạng Phản Hồi Lỗi (RFC 7807 ProblemDetails)

Tất cả các API khi gặp lỗi đều tuân thủ chuẩn công nghiệp **RFC 7807 ProblemDetails**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Descriptive business error explanation.",
  "errorCode": "identity.specific_error_code",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

Bảng tổng hợp các mã lỗi chuẩn của hệ thống:
| HTTP Status | Mã lỗi (`errorCode`) | Thuộc API | Ý nghĩa nghiệp vụ |
| :---: | :--- | :--- | :--- |
| **400** | `validation_error` | Tất cả | Lỗi định dạng dữ liệu đầu vào (FluentValidation) |
| **400** | `identity.invalid_otp_code` | `/verify-email` | Mã OTP nhập không chính xác (AttemptCount được commit lưu vết) |
| **400** | `identity.otp_expired` | `/verify-email` | Mã OTP đã quá hạn 5 phút hoặc không tồn tại |
| **400** | `identity.otp_max_attempts_exceeded` | `/verify-email` | Nhập sai OTP quá 3 lần, mã bị vô hiệu hóa |
| **401** | `identity.invalid_credentials` | `/login` | Sai email hoặc mật khẩu (có dummy BCrypt chống timing attack) |
| **401** | `identity.invalid_refresh_token` | `/refresh` | Chuỗi Refresh Token không tồn tại |
| **401** | `identity.refresh_token_expired` | `/refresh` | Refresh Token đã quá hạn 7 ngày không hoạt động |
| **401** | `identity.session_expired` | `/refresh` | Phiên đã đạt hạn mức tuyệt đối 30 ngày |
| **401** | `identity.session_revoked` | `/refresh` | Phiên đã bị thu hồi trước đó (bởi Logout hoặc Logout-All) |
| **401** | `identity.refresh_token_reused` | `/refresh` | Phát hiện tái sử dụng token đã rotate; thu hồi khẩn cấp phiên |
| **403** | `identity.email_not_verified` | `/login`, `/refresh` | Tài khoản chưa xác thực kích hoạt email |
| **403** | `identity.account_suspended` | `/login`, `/refresh` | Tài khoản bị tạm đình chỉ [PROPOSED] |
| **403** | `identity.account_disabled` | `/login`, `/refresh` | Tài khoản bị vô hiệu hóa [PROPOSED] |
| **409** | `identity.email_already_registered` | `/accounts` | Email đã được đăng ký và kích hoạt |
| **423** | `identity.account_temporarily_locked` | `/login` | Tài khoản bị tạm khóa 15 phút do sai mật khẩu 3 lần (FR-USER-008) |
| **429** | `identity.resend_cooldown_active` | `/resend-otp` | Chưa hết thời gian giãn cách Cooldown 60s [PROPOSED] |
| **429** | `identity.resend_rate_limit_exceeded` | `/resend-otp` | Vượt quá giới hạn 5 mã OTP / giờ |
