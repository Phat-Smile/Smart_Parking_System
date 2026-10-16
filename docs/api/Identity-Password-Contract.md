# Đặc Tả Giao Diện API: Quản Lý Mật Khẩu (Password Management)

> **Dịch vụ:** Identity Service (`ParkWise.Identity.WebAPI`)  
> **Phiên bản:** v1.0.0 (Chuẩn hóa tường minh cho Frontend - Đối soát 100% Backend)  
> **Trạng thái:** Sẵn sàng tích hợp (Production-Ready)  
> **Đối tượng sử dụng:** Frontend Web (`web/`), Mobile App, QA/Tester  
> **Base URL:** `/api/identity/accounts` (Trực tiếp: `http://localhost:5001/api/identity/accounts` | Qua Gateway: `http://localhost:5000/api/identity/accounts`)

---

## 1. 📌 Bối Cảnh & Kiến Trúc Bảo Mật (Security Architecture)

Nhóm API này quản lý toàn bộ quy trình khôi phục và thay đổi mật khẩu của người dùng, tích hợp các cơ chế bảo mật cấp doanh nghiệp:

1. **Chống dò quét tài khoản (Anti-Account Enumeration)**:
   - Khi gọi API `forgot-password`, dù email có tồn tại hay không, hệ thống **luôn luôn trả về mã HTTP `200 OK`** cùng thông điệp chung để ngăn kẻ xấu dò tìm email của người dùng trong hệ thống.
2. **Cơ chế OTP đặt lại mật khẩu**:
   - Mã OTP gồm **đúng 6 chữ số**, có hiệu lực trong **5 phút (300 giây)**.
   - **Giới hạn nhập sai:** Tối đa **3 lần** (`MaxAttempts = 3`). Nhập sai quá 3 lần, mã OTP bị vô hiệu hóa ngay lập tức.
   - **Chống spam OTP:** Thời gian chờ giữa 2 lần gửi là **60 giây** (Cooldown) và giới hạn **tối đa 5 mã OTP/giờ** (`MaxHourlyOtpIssued = 5`).
3. **Thu hồi phiên toàn diện (Global Session Revocation)**:
   - Bất cứ khi nào mật khẩu được Đặt lại (`reset-password`) hoặc Thay đổi (`change-password`), Backend sẽ **hủy bỏ (Revoke) ngay lập tức toàn bộ phiên làm việc (`AuthSession`) và Refresh Token trên tất cả các thiết bị**.
   - Người dùng bắt buộc phải đăng nhập lại bằng mật khẩu mới để đảm bảo nếu tài khoản từng bị xâm nhập thì kẻ xấu cũng sẽ bị văng ra khỏi hệ thống.
4. **Phạt thử sai khi đổi mật khẩu (Lockout Integration)**:
   - Tại API `change-password`, việc nhập sai mật khẩu hiện tại được tính tương đương một lần đăng nhập sai. Nếu nhập sai liên tiếp **3 lần**, tài khoản sẽ bị khóa tạm thời trong **15 phút** (`423 Locked`).

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng
    participant FE as Frontend Client
    participant API as Identity API
    participant Mail as Notification Service
    participant DB as Postgres Database

    Note over User,DB: LUỒNG 1: QUÊN MẬT KHẨU & ĐẶT LẠI (FORGOT & RESET)
    User->>FE: 1. Nhập Email quên mật khẩu
    FE->>API: POST /api/identity/accounts/forgot-password (Public)
    alt Email hợp lệ & Đã kích hoạt
        API->>Mail: Gửi email chứa OTP 6 chữ số (5 phút)
    end
    API-->>FE: Luôn trả 200 OK (Chống dò quét Email)
    FE->>FE: Chuyển sang màn hình Nhập OTP & Mật khẩu mới

    User->>FE: 2. Nhập OTP + Mật khẩu mới
    FE->>API: POST /api/identity/accounts/reset-password (Public)
    alt OTP sai (< 3 lần) / Hết hạn / Trùng mật khẩu cũ
        API-->>FE: 400 Bad Request (ProblemDetails)
        FE-->>User: Hiển thị lỗi tương ứng
    else Thành công
        API->>DB: Cập nhật Hash mới + Thu hồi mọi phiên cũ + Xóa Lockout
        API-->>FE: 200 OK (PasswordResetResult)
        FE-->>User: Báo thành công & Điều hướng sang /login
    end

    Note over User,DB: LUỒNG 2: ĐỔI MẬT KHẨU CHỦ ĐỘNG (CHANGE PASSWORD)
    User->>FE: 3. Nhập Mật khẩu hiện tại & Mật khẩu mới
    FE->>API: POST /api/identity/accounts/change-password (Header: Bearer JWT)
    alt Sai mật khẩu hiện tại (dưới 3 lần)
        API-->>FE: 400 Bad Request (identity.invalid_current_password)
        FE-->>User: Cảnh báo số lần thử còn lại
    alt Sai mật khẩu hiện tại 3 lần liên tiếp
        API-->>FE: 423 Locked (Retry-After: 900)
        FE-->>User: Khóa form đổi mật khẩu 15 phút
    else Thành công
        API->>DB: Đổi mật khẩu + Thu hồi mọi phiên & Token
        API-->>FE: 200 OK (PasswordChangedResult)
        FE->>FE: Xóa AccessToken trong Memory & Chuyển sang /login
    end
