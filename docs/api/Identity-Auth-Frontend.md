# Identity Auth cho Frontend

Tài liệu giao FE cho mọi màn đăng ký, đăng nhập, phiên, mật khẩu và quản trị người dùng. Mọi request đi qua Gateway `http://localhost:5000`. Swagger đầy đủ ở `http://localhost:5001/swagger` (chỉ Development).

## 1. Chạy BE trên máy

```powershell
docker compose up -d postgres rabbitmq identity-api notification-api parking-api gateway
powershell -ExecutionPolicy Bypass -File scripts/database/Update-DevelopmentDatabases.ps1
powershell -ExecutionPolicy Bypass -File scripts/dev/Seed-AuthTestAccounts.ps1
```

- **JWT key**: lần đầu chạy `scripts/security/New-DevelopmentJwtKey.ps1`. Thiếu key thì `identity-api` không khởi động.
- **OTP không cần email thật**: để trống `SMTP_HOST` trong `.env` (hoặc đặt `console`) rồi tạo lại `notification-api`; mã OTP in ra log: `docker logs parking-system-notification-api-1`.
- **Google**: đặt `GOOGLE_CLIENT_ID` trong `.env` (cùng giá trị cho `web` và `identity-api`); để trống thì `/google` trả 503.
- **Rate limit khi test nhiều**: Gateway giới hạn theo IP mỗi phút (login 10, register 5, resend-otp 5, verify-email 10, refresh 30, forgot-password 5, reset-password 10, google 10). Nới bằng biến môi trường của `gateway`, ví dụ `RateLimiting__Auth__auth-login=1000`.

### Tài khoản test (sau khi chạy script seed)

| Email | Role | Mật khẩu |
|---|---|---|
| customer@parking.test | Customer | Test@12345 |
| staff@parking.test | Staff | Test@12345 |
| owner@parking.test | ParkingLotOwner | Test@12345 |
| admin@parking.test | SystemAdmin | Test@12345 |

## 2. Token: FE giữ gì, ở đâu

| Thứ | Nằm ở đâu | FE làm gì |
|---|---|---|
| Access token (JWT, 15 phút) | Body của login / google / refresh | Giữ **trong bộ nhớ** (state/store), gửi `Authorization: Bearer <token>`. Không lưu localStorage / sessionStorage |
| Refresh token (7 ngày, xoay mỗi lần dùng) | Cookie `sp_refresh` HttpOnly do BE đặt | **Không đọc, không lưu**. Mọi `fetch` tới `/api/identity/accounts/refresh` và `/logout` phải có `credentials: "include"` |

Cookie: `HttpOnly`, `SameSite=Strict`, `Path=/api/identity/accounts`, `Secure` ngoài Development. Gateway trả `Access-Control-Allow-Credentials: true` cho origin trong `Security__Cors__AllowedOrigins` (mặc định `http://localhost:3000`).

### Luồng chuẩn

1. **Đăng nhập** `POST /login` → lưu `accessToken`, `account.role` để dựng menu.
2. **Tải lại trang (F5)** → access token trong bộ nhớ mất → gọi `POST /refresh` (body rỗng, `credentials: "include"`) → nhận access token mới → gọi `GET /me` để lấy lại thông tin.
3. **Gọi API bị 401** → gọi `/refresh` **một lần** (gộp các request đang chờ vào một lần refresh) → thử lại request. Refresh trả 401/403 → coi như đã đăng xuất, chuyển `/login`.
4. **Đăng xuất** `POST /logout` (`credentials: "include"`) → BE thu hồi phiên và xoá cookie; FE xoá access token trong bộ nhớ.

## 3. Danh sách API (tiền tố `/api/identity`)

| API | Quyền | Request | Thành công |
|---|---|---|---|
| `POST /accounts` | Công khai | `{ email, password, phoneNumber?, fullName? }` | 201 `{ accountId, email, fullName, status, message }` |
| `POST /accounts/verify-email` | Công khai | `{ email, otpCode }` | 200 |
| `POST /accounts/resend-otp` | Công khai | `{ email }` | 200 `{ otpSent, expiresInSeconds, cooldownSeconds, message }` |
| `POST /accounts/login` | Công khai | `{ email, password }` | 200 `{ accessToken, tokenType, expiresIn, account }` + cookie |
| `POST /accounts/google` | Công khai | `{ idToken }` | Như login |
| `POST /accounts/refresh` | Cookie | Body rỗng | Như login (cookie mới) |
| `POST /accounts/logout` | Cookie | Body rỗng | 200 (luôn 200, kể cả không có cookie) |
| `POST /accounts/logout-all` | Bearer | — | 200, mọi thiết bị bị đăng xuất |
| `GET /accounts/me` | Bearer | — | 200 `{ id, email, phoneNumber, fullName, role, status, hasPassword, googleLinked, createdAtUtc }` |
| `GET /accounts/sessions` | Bearer | — | 200 `[{ id, device, ipAddress, createdAtUtc, lastActivityAtUtc, isCurrent }]` |
| `DELETE /accounts/sessions/{id}` | Bearer | — | 200; xoá đúng thiết bị đang dùng thì cookie cũng bị xoá → FE coi như đăng xuất |
| `POST /accounts/forgot-password` | Công khai | `{ email }` | 200 (mã 6 số gửi qua email) |
| `POST /accounts/reset-password` | Công khai | `{ email, otpCode, newPassword }` | 200, mọi phiên bị đăng xuất |
| `POST /accounts/change-password` | Bearer | `{ currentPassword, newPassword }` | 200, mọi phiên bị đăng xuất |
| `GET /admin/accounts?search=&role=&status=&page=&pageSize=` | SystemAdmin | — | 200 `{ items, page, pageSize, totalCount }` |
| `POST /admin/accounts/{id}/suspend` | SystemAdmin | — | 200 |
| `POST /admin/accounts/{id}/activate` | SystemAdmin | — | 200 |
| `PUT /admin/accounts/{id}/role` | SystemAdmin | `{ role }` | 200, người đó bị đăng xuất mọi thiết bị |
| `POST /admin/accounts/{id}/unlock` | SystemAdmin | — | 200 |

