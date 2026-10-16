# Đặc Tả Giao Diện API: Đăng Ký & Kích Hoạt Tài Khoản (Registration & Verification)

> **Dịch vụ:** Identity Service (`ParkWise.Identity.WebAPI`)  
> **Phiên bản:** v1.1.0 (Đối soát 100% mã nguồn Backend & Domain Rules)  
> **Trạng thái:** Sẵn sàng tích hợp (Production-Ready)  
> **Đối tượng sử dụng:** Frontend Web (`web/`), Mobile App, QA/Tester  
> **Base URL:** `/api/identity/accounts` (Trực tiếp: `http://localhost:5001/api/identity/accounts` | Qua Gateway: `http://localhost:5000/api/identity/accounts`)

---

## 1. 📌 Bối Cảnh & Luồng Nghiệp Vụ (User Journey Flow)

Nhóm API này xử lý toàn bộ quy trình khởi tạo tài khoản mới:
1. **Bước 1 - Điền biểu mẫu**: Người dùng nhập Email, Mật khẩu (bắt buộc) cùng Họ tên, Số điện thoại (tùy chọn).
2. **Bước 2 - Tạo tài khoản & Gửi OTP**: Backend tạo tài khoản ở trạng thái `PendingVerification`, đồng thời phát sinh mã OTP 6 chữ số có hiệu lực **5 phút (300 giây)** gửi qua RabbitMQ để Notification Service bắn email cho người dùng.
3. **Bước 3 - Xác thực OTP**: Người dùng nhập mã OTP để kích hoạt tài khoản sang trạng thái `Active`.
   - **Giới hạn nhập sai:** Tối đa **3 lần** (`MaxAttempts = 3`). Nhập sai quá 3 lần, mã OTP lập tức bị vô hiệu hóa để chống dò mã.
4. **Cơ chế gửi lại OTP (Resend)**: Có thời gian chờ **Cooldown 60 giây** giữa 2 lần gửi và **tối đa 5 mã OTP/giờ** (`MaxHourlyOtpIssued = 5`) trên mỗi email.

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng
    participant FE as Frontend Client
    participant API as Identity API
    participant Mail as Notification Service

    User->>FE: 1. Nhập Form Đăng Ký
    FE->>API: POST /api/identity/accounts
    alt Email hoặc SĐT đã tồn tại
        API-->>FE: 409 Conflict (identity.email_already_registered)
        FE-->>User: Báo lỗi & gợi ý đăng nhập / quên mật khẩu
    else Dữ liệu hợp lệ
        API->>Mail: Event: UserVerificationOtpIssued
        Mail-->>User: Gửi email chứa OTP 6 chữ số (hiệu lực 5 phút)
        API-->>FE: 201 Created (RegisteredAccount)
        FE->>FE: Lưu email vào State & chuyển sang trang Nhập OTP
    end

    User->>FE: 2. Nhập mã OTP 6 chữ số
    FE->>API: POST /api/identity/accounts/verify-email
    alt OTP sai (dưới 3 lần)
        API-->>FE: 400 Bad Request (identity.invalid_otp_code)
        FE-->>User: Báo mã sai & cho phép nhập lại
    alt OTP sai liên tiếp 3 lần
        API-->>FE: 400 Bad Request (identity.otp_max_attempts_exceeded)
        FE-->>User: Khóa form & yêu cầu bấm "Gửi lại mã mới"
    else OTP chính xác
        API-->>FE: 200 OK (VerifiedEmailAccount)
        FE-->>User: Thông báo thành công & Chuyển sang màn hình Đăng nhập
    end

    opt Khi người dùng cần gửi lại mã
        User->>FE: Bấm "Gửi lại mã OTP"
        FE->>API: POST /api/identity/accounts/resend-otp
        alt Đang trong thời gian chờ (Cooldown < 60s)
            API-->>FE: 429 Too Many Requests (Header Retry-After: XX)
            FE-->>User: Đếm ngược thời gian còn lại
        else Hợp lệ
            API-->>FE: 200 OK (ResentOtpResult)
            FE-->>User: Bắt đầu đếm ngược 60s mới
        end
    end