```

---

## 2. 🌐 Đặc Tả Chi Tiết Từng Endpoint

---

### 2.1. Yêu Cầu Mã Đặt Lại Mật Khẩu (Forgot Password)

Gửi mã OTP 6 chữ số về email nếu tài khoản tồn tại và đã kích hoạt. Trả về generic `200 OK` để chống dò quét tài khoản.

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts/forgot-password`
- **Quyền truy cập (Auth):** `Public` (Không yêu cầu đăng nhập)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị | Giải thích |
| :--- | :---: | :--- | :--- |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` | Định dạng payload gửi lên |
| `Accept` | 🟢 **KHUYẾN NGHỊ** | `application/json` | Định dạng phản hồi mong muốn |
| `Authorization` | ❌ **KHÔNG GỬI** | *None* | Endpoint công khai |

#### 📥 Request Body Schema (Phân Định Rõ Ràng)

| Tên trường (Field) | Kiểu dữ liệu | Phân loại | Giá trị mặc định | Ràng buộc Validation & Quy tắc kiểm tra |
| :--- | :---: | :---: | :---: | :--- |
| `email` | `string` | 🔴 **BẮT BUỘC** | *Không có* | - Không được để trống hoặc `null`.<br>- Định dạng email hợp lệ RFC 5322.<br>- Độ dài tối đa: **254 ký tự**.<br>- *Lỗi vi phạm:* `"Email là bắt buộc, không được để trống."` / `"Email không đúng định dạng hợp lệ."` |

#### 📝 Request Body Mẫu
```json
{
  "email": "user@example.com"
}
```

#### 📤 Phản Hồi Thành Công (`200 OK`)

| Trường (Field) | Kiểu dữ liệu | Phân loại | Ý nghĩa & Giá trị mẫu |
| :--- | :---: | :---: | :--- |
| `email` | `string` | 🟢 **LUÔN CÓ** | Email chuẩn hóa nhận yêu cầu (`"user@example.com"`) |
| `expiresInSeconds` | `integer` | 🟢 **LUÔN CÓ** | Thời gian OTP có hiệu lực: `300` (giây) = 5 phút |
| `cooldownSeconds` | `integer` | 🟢 **LUÔN CÓ** | Thời gian chờ gửi lại tối thiểu: `60` (giây) |
| `message` | `string` | 🟢 **LUÔN CÓ** | Thông báo an toàn: `"Nếu email tồn tại trong hệ thống và đã được kích hoạt, mã xác thực đặt lại mật khẩu đã được gửi đến email của bạn."` |

```json
{
  "email": "user@example.com",
  "expiresInSeconds": 300,
  "cooldownSeconds": 60,
  "message": "Nếu email tồn tại trong hệ thống và đã được kích hoạt, mã xác thực đặt lại mật khẩu đã được gửi đến email của bạn."
}
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết (Error Responses)

| HTTP Status | Error Code (`errorCode`) | Header đính kèm | Message mẫu trả về | Hành động gợi ý cho Frontend |
| :---: | :--- | :---: | :--- | :--- |
| `400 Bad Request` | `validation_error` | *None* | `"Validation failed"` | Báo lỗi input Email không đúng định dạng. |
| `429 Too Many Requests` | `identity.reset_cooldown_active` | `Retry-After: <giây>` | `"Vui lòng đợi XX giây trước khi yêu cầu mã mới. Mã đã gửi trước đó vẫn dùng được nếu chưa hết hạn."` | Đọc header `Retry-After` để cập nhật đồng hồ đếm ngược Cooldown. Khóa nút gửi. |
| `429 Too Many Requests` | `identity.reset_rate_limit_exceeded` | `Retry-After: <giây>` | `"Email này đã nhận tối đa 5 mã đặt lại mật khẩu trong 1 giờ. Vui lòng thử lại sau XX phút."` | Khóa chức năng, hiển thị thông báo thời gian mở lại. |

