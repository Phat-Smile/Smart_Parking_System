# Smart Parking System — Danh Mục Đặc Tả Kỹ Thuật API (API Documentation Catalogue)

Tài liệu này tổng hợp toàn bộ các đặc tả hợp đồng giao tiếp (API Contracts) của hệ thống Smart Parking System, được thiết kế theo chuẩn doanh nghiệp cao cấp (Enterprise Production-Ready), đảm bảo tính nhất quán giữa Backend .NET 8 và Frontend Next.js / Mobile Client.

---

## 1. Mục lục API Identity Service (Chi tiết từng Endpoint)

Identity Service chịu trách nhiệm xác thực, phân quyền, quản lý phiên làm việc đa thiết bị, bảo mật tài khoản và quản trị người dùng.

### 1.1. Nhóm 1: Đăng ký & Kích hoạt Tài khoản (Registration & Verification)

| STT | Phương thức | Đường dẫn API | Mã User Story | Tài liệu đặc tả chi tiết | Mô tả tóm tắt |
| :---: | :---: | :--- | :---: | :--- | :--- |
| 1 | `POST` | `/api/identity/accounts` | `US-AUTH-001` | [Identity-Register-Account.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Register-Account.md) | Đăng ký tài khoản mới bằng Email & Mật khẩu chuẩn NIST, phát hành OTP 6 số. |
| 2 | `POST` | `/api/identity/accounts/verify-email` | `US-AUTH-003` | [Identity-Verify-Email.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Verify-Email.md) | Xác thực email bằng mã OTP 6 chữ số, chuyển trạng thái `EmailVerified`. |
| 3 | `POST` | `/api/identity/accounts/resend-otp` | `US-AUTH-003` | [Identity-Resend-Otp.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Resend-Otp.md) | Gửi lại mã OTP qua email (Cooldown 60s, giới hạn tối đa 5 mã/giờ). |

---

### 1.2. Nhóm 2: Đăng nhập & Quản lý Phiên làm việc (Authentication & Sessions)

| STT | Phương thức | Đường dẫn API | Mã User Story | Tài liệu đặc tả chi tiết | Mô tả tóm tắt |
| :---: | :---: | :--- | :---: | :--- | :--- |
| 4 | `POST` | `/api/identity/accounts/login` | `US-AUTH-004` | [Identity-Login.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Login.md) | Đăng nhập bằng Email/Password, cấp JWT 15m + Cookie `sp_refresh` 7 ngày, khóa 15m nếu sai 3 lần. |
| 5 | `POST` | `/api/identity/accounts/google` | `US-AUTH-002` | [Identity-Google-Login.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Google-Login.md) | Đăng nhập/Đăng ký một chạm với Google ID Token, tự động xác thực email. |
| 6 | `POST` | `/api/identity/accounts/refresh` | `US-AUTH-005` | [Identity-Refresh-Token.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Refresh-Token.md) | Cấp Access Token mới không gián đoạn (Silent Refresh), xoay vòng Token và tự phát hiện xâm nhập (Reuse Detection). |
| 7 | `GET` | `/api/identity/accounts/me` | `US-AUTH-004` | [Identity-Get-Me.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Get-Me.md) | Lấy thông tin tài khoản hiện tại (`id`, `email`, `role`, `fullName`, `hasPassword`, `googleLinked`). |
| 8 | `POST` | `/api/identity/accounts/logout` | `US-AUTH-005` | [Identity-Logout.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Logout.md) | Đăng xuất thiết bị hiện tại, thu hồi Refresh Token và xóa cookie `sp_refresh`. |
| 9 | `POST` | `/api/identity/accounts/logout-all` | `US-AUTH-005` | [Identity-Logout-All.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Logout-All.md) | Đăng xuất khỏi toàn bộ thiết bị đang hoạt động trên hệ thống. |
| 10 | `GET` | `/api/identity/accounts/sessions` | `US-AUTH-005` | [Identity-List-Sessions.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-List-Sessions.md) | Lấy danh sách thiết bị đang đăng nhập, nhận diện tên máy, che giấu IP, gắn cờ `isCurrent`. |
| 11 | `DELETE` | `/api/identity/accounts/sessions/{sessionId}` | `US-AUTH-005` | [Identity-Revoke-Session.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Revoke-Session.md) | Thu hồi phiên / Đăng xuất thiết bị từ xa (Bảo vệ chống lộ IDOR). |

---

### 1.3. Nhóm 3: Quản lý Mật khẩu & Phục hồi (Password Management)

| STT | Phương thức | Đường dẫn API | Mã User Story | Tài liệu đặc tả chi tiết | Mô tả tóm tắt |
| :---: | :---: | :--- | :---: | :--- | :--- |
| 12 | `POST` | `/api/identity/accounts/forgot-password` | `US-AUTH-006` | [Identity-Forgot-Password.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Forgot-Password.md) | Yêu cầu mã OTP đặt lại mật khẩu qua email (Cơ chế Anti-Enumeration chống dò email). |
| 13 | `POST` | `/api/identity/accounts/reset-password` | `US-AUTH-006` | [Identity-Reset-Password.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Reset-Password.md) | Đặt lại mật khẩu bằng OTP 6 số, tự động mở khóa Lockout và hủy phiên trên mọi thiết bị. |
| 14 | `POST` | `/api/identity/accounts/change-password` | `US-AUTH-009` | [Identity-Change-Password.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Change-Password.md) | Đổi mật khẩu chủ động trong trang cài đặt, khóa 15m nếu nhập sai mật khẩu cũ 3 lần. |

