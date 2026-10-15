# Đặc Tả Giao Diện API: Đăng Nhập & Phiên Làm Việc (Authentication & Session)

> **Dịch vụ:** Identity Service (`ParkWise.Identity.WebAPI`)  
> **Phiên bản:** v1.0.0 (Chuẩn hóa tường minh cho Frontend - Đối soát 100% Backend)  
> **Trạng thái:** Sẵn sàng tích hợp (Production-Ready)  
> **Đối tượng sử dụng:** Frontend Web (`web/`), Mobile App, QA/Tester  
> **Base URL:** `/api/identity/accounts` (Trực tiếp: `http://localhost:5001/api/identity/accounts` | Qua Gateway: `http://localhost:5000/api/identity/accounts`)

---

## 1. 📌 Bối Cảnh & Kiến Trúc Bảo Mật Phiên (Session Architecture)

Hệ thống ParkWise triển khai cơ chế xác thực kết hợp **JWT Access Token trong bộ nhớ (Memory/State)** và **Refresh Token trong Cookie an toàn (`sp_refresh`)**:

1. **Access Token (JWT)**:
   - Thời gian sống ngắn: **15 phút**.
   - Được trả về trong Response Body (`accessToken`). Frontend lưu trong Memory/React State (hoặc Pinia/Redux), **tuyệt đối không lưu vào `localStorage`** để chống tấn công XSS.
   - Được gửi kèm trong Header mọi request cần xác thực: `Authorization: Bearer <accessToken>`.
2. **Refresh Token (HttpOnly Cookie `sp_refresh`)**:
   - Thời gian sống: **7 ngày** (xoay vòng sau mỗi lần refresh).
   - Được Backend tự động ghi vào trình duyệt qua Cookie `sp_refresh` với cờ `HttpOnly`, `SameSite=Strict/Lax`, `Path=/api/identity/accounts`.
   - JavaScript Frontend **không thể đọc được** cookie này (bảo vệ tuyệt đối chống đánh cắp token).
   - Trình duyệt tự động đính kèm cookie khi gọi `/refresh` hoặc `/logout`. Frontend bắt buộc cấu hình `withCredentials: true` (hoặc `credentials: 'include'`).
3. **Cơ chế Silent Refresh (Làm mới token ngầm)**:
   - Khi Access Token hết hạn, Axios Interceptor trên Frontend bắt mã lỗi `401 Unauthorized`, tự động gọi `POST /api/identity/accounts/refresh` để lấy cặp token mới rồi thực hiện lại request ban đầu một cách trong suốt.

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng
    participant FE as Frontend Client
    participant API as Identity API
    participant DB as Postgres Database

    User->>FE: 1. Nhập Email & Mật khẩu
    FE->>API: POST /api/identity/accounts/login (Credentials)
    alt Sai mật khẩu / Chưa kích hoạt
        API-->>FE: 401 / 403 / 423 ProblemDetails
        FE-->>User: Hiển thị lỗi hoặc số giây chờ mở khóa
    else Đăng nhập thành công
        API->>DB: Tạo AuthSession & RefreshToken mới
        API-->>FE: 200 OK (AccessToken trong Body + Cookie sp_refresh)
        FE->>FE: Lưu AccessToken vào Memory & gọi GET /me
    end

    User->>FE: 2. Người dùng truy cập trang bảo mật
    FE->>API: GET /api/... (Header: Authorization: Bearer AccessToken)
    alt Token hết hạn (15 phút)
        API-->>FE: 401 Unauthorized
        Note over FE,API: Axios Interceptor kích hoạt Silent Refresh
        FE->>API: POST /api/identity/accounts/refresh (Tự đính kèm Cookie sp_refresh)
        API->>DB: Thu hồi token cũ, cấp token mới (Token Rotation)
        API-->>FE: 200 OK (AccessToken mới + Set-Cookie sp_refresh mới)
        FE->>API: Thử lại request ban đầu với AccessToken mới
        API-->>FE: 200 OK (Dữ liệu trả về bình thường)
    end

    User->>FE: 3. Người dùng bấm Đăng xuất
    FE->>API: POST /api/identity/accounts/logout
    API->>DB: Thu hồi (Revoke) Session & RefreshToken
    API-->>FE: 200 OK (Set-Cookie: sp_refresh=; Max-Age=0)
    FE->>FE: Xóa AccessToken trong Memory & Chuyển hướng sang /login