---

### 2.2. Đặt Lại Mật Khẩu Bằng OTP (Reset Password)

Xác thực mã OTP nhận qua email và cập nhật mật khẩu mới cho tài khoản.

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts/reset-password`
- **Quyền truy cập (Auth):** `Public` (Không yêu cầu đăng nhập)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị |
| :--- | :---: | :--- |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` |
| `Accept` | 🟢 **KHUYẾN NGHỊ** | `application/json` |
| `Authorization` | ❌ **KHÔNG GỬI** | *None* |

#### 📥 Request Body Schema (Phân Định Rõ Ràng)

| Tên trường (Field) | Kiểu dữ liệu | Phân loại | Giá trị mặc định | Ràng buộc Validation & Quy tắc kiểm tra (Source of Truth) |
| :--- | :---: | :---: | :---: | :--- |
| `email` | `string` | 🔴 **BẮT BUỘC** | *Không có* | Email của tài khoản cần đặt lại mật khẩu. Tối đa 254 ký tự. Không được để trống. |
| `otpCode` | `string` | 🔴 **BẮT BUỘC** | *Không có* | Đúng **6 chữ số ASCII** (`^[0-9]{6}$`). |
| `newPassword` | `string` | 🔴 **BẮT BUỘC** | *Không có* | - Tối thiểu 8 ký tự, tối đa 72 bytes UTF-8.<br>- Bắt buộc đủ 4 tiêu chí: chữ hoa, chữ thường, chữ số, ký tự đặc biệt.<br>- **Không được trùng với mật khẩu hiện tại** của tài khoản. |

#### 📝 Request Body Mẫu
```json
{
  "email": "user@example.com",
  "otpCode": "123456",
  "newPassword": "NewSecurePassword456!"
}
```

#### 📤 Phản Hồi Thành Công (`200 OK`)

| Trường (Field) | Kiểu dữ liệu | Phân loại | Ý nghĩa & Giá trị |
| :--- | :---: | :---: | :--- |
| `message` | `string` | 🟢 **LUÔN CÓ** | `"Đặt lại mật khẩu thành công. Tất cả các phiên đăng nhập khác đã được đăng xuất. Bạn có thể đăng nhập bằng mật khẩu mới."` |