---

### 1.4. Nhóm 4: Quản trị Hệ thống (Administration - SystemAdmin Only)

| STT | Phương thức | Đường dẫn API | Mã User Story | Tài liệu đặc tả chi tiết | Mô tả tóm tắt |
| :---: | :---: | :--- | :---: | :--- | :--- |
| 15 | `GET` | `/api/identity/admin/accounts` | `US-ADMIN-003` | [Identity-Admin-Search-Accounts.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Admin-Search-Accounts.md) | Tìm kiếm mờ (Email, Họ tên, SĐT), lọc theo Role/Status và phân trang linh hoạt (tối đa 100). |
| 16 | `PUT` | `/api/identity/admin/accounts/{accountId}/role` | `US-ADMIN-003` | [Identity-Admin-Change-Role.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Admin-Change-Role.md) | Nâng cấp/đổi vai trò người dùng (`Customer`, `Staff`, `ParkingLotOwner`, `SystemAdmin`), cấm tự đổi quyền mình. |
| 17 | `POST` | `/api/identity/admin/accounts/{accountId}/unlock` | `US-AUTH-008` | [Identity-Admin-Unlock-Account.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Admin-Unlock-Account.md) | Mở khóa thủ công tài khoản bị tạm khóa do nhập sai mật khẩu quá 3 lần. |
| 18 | `POST` | `/api/identity/admin/accounts/{accountId}/suspend` | `US-ADMIN-003` | [Identity-Admin-Suspend-Account.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Admin-Suspend-Account.md) | Tạm đình chỉ tài khoản vi phạm, hủy toàn bộ phiên làm việc, cấm đình chỉ Admin khác. |
| 19 | `POST` | `/api/identity/admin/accounts/{accountId}/activate` | `US-ADMIN-003` | [Identity-Admin-Activate-Account.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Admin-Activate-Account.md) | Kích hoạt lại tài khoản đang bị đình chỉ (Từ chối tài khoản chưa hoàn tất OTP email). |

---

### 1.5. Nhóm 5: Giao tiếp Nội bộ Microservices (Inter-Service gRPC)

| STT | Phương thức | Tên RPC Service | Bounded Context | Tài liệu đặc tả chi tiết | Mô tả tóm tắt |
| :---: | :---: | :--- | :---: | :--- | :--- |
| 20 | `gRPC` | `IdentityVehicleContract.LookupVehicle` | `Vehicle Lookup` | [Identity-gRPC-Vehicle-Lookup.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-gRPC-Vehicle-Lookup.md) | Tra cứu thông tin xe & chủ xe từ biển số quét qua camera AI cổng bãi đỗ (Chuẩn hóa tự động). |

---

## 2. Tài liệu Hợp đồng Tổng hợp (Grouped Contracts)

Ngoài tài liệu tách lẻ cho từng Endpoint, hệ thống còn cung cấp 5 bộ tài liệu tổng hợp theo từng phân hệ nghiệp vụ lớn để thuận tiện cho việc Review tổng quan:

1. [Identity-Registration-Contract.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Registration-Contract.md) — Tổng hợp Đăng ký & Xác thực Email.
2. [Identity-Authentication-Contract.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Authentication-Contract.md) — Tổng hợp Đăng nhập, Google OAuth & Refresh Token.
3. [Identity-Sessions-Contract.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Sessions-Contract.md) — Tổng hợp Đăng xuất & Quản lý thiết bị.
4. [Identity-Password-Contract.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Password-Contract.md) — Tổng hợp Quên mật khẩu, Reset mật khẩu & Đổi mật khẩu.
5. [Identity-Admin-Contract.md](file:///f:/Ky_5/Smart_Parking_System/docs/api/Identity-Admin-Contract.md) — Tổng hợp Toàn bộ phân hệ Quản trị Admin.

---

## 3. Quy Chuẩn Kỹ Thuật Chung (Global Conventions)

- **Định dạng dữ liệu:** JSON (`application/json`) qua UTF-8.
- **Thời gian (Timestamp):** Chuẩn ISO-8601 UTC kết thúc bằng chữ `Z` (VD: `2026-10-13T08:30:00Z`).
- **Khóa chính (Primary Key):** UUID v4 36 ký tự (VD: `a3bb108e-17d4-4a6c-9f87-2a54e9912345`).
- **Chuẩn báo lỗi:** RFC 7807 Problem Details kèm extension `"errorCode"` tường minh.
- **Bảo mật Cookie:** `HttpOnly`, `SameSite=Strict`, `Secure`, `Path=/api/identity/accounts`.