**Quên mật khẩu dùng mã OTP 6 số, không phải link.** Màn `/reset-password` cần ô email, ô mã OTP và mật khẩu mới.

Giá trị `role`: `Customer`, `Staff`, `ParkingLotOwner`, `SystemAdmin`. Giá trị `status`: `PendingEmailVerification`, `EmailVerified`, `Suspended`, `Disabled`.

## 4. Lỗi: FE rẽ nhánh theo `errorCode`

Mọi lỗi là `application/problem+json` có `title`, `detail` (câu tiếng Việt hiển thị được ngay), `errorCode`, `correlationId`. Không so sánh theo câu chữ, câu có thể đổi.

- **Lỗi nhập liệu** 400 `request.validation_failed`: `errors` có đúng một câu cho mỗi trường, hiện dưới ô nhập tương ứng.
- **Phải chờ** (423, 429): có header `Retry-After` và `retryAfterSeconds` trong body (giây). Dùng để đếm ngược.

| HTTP | errorCode | Khi nào | FE nên làm |
|---|---|---|---|
| 409 | `identity.email_already_registered` | Email đã có (kể cả đang chờ xác thực) | Gợi ý đăng nhập; nếu chưa xác thực thì nút "Nhập mã OTP" |
| 409 | `identity.phone_already_registered` | SĐT đã thuộc tài khoản khác | Báo dưới ô SĐT |
| 400 | `identity.invalid_otp_code` | Sai mã, còn lượt (câu có số lượt còn lại) | Cho nhập lại |
| 400 | `identity.otp_max_attempts_exceeded` | Sai 3 lần | Ẩn ô nhập, chỉ còn "Gửi lại mã" |
| 400 | `identity.otp_expired` | Hết hạn, bị thay bằng mã mới, hoặc tài khoản đã kích hoạt | Hiện câu trong `detail`, nút "Gửi lại mã" |
| 429 | `identity.resend_cooldown_active` / `identity.reset_cooldown_active` | Gửi lại trong 60 giây | Đếm ngược `retryAfterSeconds` trên nút gửi lại |
| 429 | `identity.resend_rate_limit_exceeded` / `identity.reset_rate_limit_exceeded` | Quá 5 mã/giờ | Đếm ngược |
| 429 | `gateway.rate_limited` | Quá nhanh theo IP | Đếm ngược |
| 401 | `identity.invalid_credentials` | Sai email/mật khẩu (câu có số lần còn lại) | Báo chung, không tách email hay mật khẩu sai |
| 401 | `identity.password_not_set` | Tài khoản Google chưa có mật khẩu | Gợi ý nút Google hoặc "Quên mật khẩu" |
| 403 | `identity.email_not_verified` | Đăng nhập khi chưa xác thực | Chuyển `/verify-otp` với email vừa nhập |
| 403 | `identity.account_suspended` / `identity.account_disabled` | Bị đình chỉ / vô hiệu | Báo liên hệ quản trị viên |
| 423 | `identity.account_temporarily_locked` | Sai mật khẩu 3 lần | Đếm ngược `retryAfterSeconds`; gợi ý "Quên mật khẩu" để mở ngay |
| 401 | `identity.invalid_refresh_token`, `identity.refresh_token_expired`, `identity.session_expired`, `identity.session_revoked`, `identity.refresh_token_reused` | Refresh thất bại | Đăng xuất, chuyển `/login` (cookie đã bị xoá) |
| 401 | `identity.invalid_google_token` | Token Google sai | Cho thử lại |
| 503 | `identity.google_login_unavailable` | Chưa cấu hình Google | Ẩn nút Google |
| 400 | `identity.invalid_current_password` | Đổi mật khẩu, sai mật khẩu cũ | Báo dưới ô mật khẩu hiện tại |
| 400 | `identity.password_same_as_current` | Mật khẩu mới trùng cũ | Báo dưới ô mật khẩu mới |
| 404 | `identity.session_not_found` | Thu hồi phiên không thuộc mình / không tồn tại | Tải lại danh sách thiết bị |
| 403 | `identity.cannot_change_own_role`, `identity.cannot_change_own_status`, `identity.cannot_change_admin_status` | Admin tự đổi mình / đình chỉ Admin khác | Ẩn nút với chính mình và với Admin khác |
| 409 | `identity.account_not_verified` | Kích hoạt tài khoản chưa xác thực email | Ẩn nút kích hoạt khi status là `PendingEmailVerification` |
| 404 | `identity.account_not_found` | Id không tồn tại | Tải lại danh sách |
| 401 / 403 | `auth.unauthorized` / `auth.forbidden` | Thiếu token / sai role | 401: refresh rồi thử lại; 403: trang 403 |