```

---

## 2. 🌐 Đặc Tả Chi Tiết Từng Endpoint

---

### 2.1. Đăng Ký Tài Khoản Mới (Register Account)

Tạo tài khoản người dùng mới với vai trò mặc định `Customer`.

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts`
- **Quyền truy cập (Auth):** `Public` (Không yêu cầu đăng nhập)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị | Giải thích |
| :--- | :---: | :--- | :--- |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` | Định dạng dữ liệu gửi lên |
| `Accept` | 🟢 **KHUYẾN NGHỊ** | `application/json` | Định dạng phản hồi mong muốn |
| `Authorization` | ❌ **KHÔNG GỬI** | *None* | Endpoint công khai, không gửi Bearer token |

#### 📥 Request Body Schema (Phân Định Rõ Ràng Từng Trường)

| Tên trường (Field) | Kiểu dữ liệu | Phân loại | Giá trị mặc định | Ràng buộc Validation & Quy tắc kiểm tra (Source of Truth) |
| :--- | :---: | :---: | :---: | :--- |
| `email` | `string` | 🔴 **BẮT BUỘC** | *Không có* | - Không được để trống hoặc `null`.<br>- Định dạng email hợp lệ RFC 5322.<br>- Độ dài tối đa: **254 ký tự**.<br>- Hệ thống tự động chuẩn hóa: `Trim()` và chuyển về chữ thường (`ToLowerInvariant`).<br>- *Lỗi vi phạm:* `"Email là bắt buộc, không được để trống."` / `"Email không đúng định dạng hợp lệ."` |
| `password` | `string` | 🔴 **BẮT BUỘC** | *Không có* | - Không được để trống hoặc `null`.<br>- Độ dài: **Ít nhất 8 ký tự** và **Tối đa 72 bytes UTF-8** (giới hạn bảo mật BCrypt).<br>- Phải thỏa mãn đủ 4 điều kiện regex:<br>&nbsp;&nbsp;1. Ít nhất 1 chữ hoa (`[A-Z]`)<br>&nbsp;&nbsp;2. Ít nhất 1 chữ thường (`[a-z]`)<br>&nbsp;&nbsp;3. Ít nhất 1 chữ số (`[0-9]`)<br>&nbsp;&nbsp;4. Ít nhất 1 ký tự đặc biệt (`[^A-Za-z0-9]`)<br>- *Lỗi vi phạm:* `"Mật khẩu phải có ít nhất 8 ký tự."` / `"Mật khẩu quá dài: tối đa 72 ký tự không dấu..."` |
| `fullName` | `string` hoặc `null` | 🟡 **TÙY CHỌN** | `null` | - Cho phép bỏ qua không truyền, truyền `null`, hoặc truyền chuỗi rỗng `""`.<br>- **Nếu có truyền giá trị:** Độ dài từ **2 đến 100 ký tự**; Regex: `^[\p{L}]+(?:[ '-][\p{L}]+)*$` (Chỉ chứa chữ cái tiếng Việt có dấu, chữ cái tiếng Anh, khoảng trắng, gạch nối `-`, nháy đơn `'`). Không chứa số hoặc ký tự đặc biệt khác.<br>- *Hành vi khi bỏ qua:* Tài khoản vẫn tạo thành công, hồ sơ cá nhân (Profile) sẽ được tạo sau khi người dùng bổ sung ở trang Profile. |
| `phoneNumber` | `string` hoặc `null` | 🟡 **TÙY CHỌN** | `null` | - Cho phép bỏ qua không truyền, truyền `null`, hoặc truyền chuỗi rỗng `""`.<br>- **Nếu có truyền giá trị:** Bắt buộc là số di động Việt Nam: Regex `^\s*(0|\+84)[35789][0-9]{8}\s*$` (10 chữ số, bắt đầu bằng `0` hoặc `+84`, chữ số tiếp theo là `3, 5, 7, 8, 9`).<br>- *Lỗi vi phạm:* `"Số điện thoại phải là số di động Việt Nam: 0xxxxxxxxx (10 chữ số) hoặc +84xxxxxxxxx, chữ số sau 0/+84 là 3, 5, 7, 8 hoặc 9."` |

#### 📝 Request Body Mẫu

##### Trường hợp 1: Đầy đủ tất cả các trường (Cả bắt buộc và tùy chọn)
```json
{
  "fullName": "Nguyễn Văn A",
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "phoneNumber": "0912345678"
}
```

##### Trường hợp 2: Chỉ truyền các trường Bắt Buộc (Tối giản)
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

#### 📤 Phản Hồi Thành Công (`201 Created`)

| Trường (Field) | Kiểu dữ liệu | Phân loại | Ý nghĩa & Giá trị mẫu |
| :--- | :---: | :---: | :--- |
| `accountId` | `string (UUID)` | 🟢 **LUÔN CÓ** | Định danh duy nhất của tài khoản (`"a3bb108e-17d4-4a6c-9f87-2a54e9912345"`) |
| `email` | `string` | 🟢 **LUÔN CÓ** | Email chuẩn hóa đã đăng ký (`"user@example.com"`) |
| `fullName` | `string` hoặc `null` | ⚪ **CÓ THỂ NULL** | Họ tên nếu có nhập, hoặc `null` nếu bỏ qua |
| `status` | `string` | 🟢 **LUÔN CÓ** | Trạng thái hiện tại: `"PendingVerification"` |
| `message` | `string` | 🟢 **LUÔN CÓ** | `"Đăng ký thành công. Mã OTP đã được gửi đến user@example.com, có hiệu lực 5 phút."` |

```json
{
  "accountId": "a3bb108e-17d4-4a6c-9f87-2a54e9912345",
  "email": "user@example.com",
  "fullName": "Nguyễn Văn A",
  "status": "PendingVerification",
  "message": "Đăng ký thành công. Mã OTP đã được gửi đến user@example.com, có hiệu lực 5 phút."
}
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết (Error Responses)

| HTTP Status | Error Code (`errorCode`) | Nguyên nhân cụ thể | Message mẫu trả về | Hành động gợi ý cho Frontend |
| :---: | :--- | :--- | :--- | :--- |
| `400 Bad Request` | `validation_error` | Vi phạm ràng buộc dữ liệu (email sai format, password thiếu hoa/thường/số/ký tự đặc biệt, SĐT sai đầu số). | `"Validation failed"` kèm mảng chi tiết | Tô đỏ input vi phạm, hiển thị thông báo lỗi dưới ô nhập. |
| `409 Conflict` | `identity.email_already_registered` | Email này đã tồn tại trong hệ thống. | `"Email này đã được đăng ký. Vui lòng đăng nhập, hoặc dùng 'Quên mật khẩu'..."` | Hiển thị thông báo Toast cảnh báo + Nút điều hướng nhanh sang trang Đăng nhập. |
| `409 Conflict` | `identity.phone_already_registered` | Số điện thoại đã được dùng cho tài khoản khác. | `"Số điện thoại này đã được đăng ký cho một tài khoản khác."` | Focus vào ô Số điện thoại và báo lỗi trùng lặp. |

---

### 2.2. Xác Thực Email Bằng Mã OTP (Verify Email OTP)

Kích hoạt tài khoản từ `PendingVerification` sang `Active`.

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts/verify-email`
- **Quyền truy cập (Auth):** `Public` (Không yêu cầu đăng nhập)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị |
| :--- | :---: | :--- |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` |
| `Accept` | 🟢 **KHUYẾN NGHỊ** | `application/json` |
| `Authorization` | ❌ **KHÔNG GỬI** | *None* |

#### 📥 Request Body Schema

| Tên trường (Field) | Kiểu dữ liệu | Phân loại | Ràng buộc Validation & Quy tắc kiểm tra |
| :--- | :---: | :---: | :--- |
| `email` | `string` | 🔴 **BẮT BUỘC** | Địa chỉ email của tài khoản cần kích hoạt. Tối đa 254 ký tự. Không được để trống. |
| `otpCode` | `string` | 🔴 **BẮT BUỘC** | Đúng **6 chữ số ASCII** (`^[0-9]{6}$`). Không chứa chữ cái hay khoảng trắng. |

#### 📝 Request Body Mẫu
```json
{
  "email": "user@example.com",
  "otpCode": "123456"
}
```

#### 📤 Phản Hồi Thành Công (`200 OK`)

| Trường (Field) | Kiểu dữ liệu | Phân loại | Ý nghĩa & Giá trị |
| :--- | :---: | :---: | :--- |
| `accountId` | `string (UUID)` | 🟢 **LUÔN CÓ** | Định danh tài khoản đã kích hoạt |
| `email` | `string` | 🟢 **LUÔN CÓ** | Địa chỉ email của tài khoản |
| `status` | `string` | 🟢 **LUÔN CÓ** | Trạng thái mới: `"Active"` |
| `isVerified` | `boolean` | 🟢 **LUÔN CÓ** | `true` |
| `message` | `string` | 🟢 **LUÔN CÓ** | `"Xác thực email thành công. Bạn có thể đăng nhập ngay."` |

```json
{
  "accountId": "a3bb108e-17d4-4a6c-9f87-2a54e9912345",
  "email": "user@example.com",
  "status": "Active",
  "isVerified": true,
  "message": "Xác thực email thành công. Bạn có thể đăng nhập ngay."
}
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết (Error Responses)

| HTTP Status | Error Code (`errorCode`) | Nguyên nhân cụ thể | Message mẫu trả về | Hành động gợi ý cho Frontend |
| :---: | :--- | :--- | :--- | :--- |
| `400 Bad Request` | `identity.invalid_otp_code` | Mã OTP nhập vào không khớp (số lần sai còn < 3). | `"Mã OTP không chính xác. Vui lòng kiểm tra lại."` | Xóa trắng 6 ô OTP, hiệu ứng rung form (shake) và focus lại ô đầu tiên. |
| `400 Bad Request` | `identity.otp_expired` | Mã OTP đã quá 5 phút hoặc tài khoản đã kích hoạt rồi. | `"Mã OTP đã hết hạn hoặc không còn hiệu lực. Vui lòng yêu cầu mã OTP mới."` | Báo lỗi hết hạn + Hiển thị nổi bật nút *"Gửi lại mã OTP"*. |
| `400 Bad Request` | `identity.otp_max_attempts_exceeded` | Người dùng đã nhập sai **3 lần liên tiếp** (`MaxAttempts = 3`). Mã OTP này bị hủy ngay lập tức. | `"Bạn đã nhập sai mã OTP 3 lần nên mã này không còn dùng được. Vui lòng yêu cầu mã mới."` | Vô hiệu hóa 6 ô nhập, yêu cầu người dùng bắt buộc bấm nút *"Gửi lại mã OTP"* để nhận mã mới. |

---

### 2.3. Gửi Lại Mã OTP (Resend Email OTP)

Phát hành mã OTP 6 chữ số mới khi mã cũ hết hạn, nhập sai 3 lần hoặc bị thất lạc email.

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts/resend-otp`
- **Quyền truy cập (Auth):** `Public` (Không yêu cầu đăng nhập)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị |
| :--- | :---: | :--- |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` |
| `Accept` | 🟢 **KHUYẾN NGHỊ** | `application/json` |
| `Authorization` | ❌ **KHÔNG GỬI** | *None* |

#### 📥 Request Body Schema

| Tên trường (Field) | Kiểu dữ liệu | Phân loại | Ràng buộc Validation & Quy tắc kiểm tra |
| :--- | :---: | :---: | :--- |
| `email` | `string` | 🔴 **BẮT BUỘC** | Địa chỉ email của tài khoản đang ở trạng thái `PendingVerification`. Tối đa 254 ký tự. Không được để trống. |

#### 📝 Request Body Mẫu
```json
{
  "email": "user@example.com"
}
```

#### 📤 Phản Hồi Thành Công (`200 OK`)

| Trường (Field) | Kiểu dữ liệu | Phân loại | Ý nghĩa & Giá trị |
| :--- | :---: | :---: | :--- |
| `email` | `string` | 🟢 **LUÔN CÓ** | Email vừa nhận mã mới |
| `expiresInSeconds` | `integer` | 🟢 **LUÔN CÓ** | Thời gian OTP có hiệu lực: `300` (giây) = 5 phút |
| `cooldownSeconds` | `integer` | 🟢 **LUÔN CÓ** | Thời gian chờ tối thiểu trước lần gửi kế tiếp: `60` (giây) |
| `message` | `string` | 🟢 **LUÔN CÓ** | `"Đã gửi mã OTP mới đến user@example.com. Mã có hiệu lực 5 phút; bạn có thể yêu cầu gửi lại sau 60 giây."` |
| `otpSent` | `boolean` | 🟢 **LUÔN CÓ** | `true` |

```json
{
  "email": "user@example.com",
  "expiresInSeconds": 300,
  "cooldownSeconds": 60,
  "message": "Đã gửi mã OTP mới đến user@example.com. Mã có hiệu lực 5 phút; bạn có thể yêu cầu gửi lại sau 60 giây.",
  "otpSent": true
}
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết (Error Responses)

| HTTP Status | Error Code (`errorCode`) | Header đính kèm | Message mẫu trả về | Hành động gợi ý cho Frontend |
| :---: | :--- | :---: | :--- | :--- |
| `429 Too Many Requests` | `identity.resend_cooldown_active` | `Retry-After: <giây>` | `"Vui lòng đợi XX giây trước khi yêu cầu mã mới. Mã đã gửi trước đó vẫn dùng được nếu chưa hết hạn."` | Đọc số giây từ header `Retry-After` để reset đồng hồ đếm ngược trên UI. Vô hiệu hóa nút Resend. |
| `429 Too Many Requests` | `identity.resend_rate_limit_exceeded` | `Retry-After: <giây>` | `"Email này đã nhận tối đa 5 mã OTP trong 1 giờ... Vui lòng thử lại sau XX phút."` | Khóa tính năng gửi lại, hiển thị thông báo thời gian mở khóa cho người dùng. |
| `400 Bad Request` | `identity.account_not_pending` | *None* | `"Email này chưa được đăng ký hoặc tài khoản đã được xác thực trước đó."` | Hiển thị thông báo và chuyển hướng người dùng sang trang Đăng nhập. |

---

## 3. 🚨 Định Dạng Lỗi Chuẩn Toàn Hệ Thống (RFC 7807 ProblemDetails)

Mọi phản hồi lỗi (`400`, `409`, `429`) đều tuân theo chuẩn RFC 7807:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Mã OTP không chính xác. Vui lòng kiểm tra lại.",
  "status": 400,
  "detail": "Mã OTP không chính xác. Vui lòng kiểm tra lại.",
  "errorCode": "identity.invalid_otp_code"
}
```

### Quy tắc xử lý cho Frontend:
1. **Rẽ nhánh nghiệp vụ (Logic Branching):** Luôn kiểm tra `error.response.data.errorCode`. Tuyệt đối không so sánh bằng chuỗi text `detail` vì text có thể thay đổi ngôn ngữ.
2. **Hiển thị thông báo UI (Toast/Alert):** Sử dụng `error.response.data.detail` hoặc `error.response.data.title` để hiển thị trực tiếp cho người dùng.
3. **Đọc Header `Retry-After`:** Khi gặp status `429`, luôn đọc `error.response.headers['retry-after']` để lấy chính xác số giây cần khóa nút bấm.

---

## 4. 💻 Khai Báo TypeScript & Hàm Gọi API Mẫu (Frontend SDK)

### 4.1. TypeScript Types (Phân định rõ Required vs Optional)

```typescript
// ==========================================
// 1. REQUEST TYPES
// ==========================================

export interface RegisterAccountRequest {
  // 🔴 [BẮT BUỘC]
  email: string;
  password: string;

  // 🟡 [TÙY CHỌN] (Có thể bỏ qua, truyền null hoặc undefined)
  fullName?: string | null;
  phoneNumber?: string | null;
}

export interface VerifyEmailRequest {
  // 🔴 [BẮT BUỘC]
  email: string;
  otpCode: string; // Đúng 6 chữ số ASCII: ^[0-9]{6}$
}

export interface ResendOtpRequest {
  // 🔴 [BẮT BUỘC]
  email: string;
}

// ==========================================
// 2. RESPONSE TYPES
// ==========================================

export interface RegisteredAccountResponse {
  accountId: string;          // 🟢 [LUÔN CÓ] UUID
  email: string;              // 🟢 [LUÔN CÓ]
  fullName: string | null;    // ⚪ [CÓ THỂ NULL]
  status: 'PendingVerification' | string; // 🟢 [LUÔN CÓ]
  message: string;            // 🟢 [LUÔN CÓ]
}

export interface VerifiedEmailResponse {
  accountId: string;          // 🟢 [LUÔN CÓ] UUID
  email: string;              // 🟢 [LUÔN CÓ]
  status: 'Active' | string;  // 🟢 [LUÔN CÓ]
  isVerified: boolean;        // 🟢 [LUÔN CÓ: true]
  message: string;            // 🟢 [LUÔN CÓ]
}

export interface ResentOtpResponse {
  email: string;              // 🟢 [LUÔN CÓ]
  expiresInSeconds: number;   // 🟢 [LUÔN CÓ: 300]
  cooldownSeconds: number;    // 🟢 [LUÔN CÓ: 60]
  message: string;            // 🟢 [LUÔN CÓ]
  otpSent: boolean;           // 🟢 [LUÔN CÓ: true/false]
}

export interface ProblemDetailsError {
  status: number;
  title: string;
  detail: string;
  errorCode: string;
}
```

### 4.2. Axios Service Functions

```typescript
import axios, { AxiosError } from 'axios';

const apiClient = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000',
  headers: {
    'Content-Type': 'application/json',
  },
});