```json
{
  "message": "Đặt lại mật khẩu thành công. Tất cả các phiên đăng nhập khác đã được đăng xuất. Bạn có thể đăng nhập bằng mật khẩu mới."
}
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết (Error Responses)

| HTTP Status | Error Code (`errorCode`) | Nguyên nhân cụ thể | Message mẫu trả về | Hành động gợi ý cho Frontend |
| :---: | :--- | :--- | :--- | :--- |
| `400 Bad Request` | `identity.invalid_otp_code` | Mã OTP đặt lại mật khẩu không chính xác. | `"Mã OTP đặt lại mật khẩu không chính xác. Vui lòng kiểm tra lại."` | Xóa trắng ô OTP, giữ nguyên mật khẩu mới, focus lại ô OTP. |
| `400 Bad Request` | `identity.otp_expired` | Mã OTP đã quá thời hạn 5 phút. | `"Mã OTP đặt lại mật khẩu đã hết hạn hoặc không còn hiệu lực. Vui lòng yêu cầu mã mới."` | Hiển thị thông báo mã hết hạn, yêu cầu quay lại bước gửi mã. |
| `400 Bad Request` | `identity.otp_max_attempts_exceeded` | Nhập sai mã OTP **3 lần liên tiếp** (`MaxAttempts = 3`). | `"Bạn đã nhập sai mã OTP 3 lần nên mã này không còn dùng được. Vui lòng gửi lại yêu cầu đặt lại mật khẩu."` | Khóa form, yêu cầu người dùng quay lại màn hình Forgot Password. |
| `400 Bad Request` | `identity.password_same_as_current` | Mật khẩu mới trùng hoàn toàn với mật khẩu hiện tại. | `"Mật khẩu mới phải khác mật khẩu hiện tại."` | Focus ô Mật khẩu mới và báo lỗi trùng mật khẩu. |

---

### 2.3. Đổi Mật Khẩu Tài Khoản Hiện Tại (Change Password)

Người dùng đã đăng nhập chủ động thay đổi mật khẩu bằng cách xác nhận mật khẩu hiện tại.

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts/change-password`
- **Quyền truy cập (Auth):** 🔴 **BẮT BUỘC ACCESS TOKEN** (`RequireAuthorization()`)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị mẫu | Giải thích |
| :--- | :---: | :--- | :--- |
| `Authorization` | 🔴 **BẮT BUỘC** | `Bearer eyJhbGciOiJSUz...` | Access Token của tài khoản đang đăng nhập |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` | Định dạng payload gửi lên |
| `Accept` | 🟢 **KHUYẾN NGHỊ** | `application/json` | Định dạng phản hồi mong muốn |

#### 📥 Request Body Schema (Phân Định Rõ Ràng)

| Tên trường (Field) | Kiểu dữ liệu | Phân loại | Giá trị mặc định | Ràng buộc Validation & Quy tắc kiểm tra (Source of Truth) |
| :--- | :---: | :---: | :---: | :--- |
| `currentPassword` | `string` | 🔴 **BẮT BUỘC** | *Không có* | Mật khẩu hiện tại của tài khoản. Tối đa 72 bytes UTF-8. Không được để trống. |
| `newPassword` | `string` | 🔴 **BẮT BUỘC** | *Không có* | - Mật khẩu mới cần đổi.<br>- Tối thiểu 8 ký tự, tối đa 72 bytes UTF-8.<br>- Bắt buộc đủ 4 tiêu chí (chữ hoa, chữ thường, số, ký tự đặc biệt).<br>- **Phải khác mật khẩu hiện tại** (`NotEqual(currentPassword)`). |

#### 📝 Request Body Mẫu
```json
{
  "currentPassword": "CurrentPassword123!",
  "newPassword": "BrandNewPassword789!"
}
```

#### 📤 Phản Hồi Thành Công (`200 OK`)
*Khi đổi thành công, Backend tự động thu hồi toàn bộ phiên và token trên mọi thiết bị.*

| Trường (Field) | Kiểu dữ liệu | Phân loại | Ý nghĩa & Giá trị |
| :--- | :---: | :---: | :--- |
| `message` | `string` | 🟢 **LUÔN CÓ** | `"Đổi mật khẩu thành công. Bạn đã được đăng xuất khỏi tất cả thiết bị, vui lòng đăng nhập lại bằng mật khẩu mới."` |

```json
{
  "message": "Đổi mật khẩu thành công. Bạn đã được đăng xuất khỏi tất cả thiết bị, vui lòng đăng nhập lại bằng mật khẩu mới."
}
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết (Error Responses)

| HTTP Status | Error Code (`errorCode`) | Header đính kèm | Message mẫu trả về | Hành động gợi ý cho Frontend |
| :---: | :--- | :---: | :--- | :--- |
| `400 Bad Request` | `identity.invalid_current_password` | *None* | `"Mật khẩu hiện tại không chính xác. Bạn còn X lần thử trước khi đăng nhập bị tạm khóa 15 phút..."` | Báo lỗi ô Mật khẩu hiện tại kèm số lần thử còn lại (3 - FailedAttempts). |
| `400 Bad Request` | `identity.password_same_as_current` | *None* | `"Mật khẩu mới phải khác mật khẩu hiện tại."` | Báo lỗi ô Mật khẩu mới. |
| `401 Unauthorized` | *Standard HTTP 401* | *None* | `"Unauthorized"` | Phiên làm việc đã hết hạn. Kích hoạt Silent Refresh hoặc đưa về trang Login. |
| `423 Locked` | `identity.account_temporarily_locked` | `Retry-After: <giây>` | `"Tài khoản tạm thời bị khóa do nhập sai mật khẩu 3 lần liên tiếp..."` | Khóa form, đọc header `Retry-After` và hiển thị đồng hồ đếm ngược 15 phút. |

---

## 3. 🚨 Định Dạng Lỗi Chuẩn Toàn Hệ Thống (RFC 7807 ProblemDetails)

Tất cả các API khi gặp lỗi đều trả về cấu trúc ProblemDetails chuẩn:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Mật khẩu hiện tại không chính xác.",
  "status": 400,
  "detail": "Mật khẩu hiện tại không chính xác. Bạn còn 2 lần thử trước khi đăng nhập bị tạm khóa 15 phút trên mọi thiết bị.",
  "errorCode": "identity.invalid_current_password"
}
```

---

## 4. 💻 Khai Báo TypeScript & Hàm Gọi API Mẫu (Frontend SDK)

### 4.1. TypeScript Interface Definitions

```typescript
// ==========================================
// 1. REQUEST INTERFACES
// ==========================================