```

---

## 2. 🌐 Đặc Tả Chi Tiết Từng Endpoint

---

### 2.1. Đăng Nhập Bằng Email & Mật Khẩu (Login)

Xác thực thông tin tài khoản, tạo phiên làm việc mới, cấp phát Access Token và Refresh Token.

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts/login`
- **Quyền truy cập (Auth):** `Public` (Không yêu cầu đăng nhập trước)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị | Giải thích |
| :--- | :---: | :--- | :--- |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` | Định dạng payload gửi lên |
| `Accept` | 🟢 **KHUYẾN NGHỊ** | `application/json` | Định dạng phản hồi mong muốn |
| `User-Agent` | 🟢 **KHUYẾN NGHỊ** | Trình duyệt tự gửi | Backend đọc để lưu thông tin thiết bị (`deviceInfo`) |
| `Authorization` | ❌ **KHÔNG GỬI** | *None* | Endpoint công khai |

#### 📥 Request Body Schema (Phân Định Rõ Ràng)

| Tên trường (Field) | Kiểu dữ liệu | Phân loại | Giá trị mặc định | Ràng buộc Validation & Quy tắc kiểm tra |
| :--- | :---: | :---: | :---: | :--- |
| `email` | `string` | 🔴 **BẮT BUỘC** | *Không có* | - Không được để trống hoặc `null`.<br>- Định dạng email hợp lệ RFC 5322.<br>- Tối đa 254 ký tự.<br>- *Lỗi vi phạm:* `"Email là bắt buộc, không được để trống."` |
| `password` | `string` | 🔴 **BẮT BUỘC** | *Không có* | - Không được để trống hoặc `null`.<br>- Tối thiểu 8 ký tự, tối đa 72 bytes UTF-8.<br>- *Lỗi vi phạm:* `"Mật khẩu là bắt buộc, không được để trống."` |

#### 📝 Request Body Mẫu
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

#### 📤 Phản Hồi Thành Công (`200 OK`)

##### 🍪 Cookies Được Thiết Lập (Response Headers)
Backend sẽ tự động gửi kèm header thiết lập Cookie:
```http
Set-Cookie: sp_refresh=f81d4fae-7dec-11d0-a765-00a0c91e6bf6; Path=/api/identity/accounts; HttpOnly; SameSite=Strict; Max-Age=604800; Secure
```

##### 📦 Response Body Schema

| Trường (Field) | Kiểu dữ liệu | Phân loại | Ý nghĩa & Giá trị mẫu |
| :--- | :---: | :---: | :--- |
| `accessToken` | `string (JWT)` | 🟢 **LUÔN CÓ** | Chuỗi JSON Web Token chứa User ID, Role, Permissions. Dùng để gọi API. |
| `refreshToken` | `string (UUID)` | 🟢 **LUÔN CÓ** | Chuỗi Refresh Token dự phòng (ngoài việc đã ghi vào cookie). |
| `tokenType` | `string` | 🟢 **LUÔN CÓ** | Luôn mang giá trị cố định: `"Bearer"` |
| `expiresIn` | `integer` | 🟢 **LUÔN CÓ** | Số giây Access Token có hiệu lực: `900` (giây) = 15 phút |

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhM2JiMTA4ZS0xN2Q0LTRhNmMtOWY4Ny0yYTU0ZTk5MTIzNDUiLCJlbWFpbCI6InVzZXJAZXhhbXBsZS5jb20iLCJyb2xlIjoiQ3VzdG9tZXIiLCJleHAiOjE3MjkxMjM0NTZ9...",
  "refreshToken": "c9b578e2-f472-4b2a-8bc2-a1b948f93e12",
  "tokenType": "Bearer",
  "expiresIn": 900
}
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết (Error Responses)

| HTTP Status | Error Code (`errorCode`) | Header đính kèm | Nguyên nhân cụ thể | Hành động gợi ý cho Frontend |
| :---: | :--- | :---: | :--- | :--- |
| `401 Unauthorized` | `identity.invalid_credentials` | *None* | Email hoặc mật khẩu không chính xác. | Hiển thị: *"Email hoặc mật khẩu không chính xác."* Xóa trắng ô password. |
| `401 Unauthorized` | `identity.password_not_set` | *None* | Tài khoản này đăng ký qua Google, chưa tạo mật khẩu truyền thống. | Thông báo: *"Tài khoản chưa thiết lập mật khẩu. Vui lòng đăng nhập bằng Google."* |
| `403 Forbidden` | `identity.email_not_verified` | *None* | Tài khoản chưa xác thực OTP qua email. | Điều hướng người dùng sang trang `/verify-otp?email=...`. |
| `403 Forbidden` | `identity.account_suspended` | *None* | Tài khoản đang bị tạm đình chỉ hoạt động bởi Admin. | Thông báo liên hệ hỗ trợ kỹ thuật. |
| `403 Forbidden` | `identity.account_disabled` | *None* | Tài khoản đã bị vô hiệu hóa vĩnh viễn. | Thông báo tài khoản không còn hoạt động. |
| `423 Locked` | `identity.account_temporarily_locked` | `Retry-After: <số giây>` | Đăng nhập sai quá **3 lần liên tiếp**. Tài khoản bị khóa tạm thời. | Đọc header `Retry-After` để hiển thị đếm ngược: *"Tài khoản tạm khóa. Thử lại sau XX giây."* |

---

### 2.2. Đăng Nhập Bằng Google (Google Login)

Xác thực hoặc tự động đăng ký tài khoản mới bằng Google ID Token (Google Identity Services).

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts/google`
- **Quyền truy cập (Auth):** `Public` (Không yêu cầu đăng nhập trước)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị |
| :--- | :---: | :--- |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` |
| `Accept` | 🟢 **KHUYẾN NGHỊ** | `application/json` |

#### 📥 Request Body Schema

| Tên trường (Field) | Kiểu dữ liệu | Phân loại | Ràng buộc Validation & Quy tắc kiểm tra |
| :--- | :---: | :---: | :--- |
| `idToken` | `string` | 🔴 **BẮT BUỘC** | Chuỗi Google ID Token nhận được từ thư viện `@react-oauth/google` hoặc Google SDK. Không được để trống. |

#### 📝 Request Body Mẫu
```json
{
  "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6IjFhMmIzYy...sample_google_id_token"
}
```

#### 📤 Phản Hồi Thành Công (`200 OK`)
*Cấu trúc Response và Cookie trả về hoàn toàn giống hệt API Login (Mục 2.1).*

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "e7c65432-d123-4a56-8bc9-f123456789ab",
  "tokenType": "Bearer",
  "expiresIn": 900
}
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết

| HTTP Status | Error Code (`errorCode`) | Nguyên nhân cụ thể | Hành động gợi ý cho Frontend |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `identity.invalid_google_token` | Token Google không hợp lệ, giả mạo hoặc đã hết hạn. | Yêu cầu người dùng bấm đăng nhập Google lại. |
| `403 Forbidden` | `identity.account_suspended` | Tài khoản Google liên kết đã bị khóa bởi hệ thống. | Hiển thị thông báo tài khoản bị đình chỉ. |
| `503 Service Unavailable` | `identity.google_login_unavailable` | Backend không kết nối được tới máy chủ xác thực của Google. | Thông báo: *"Dịch vụ Google tạm thời gián đoạn. Vui lòng thử lại sau."* |

---

### 2.3. Làm Mới Token Truy Cập (Refresh Token)

Cấp lại Access Token mới và xoay vòng Refresh Token (Token Rotation) khi Access Token cũ hết hạn.

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts/refresh`
- **Quyền truy cập (Auth):** `Public / Cookie-Based` (Đọc tự động từ Cookie `sp_refresh` hoặc Body)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị |
| :--- | :---: | :--- |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` |
| `Cookie` | 🟢 **TỰ ĐỘNG GỬI** | `sp_refresh=<token>` (Yêu cầu FE bật `withCredentials: true`) |

#### 📥 Request Body Schema

| Tên trường (Field) | Kiểu dữ liệu | Phân loại | Giá trị mặc định | Quy tắc kiểm tra |
| :--- | :---: | :---: | :---: | :--- |
| `refreshToken` | `string` | 🟡 **TÙY CHỌN NẾU ĐÃ CÓ COOKIE**<br>🔴 **BẮT BUỘC NẾU KHÔNG CÓ COOKIE** | `""` | - Nếu trình duyệt đã có Cookie `sp_refresh`, trường này có thể truyền chuỗi rỗng `""` hoặc bỏ qua.<br>- Nếu chạy trên Mobile App (không dùng Cookie), bắt buộc phải truyền chuỗi Refresh Token vào trường này. |

#### 📝 Request Body Mẫu

##### Trường hợp 1: Dành cho Web Client (Sử dụng Cookie HttpOnly)
```json
{
  "refreshToken": ""
}
```

##### Trường hợp 2: Dành cho Mobile / Non-browser Client
```json
{
  "refreshToken": "c9b578e2-f472-4b2a-8bc2-a1b948f93e12"
}
```

#### 📤 Phản Hồi Thành Công (`200 OK`)
*Cấp phát cặp Token mới. Backend tự động set-cookie mới cho `sp_refresh`.*

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.new_access_token...",
  "refreshToken": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "tokenType": "Bearer",
  "expiresIn": 900
}
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết

| HTTP Status | Error Code (`errorCode`) | Nguyên nhân cụ thể | Hành động gợi ý cho Frontend |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `identity.invalid_refresh_token` | Token không tồn tại hoặc sai định dạng. | Xóa toàn bộ State đăng nhập, điều hướng về `/login`. |
| `401 Unauthorized` | `identity.refresh_token_expired` | Refresh token đã quá hạn 7 ngày. | Hiển thị: *"Phiên làm việc hết hạn. Vui lòng đăng nhập lại."* |
| `401 Unauthorized` | `identity.session_revoked` | Phiên đã bị thu hồi do đăng xuất từ thiết bị khác. | Điều hướng về màn hình Đăng nhập. |
| `401 Unauthorized` | `identity.token_reuse_detected` | **CẢNH BÁO BẢO MẬT:** Phát hiện Refresh Token cũ bị sử dụng lại (nghi vấn bị đánh cắp). Hệ thống đã tự động hủy bỏ toàn bộ phiên làm việc của tài khoản! | Đăng xuất người dùng ngay lập tức, cảnh báo đổi mật khẩu. |

---

### 2.4. Đăng Xuất Phiên Hiện Tại (Logout)

Thu hồi phiên làm việc gắn liền với Refresh Token hiện tại và xóa sạch Cookie `sp_refresh`.

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts/logout`
- **Quyền truy cập (Auth):** `Public / Cookie-Based`