export const registrationService = {
  /**
   * Đăng ký tài khoản người dùng mới
   */
  async register(payload: RegisterAccountRequest): Promise<RegisteredAccountResponse> {
    const response = await apiClient.post<RegisteredAccountResponse>('/api/identity/accounts', payload);
    return response.data;
  },

  /**
   * Xác thực tài khoản bằng mã OTP 6 chữ số
   */
  async verifyEmail(payload: VerifyEmailRequest): Promise<VerifiedEmailResponse> {
    const response = await apiClient.post<VerifiedEmailResponse>('/api/identity/accounts/verify-email', payload);
    return response.data;
  },

  /**
   * Gửi lại mã OTP xác thực email
   */
  async resendOtp(payload: ResendOtpRequest): Promise<ResentOtpResponse> {
    const response = await apiClient.post<ResentOtpResponse>('/api/identity/accounts/resend-otp', payload);
    return response.data;
  },
};
```

---

## 5. 🎯 Checklist UI/UX Frontend Chuẩn Thương Phẩm

- [ ] **Tại Form Đăng Ký (`/register`):**
  - Đánh dấu hoa thị đỏ `*` rõ ràng cho 2 trường bắt buộc: `Email *` và `Mật khẩu *`.
  - Ghi nhãn `(Không bắt buộc)` cho 2 trường: `Họ và tên` và `Số điện thoại`.
  - Realtime password strength meter: Hiển thị checklist xanh/đỏ cho 4 tiêu chí (ít nhất 8 ký tự, tối đa 72 bytes, 1 hoa, 1 thường, 1 số, 1 ký tự đặc biệt).
  - Vô hiệu hóa nút *"Đăng ký"* khi form chưa hợp lệ hoặc khi request đang gửi (Loading spinner chống bấm đúp).
  - Đăng ký thành công (`201`): Lưu `email` vào State/URL param và điều hướng ngay sang trang `/verify-otp?email=...`.

- [ ] **Tại Màn hình Nhập OTP (`/verify-otp`):**
  - Hiển thị email nhận mã: *"Mã OTP đã được gửi đến user@example.com"*.
  - Component 6 ô nhập: Tự động nhảy ô tiếp theo khi gõ số, tự động lùi ô khi bấm Backspace, tự động chia tách khi người dùng Paste 6 chữ số từ Clipboard.
  - Tự động gọi API `verifyEmail` ngay khi người dùng điền đủ chữ số thứ 6.
  - Đồng hồ đếm ngược Cooldown: Khởi tạo đếm ngược `60s`. Trong lúc đếm ngược, nút *"Gửi lại mã (XXs)"* bị disabled. Khi về 0, nút sáng lên thành *"Gửi lại mã OTP"*.
  - Khi gặp lỗi `identity.otp_max_attempts_exceeded`: Vô hiệu hóa 6 ô nhập, hiển thị thông báo đã nhập sai 3 lần và bắt buộc bấm *"Gửi lại mã OTP"* để nhận mã mới.