export interface ForgotPasswordRequest {
  // 🔴 [BẮT BUỘC]
  email: string;
}

export interface ResetPasswordRequest {
  // 🔴 [BẮT BUỘC]
  email: string;
  otpCode: string;     // Đúng 6 chữ số: ^[0-9]{6}$
  newPassword: string; // Tối thiểu 8 ký tự, tối đa 72 bytes
}

export interface ChangePasswordRequest {
  // 🔴 [BẮT BUỘC]
  currentPassword: string;
  newPassword: string; // Phải khác currentPassword
}

// ==========================================
// 2. RESPONSE INTERFACES
// ==========================================

export interface ForgotPasswordResponse {
  email: string;              // 🟢 [LUÔN CÓ]
  expiresInSeconds: number;   // 🟢 [LUÔN CÓ: 300]
  cooldownSeconds: number;    // 🟢 [LUÔN CÓ: 60]
  message: string;            // 🟢 [LUÔN CÓ]
}

export interface PasswordActionResponse {
  message: string;            // 🟢 [LUÔN CÓ]
}

export interface ApiProblemDetails {
  status: number;
  title: string;
  detail: string;
  errorCode: string;
}
```

### 4.2. Axios Service Functions

```typescript
import { api } from './auth'; // Sử dụng instance Axios đã cấu hình withCredentials và Interceptor

export const passwordService = {
  /**
   * 1. Yêu cầu gửi mã OTP đặt lại mật khẩu qua email
   */
  async forgotPassword(payload: ForgotPasswordRequest): Promise<ForgotPasswordResponse> {
    const response = await api.post<ForgotPasswordResponse>('/api/identity/accounts/forgot-password', payload);
    return response.data;
  },

  /**
   * 2. Đặt lại mật khẩu mới bằng mã OTP
   */
  async resetPassword(payload: ResetPasswordRequest): Promise<PasswordActionResponse> {
    const response = await api.post<PasswordActionResponse>('/api/identity/accounts/reset-password', payload);
    return response.data;
  },

  /**
   * 3. Đổi mật khẩu tài khoản hiện tại (Yêu cầu Access Token)
   */
  async changePassword(payload: ChangePasswordRequest): Promise<PasswordActionResponse> {
    const response = await api.post<PasswordActionResponse>('/api/identity/accounts/change-password', payload);
    return response.data;
  },
};
```

---

## 5. 🎯 Checklist UI/UX Frontend Màn Hình Quản Lý Mật Khẩu

- [ ] **Màn hình Quên mật khẩu (`/forgot-password`):**
  - Validation client-side định dạng Email trước khi bấm gửi.
  - Vô hiệu hóa nút *"Gửi mã"* và hiển thị spinner trong khi request đang gửi.
  - Sau khi nhận `200 OK`: Lưu `email` vào State/Query param và chuyển ngay sang trang `/reset-password?email=...`.
- [ ] **Màn hình Đặt lại mật khẩu (`/reset-password`):**
  - Component 6 ô nhập OTP: Tự động nhảy ô, tự động paste từ clipboard.
  - Ô Mật khẩu mới: Checklist trực quan 4 tiêu chí mật khẩu (8 ký tự, chữ hoa, thường, số/ký tự đặc biệt).
  - Thêm ô *"Xác nhận mật khẩu mới"* trên Frontend (Client-side validation) để đảm bảo người dùng không gõ nhầm trước khi submit.
  - Khi đổi mật khẩu thành công (`200`): Hiển thị Modal/Toast thông báo thành công và chuyển hướng về màn hình `/login` sau 2 giây.
- [ ] **Trang Cài đặt Đổi mật khẩu (`/profile/security` hoặc `/settings/password`):**
  - Có icon mắt (Toggle Show/Hide Password) cho cả 2 ô Mật khẩu hiện tại và Mật khẩu mới.
  - Cảnh báo trực tiếp: *"Đổi mật khẩu sẽ đăng xuất phiên làm việc của bạn trên mọi thiết bị khác."*
  - Nếu gặp lỗi `identity.invalid_current_password`: Hiển thị rõ số lần thử còn lại trước khi tài khoản bị khóa 15 phút.
  - Sau khi đổi thành công: Xóa `memoryAccessToken`, xóa Cookie (nếu có), đăng xuất và đưa người dùng về trang Đăng nhập.