#### 📋 Request Headers

| Header | Trạng thái | Giá trị |
| :--- | :---: | :--- |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` |
| `Cookie` | 🟢 **TỰ ĐỘNG GỬI** | `sp_refresh=<token>` (Bật `withCredentials: true`) |

#### 📥 Request Body Schema

| Tên trường (Field) | Kiểu dữ liệu | Phân loại | Ghi chú |
| :--- | :---: | :---: | :--- |
| `refreshToken` | `string` hoặc `null` | 🟡 **TÙY CHỌN** | Chuỗi Refresh Token cần hủy. Có thể để trống nếu đã có Cookie `sp_refresh`. Nếu cả cookie và body đều không có, API vẫn trả về `200 OK` (Idempotent). |

#### 📝 Request Body Mẫu
```json
{
  "refreshToken": ""
}
```

#### 📤 Phản Hồi Thành Công (`200 OK`)
*Backend gửi header xóa Cookie: `Set-Cookie: sp_refresh=; Max-Age=0`.*

```json
{
  "message": "Đăng xuất thành công."
}
```

---

### 2.5. Lấy Thông Tin Tài Khoản Đang Đăng Nhập (`GET /me`)

Lấy hồ sơ cá nhân, vai trò (Role) và trạng thái hiện tại của chủ tài khoản sở hữu Access Token.

- **HTTP Method:** `GET`
- **URL Path:** `/api/identity/accounts/me`
- **Quyền truy cập (Auth):** 🔴 **BẮT BUỘC ACCESS TOKEN** (`RequireAuthorization`)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị mẫu | Giải thích |
| :--- | :---: | :--- | :--- |
| `Authorization` | 🔴 **BẮT BUỘC** | `Bearer eyJhbGciOiJSUz...` | Truyền Access Token nhận được từ API Login/Refresh |
| `Accept` | 🟢 **KHUYẾN NGHỊ** | `application/json` | Định dạng dữ liệu mong muốn |

#### 📥 Request Body: ❌ **KHÔNG CÓ BODY (Request GET)**

#### 📤 Phản Hồi Thành Công (`200 OK`)

| Trường (Field) | Kiểu dữ liệu | Phân loại | Ý nghĩa & Giá trị mẫu |
| :--- | :---: | :---: | :--- |
| `id` | `string (UUID)` | 🟢 **LUÔN CÓ** | Định danh duy nhất của tài khoản (`"a3bb108e-17d4-4a6c-9f87-2a54e9912345"`) |
| `email` | `string` | 🟢 **LUÔN CÓ** | Địa chỉ email của tài khoản (`"user@example.com"`) |
| `fullName` | `string` hoặc `null` | ⚪ **CÓ THỂ NULL** | Họ tên người dùng từ bảng Profile (nếu chưa cập nhật thì trả `null`) |
| `phoneNumber` | `string` hoặc `null` | ⚪ **CÓ THỂ NULL** | Số điện thoại đã liên kết (nếu chưa có thì trả `null`) |
| `role` | `string` | 🟢 **LUÔN CÓ** | Vai trò hệ thống: `"Customer"`, `"Staff"`, `"ParkingLotOwner"`, `"SystemAdmin"` |
| `status` | `string` | 🟢 **LUÔN CÓ** | Trạng thái tài khoản: `"Active"`, `"PendingEmailVerification"`, `"Suspended"` |
| `hasPassword` | `boolean` | 🟢 **LUÔN CÓ** | `true` nếu đã có mật khẩu truyền thống, `false` nếu chỉ đăng nhập Google |
| `googleLinked` | `boolean` | 🟢 **LUÔN CÓ** | `true` nếu tài khoản đã liên kết với Google OAuth |
| `createdAtUtc` | `string (ISO-8601)` | 🟢 **LUÔN CÓ** | Thời gian tạo tài khoản (`"2026-10-12T15:30:00Z"`) |

```json
{
  "id": "a3bb108e-17d4-4a6c-9f87-2a54e9912345",
  "email": "user@example.com",
  "fullName": "Nguyễn Văn A",
  "phoneNumber": "0912345678",
  "role": "Customer",
  "status": "Active",
  "hasPassword": true,
  "googleLinked": false,
  "createdAtUtc": "2026-10-12T15:30:00Z"
}
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết

| HTTP Status | Error Code (`errorCode`) | Nguyên nhân cụ thể | Hành động gợi ý cho Frontend |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | *Standard HTTP 401* | Token không hợp lệ, thiếu token hoặc token đã hết hạn. | Kích hoạt cơ chế Silent Refresh qua Axios Interceptor. |
| `404 Not Found` | `identity.account_not_found` | Tài khoản ứng với Token đã bị xóa khỏi cơ sở dữ liệu. | Đăng xuất và xóa sạch session. |

---

## 3. 💻 Khai Báo TypeScript & Cấu Hình Axios Interceptor (Frontend SDK)

### 3.1. TypeScript Interface Definitions

```typescript
// ==========================================
// 1. REQUEST INTERFACES
// ==========================================

export interface LoginRequest {
  // 🔴 [BẮT BUỘC]
  email: string;
  password: string;
}

export interface GoogleLoginRequest {
  // 🔴 [BẮT BUỘC]
  idToken: string;
}

export interface RefreshTokenRequest {
  // 🟡 [TÙY CHỌN TRÊN WEB VÌ ĐÃ CÓ COOKIE sp_refresh]
  refreshToken?: string;
}

export interface LogoutRequest {
  // 🟡 [TÙY CHỌN TRÊN WEB VÌ ĐÃ CÓ COOKIE sp_refresh]
  refreshToken?: string;
}

// ==========================================
// 2. RESPONSE INTERFACES
// ==========================================

export interface LoginResponse {
  accessToken: string;        // 🟢 [LUÔN CÓ] JWT 15 phút
  refreshToken: string;       // 🟢 [LUÔN CÓ] UUID
  tokenType: 'Bearer';        // 🟢 [LUÔN CÓ]
  expiresIn: number;          // 🟢 [LUÔN CÓ] 900 giây
}

export interface RefreshTokenResponse {
  accessToken: string;        // 🟢 [LUÔN CÓ]
  refreshToken: string;       // 🟢 [LUÔN CÓ]
  tokenType: 'Bearer';        // 🟢 [LUÔN CÓ]
  expiresIn: number;          // 🟢 [LUÔN CÓ]
}

export interface LogoutResponse {
  message?: string;
}

export type UserRole = 'Customer' | 'Staff' | 'ParkingLotOwner' | 'SystemAdmin';
export type AccountStatus = 'PendingEmailVerification' | 'Active' | 'Suspended' | 'Disabled';

export interface CurrentAccountResponse {
  id: string;                 // 🟢 [LUÔN CÓ]
  email: string;              // 🟢 [LUÔN CÓ]
  fullName: string | null;    // ⚪ [CÓ THỂ NULL]
  phoneNumber: string | null; // ⚪ [CÓ THỂ NULL]
  role: UserRole;             // 🟢 [LUÔN CÓ]
  status: AccountStatus;      // 🟢 [LUÔN CÓ]
  hasPassword: boolean;       // 🟢 [LUÔN CÓ]
  googleLinked: boolean;      // 🟢 [LUÔN CÓ]
  createdAtUtc: string;       // 🟢 [LUÔN CÓ] ISO-8601
}
```

### 3.2. Cấu Hình Axios Tự Động Gửi Cookie & Silent Refresh

```typescript
import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';

// 1. Biến lưu Access Token trong Memory (Không lưu localStorage)
let memoryAccessToken: string | null = null;

export const setAccessToken = (token: string | null) => {
  memoryAccessToken = token;
};

export const getAccessToken = () => memoryAccessToken;

// 2. Tạo instance Axios với withCredentials = true
export const api = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000',
  headers: {
    'Content-Type': 'application/json',
  },
  withCredentials: true, // 🔴 BẮT BUỘC để trình duyệt tự gửi Cookie sp_refresh
});

// 3. Request Interceptor: Gắn Bearer Token vào Header
api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  if (memoryAccessToken && config.headers) {
    config.headers.Authorization = `Bearer ${memoryAccessToken}`;
  }
  return config;
});

// 4. Response Interceptor: Tự động Refresh Token khi gặp lỗi 401
let isRefreshing = false;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (error: any) => void;
}> = [];

const processQueue = (error: any, token: string | null = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else if (token) {
      prom.resolve(token);
    }
  });
  failedQueue = [];
};

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const originalRequest = error.config as InternalAxiosRequestConfig & { _retry?: boolean };

    // Không refresh nếu chính request refresh bị 401 hoặc endpoint là login
    if (
      error.response?.status === 401 &&
      !originalRequest._retry &&
      !originalRequest.url?.includes('/login') &&
      !originalRequest.url?.includes('/refresh')
    ) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({
            resolve: (token: string) => {
              originalRequest.headers.Authorization = `Bearer ${token}`;
              resolve(api(originalRequest));
            },
            reject: (err) => reject(err),
          });
        });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      try {
        // Gọi API refresh (Trình duyệt tự gửi Cookie sp_refresh)
        const { data } = await axios.post<RefreshTokenResponse>(
          `${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000'}/api/identity/accounts/refresh`,
          { refreshToken: '' },
          { withCredentials: true }
        );

        setAccessToken(data.accessToken);
        processQueue(null, data.accessToken);

        originalRequest.headers.Authorization = `Bearer ${data.accessToken}`;
        return api(originalRequest);
      } catch (refreshError) {
        processQueue(refreshError, null);
        setAccessToken(null);
        // Chuyển hướng người dùng về màn hình đăng nhập nếu refresh thất bại
        if (typeof window !== 'undefined') {
          window.location.href = '/login?expired=true';
        }
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    return Promise.reject(error);
  }
);
```

---

## 4. 🎯 Checklist UI/UX Frontend Màn Hình Đăng Nhập & Quản Lý Phiên

- [ ] **Màn hình Đăng nhập (`/login`):**
  - Cấu hình nút đăng nhập bằng Google bằng `@react-oauth/google`. Sau khi nhận được `credential` (ID Token), gửi ngay đến `POST /api/identity/accounts/google`.
  - Khóa (disable) nút *"Đăng nhập"* và hiển thị loading spinner trong thời gian request đang xử lý.
  - Khi gặp mã lỗi `423 Locked`: Đọc header `Retry-After` và hiển thị đồng hồ đếm ngược: *"Bạn đã nhập sai 3 lần. Tài khoản tạm khóa, vui lòng thử lại sau XX giây"*. Vô hiệu hóa nút Submit cho đến khi hết giờ.
  - Sau khi đăng nhập thành công: Gọi ngay `GET /api/identity/accounts/me` để lấy `role`, lưu thông tin vào User State, rồi điều hướng đúng trang:
    - `Customer` $\rightarrow$ `/` (Trang chủ đặt chỗ).
    - `Staff` $\rightarrow$ `/staff/check-in` (Trang soát vé bãi đỗ).
    - `ParkingLotOwner` $\rightarrow$ `/owner/dashboard` (Trang quản lý bãi đỗ).
    - `SystemAdmin` $\rightarrow$ `/admin` (Trang quản trị hệ thống).
- [ ] **Khởi động ứng dụng (App Hydration / Init):**
  - Khi người dùng F5 hoặc mở lại tab: Tự động gọi `POST /api/identity/accounts/refresh` ngầm. Nếu thành công thì gọi tiếp `GET /me` để phục hồi trạng thái đăng nhập mà không bắt người dùng đăng nhập lại.
- [ ] **Xử lý Đăng xuất (`Logout`):**
  - Khi bấm Đăng xuất: Gọi `POST /api/identity/accounts/logout`, xóa `memoryAccessToken`, reset toàn bộ Store State và điều hướng về `/login`.
