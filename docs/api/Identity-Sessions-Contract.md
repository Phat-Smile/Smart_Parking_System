# Đặc Tả Giao Diện API: Quản Lý Phiên & Thiết Bị Đăng Nhập (Session & Device Management)

> **Dịch vụ:** Identity Service (`ParkWise.Identity.WebAPI`)  
> **Phiên bản:** v1.0.0 (Chuẩn hóa tường minh cho Frontend - Đối soát 100% Backend)  
> **Trạng thái:** Sẵn sàng tích hợp (Production-Ready)  
> **Đối tượng sử dụng:** Frontend Web (`web/`), Mobile App, QA/Tester  
> **Base URL:** `/api/identity/accounts` (Trực tiếp: `http://localhost:5001/api/identity/accounts` | Qua Gateway: `http://localhost:5000/api/identity/accounts`)

---

## 1. 📌 Bối Cảnh & Kiến Trúc Quản Lý Thiết Bị (Device Tracking Architecture)

Nhằm nâng cao tính minh bạch và an toàn tài khoản, hệ thống ParkWise cung cấp tính năng **Theo dõi thiết bị & Quản lý phiên làm việc đa nền tảng**:

1. **Gắn định danh phiên (`sid`) vào Access Token**:
   - Khi người dùng đăng nhập thành công, Backend tạo bản ghi `AuthSession` lưu `User-Agent` và `IpAddress`.
   - Access Token JWT được phát hành sẽ mang claim `"sid"` (Session ID). Nhờ đó, Backend luôn xác định được phiên nào chính là **thiết bị người dùng đang cầm trên tay** (`isCurrent = true`).
2. **Nhận diện thiết bị thân thiện (Human-Readable Device Name)**:
   - Backend phân tích chuỗi `User-Agent` thành tên thiết bị và hệ điều hành dễ hiểu (VD: `"Chrome trên Windows"`, `"Safari trên iOS"`, `"Edge trên macOS"`), thay vì bắt Frontend phải tự phân tích chuỗi User-Agent phức tạp.
3. **Bảo vệ quyền riêng tư địa chỉ IP (IP Masking)**:
   - Địa chỉ IP là dữ liệu nhạy cảm. Backend tự động che giấu phần host:
     - IPv4: Che 2 octet cuối: `192.168.x.x`
     - IPv6: Che phần thiết bị: `2001:0db8:85a3:0000:x:x:x:x`
4. **Đăng xuất từ xa (Remote Sign-out)**:
   - Người dùng có thể xem danh sách các máy đã từng đăng nhập tài khoản của mình và bấm **"Đăng xuất"** một máy bất kỳ (hoặc bấm **"Đăng xuất tất cả thiết bị"** khi phát hiện đăng nhập trái phép).
   - Nếu người dùng thu hồi đúng phiên hiện tại của mình (`isCurrent = true`), Cookie `sp_refresh` sẽ tự động bị xóa bỏ.

```mermaid
sequenceDiagram
    autonumber
    actor User as Người dùng (Thiết bị A)
    participant FE as Frontend Client
    participant API as Identity API
    participant DB as Postgres Database

    Note over User,DB: 1. Xem danh sách thiết bị đang đăng nhập
    User->>FE: Truy cập trang "Bảo mật & Thiết bị"
    FE->>API: GET /api/identity/accounts/sessions (Header: Bearer JWT)
    API->>DB: Truy vấn danh sách Session còn hoạt động
    API-->>FE: 200 OK (Mảng SessionInfo có isCurrent, masked IP)
    FE-->>User: Render danh sách thiết bị (Đánh dấu "Thiết bị này")

    Note over User,DB: 2. Thu hồi từ xa Thiết bị B
    User->>FE: Bấm "Đăng xuất" Thiết bị B (sessionId)
    FE->>API: DELETE /api/identity/accounts/sessions/{sessionId} (Bearer JWT)
    API->>DB: Thu hồi Session & RefreshToken của Thiết bị B
    API-->>FE: 200 OK (RevokedSession)
    FE-->>User: Xóa Thiết bị B khỏi danh sách UI

    Note over User,DB: 3. Đăng xuất khỏi TẤT CẢ thiết bị
    User->>FE: Bấm "Đăng xuất khỏi tất cả thiết bị"
    FE->>API: POST /api/identity/accounts/logout-all (Bearer JWT)
    API->>DB: Thu hồi toàn bộ Session & RefreshToken của Account
    API-->>FE: 200 OK (Set-Cookie: sp_refresh=; Max-Age=0)
    FE->>FE: Xóa AccessToken trong Memory & Chuyển sang /login
```

---

## 2. 🌐 Đặc Tả Chi Tiết Từng Endpoint

---

### 2.1. Lấy Danh Sách Thiết Bị Đang Đăng Nhập (List Active Sessions)

Lấy danh sách tất cả các phiên làm việc đang hoạt động của tài khoản người dùng hiện tại, có đánh dấu thiết bị hiện tại.

- **HTTP Method:** `GET`
- **URL Path:** `/api/identity/accounts/sessions`
- **Quyền truy cập (Auth):** 🔴 **BẮT BUỘC ACCESS TOKEN** (`RequireAuthorization()`)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị mẫu | Giải thích |
| :--- | :---: | :--- | :--- |
| `Authorization` | 🔴 **BẮT BUỘC** | `Bearer eyJhbGciOiJSUz...` | Access Token của tài khoản đang đăng nhập |
| `Accept` | 🟢 **KHUYẾN NGHỊ** | `application/json` | Định dạng dữ liệu mong muốn |

#### 📥 Request Body: ❌ **KHÔNG CÓ BODY (Request GET)**

#### 📤 Phản Hồi Thành Công (`200 OK`)
*Trả về mảng JSON danh sách các thiết bị (`SessionInfo[]`).*

| Trường (Field) | Kiểu dữ liệu | Phân loại | Ý nghĩa & Giá trị mẫu |
| :--- | :---: | :---: | :--- |
| `id` | `string (UUID)` | 🟢 **LUÔN CÓ** | Định danh duy nhất của phiên (`"c9b578e2-f472-4b2a-8bc2-a1b948f93e12"`). Dùng để truyền vào API DELETE. |
| `device` | `string` | 🟢 **LUÔN CÓ** | Tên thiết bị thân thiện do Backend phân tích: `"Chrome trên Windows"`, `"Safari trên iOS"`, `"Edge trên macOS"`, `"Trình duyệt khác"`. |
| `ipAddress` | `string` hoặc `null` | ⚪ **CÓ THỂ NULL** | Địa chỉ IP đã được che giấu (`"113.161.x.x"` hoặc `"2001:db8:x:x:x:x"`). Trả `null` nếu không xác định được IP. |
| `createdAtUtc` | `string (ISO-8601)` | 🟢 **LUÔN CÓ** | Thời điểm bắt đầu đăng nhập phiên này (`"2026-10-12T08:30:00Z"`). |
| `lastActivityAtUtc` | `string (ISO-8601)` | 🟢 **LUÔN CÓ** | Thời điểm hoạt động / refresh gần nhất (`"2026-10-13T06:15:00Z"`). |
| `isCurrent` | `boolean` | 🟢 **LUÔN CÓ** | `true` nếu đây chính là phiên/thiết bị đang gửi request; `false` nếu là thiết bị khác. |

#### 📝 Response Body Mẫu
```json
[
  {
    "id": "c9b578e2-f472-4b2a-8bc2-a1b948f93e12",
    "device": "Chrome trên Windows",
    "ipAddress": "113.161.x.x",
    "createdAtUtc": "2026-10-12T08:30:00Z",
    "lastActivityAtUtc": "2026-10-13T06:15:00Z",
    "isCurrent": true
  },
  {
    "id": "e7c65432-d123-4a56-8bc9-f123456789ab",
    "device": "Safari trên iOS",
    "ipAddress": "14.232.x.x",
    "createdAtUtc": "2026-10-10T14:20:00Z",
    "lastActivityAtUtc": "2026-10-11T09:45:00Z",
    "isCurrent": false
  }
]
```

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết (Error Responses)

| HTTP Status | Error Code (`errorCode`) | Nguyên nhân cụ thể | Hành động gợi ý cho Frontend |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | *Standard HTTP 401* | Token không hợp lệ hoặc đã hết hạn. | Kích hoạt Silent Refresh hoặc đưa về màn hình Đăng nhập. |

---

### 2.2. Thu Hồi Một Phiên / Đăng Xuất Thiết Bị Từ Xa (Revoke Session)

Thu hồi phiên làm việc gắn với `sessionId`. Vô hiệu hóa ngay lập tức Refresh Token của thiết bị đó.

- **HTTP Method:** `DELETE`
- **URL Path:** `/api/identity/accounts/sessions/{sessionId}`
- **Quyền truy cập (Auth):** 🔴 **BẮT BUỘC ACCESS TOKEN** (`RequireAuthorization()`)

#### 📋 Request Headers & Route Params

| Tham số / Header | Trạng thái | Vị trí | Kiểu dữ liệu | Giải thích |
| :--- | :---: | :---: | :---: | :--- |
| `Authorization` | 🔴 **BẮT BUỘC** | Header | `string` | `Bearer <access_token>` |
| `sessionId` | 🔴 **BẮT BUỘC** | Path | `string (UUID)` | ID của phiên cần thu hồi (lấy từ trường `id` trong API danh sách sessions). |

#### 📥 Request Body: ❌ **KHÔNG CÓ BODY (Request DELETE)**

#### 📤 Phản Hồi Thành Công (`200 OK`)

| Trường (Field) | Kiểu dữ liệu | Phân loại | Ý nghĩa & Giá trị |
| :--- | :---: | :---: | :--- |
| `sessionId` | `string (UUID)` | 🟢 **LUÔN CÓ** | ID của phiên vừa bị thu hồi |
| `message` | `string` | 🟢 **LUÔN CÓ** | `"Đã đăng xuất thiết bị này."` |

```json
{
  "sessionId": "e7c65432-d123-4a56-8bc9-f123456789ab",
  "message": "Đã đăng xuất thiết bị này."
}
```

> **Lưu ý quan trọng cho Frontend:**  
> Nếu `sessionId` bị xóa trùng với phiên hiện tại (`isCurrent == true`), Backend sẽ tự động xóa Cookie `sp_refresh`. Frontend cần xóa Access Token trong memory và điều hướng người dùng về trang Đăng nhập.

#### ⚠️ Danh Sách Mã Lỗi Chi Tiết (Error Responses)

| HTTP Status | Error Code (`errorCode`) | Nguyên nhân cụ thể | Hành động gợi ý cho Frontend |
| :---: | :--- | :--- | :--- |
| `404 Not Found` | `identity.session_not_found` | Phiên không tồn tại, đã hết hạn, hoặc **thuộc về một tài khoản khác** (chống IDOR). | Hiển thị: *"Không tìm thấy phiên đăng nhập này."* và tải lại danh sách thiết bị. |
| `401 Unauthorized` | *Standard HTTP 401* | Token không hợp lệ. | Điều hướng về màn hình Đăng nhập. |

---

### 2.3. Đăng Xuất Tất Cả Thiết Bị (Logout All Devices)

Thu hồi toàn bộ tất cả các phiên làm việc và Refresh Token của tài khoản trên mọi thiết bị, đồng thời xóa Cookie `sp_refresh` trên trình duyệt hiện tại.

- **HTTP Method:** `POST`
- **URL Path:** `/api/identity/accounts/logout-all`
- **Quyền truy cập (Auth):** 🔴 **BẮT BUỘC ACCESS TOKEN** (`RequireAuthorization()`)

#### 📋 Request Headers

| Header | Trạng thái | Giá trị mẫu |
| :--- | :---: | :--- |
| `Authorization` | 🔴 **BẮT BUỘC** | `Bearer eyJhbGciOiJSUz...` |
| `Content-Type` | 🔴 **BẮT BUỘC** | `application/json` |

#### 📥 Request Body: ❌ **KHÔNG CÓ BODY**

#### 📤 Phản Hồi Thành Công (`200 OK`)

##### 🍪 Header Xóa Cookie Đính Kèm:
```http
Set-Cookie: sp_refresh=; Path=/api/identity/accounts; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Max-Age=0
```

##### 📦 Response Body:
```json
{
  "message": "Đã đăng xuất khỏi tất cả thiết bị."
}
```

---

## 3. 🚨 Chuẩn Định Dạng Lỗi (RFC 7807 ProblemDetails)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Không tìm thấy phiên đăng nhập này.",
  "status": 404,
  "detail": "Không tìm thấy phiên đăng nhập này.",
  "errorCode": "identity.session_not_found"
}
```

---

## 4. 💻 Khai Báo TypeScript & Hàm Gọi API Mẫu (Frontend SDK)

### 4.1. TypeScript Interface Definitions

```typescript
// ==========================================
// 1. RESPONSE INTERFACES
// ==========================================

export interface SessionInfo {
  id: string;                 // 🟢 [LUÔN CÓ] UUID phiên
  device: string;             // 🟢 [LUÔN CÓ] Tên thiết bị (VD: "Chrome trên Windows")
  ipAddress: string | null;   // ⚪ [CÓ THỂ NULL] IP đã che (VD: "113.161.x.x")
  createdAtUtc: string;       // 🟢 [LUÔN CÓ] ISO-8601
  lastActivityAtUtc: string;  // 🟢 [LUÔN CÓ] ISO-8601
  isCurrent: boolean;         // 🟢 [LUÔN CÓ] true nếu là thiết bị hiện tại
}

export interface RevokedSessionResponse {
  sessionId: string;          // 🟢 [LUÔN CÓ]
  message: string;            // 🟢 [LUÔN CÓ]
}

export interface LogoutAllResponse {
  message?: string;
}
```

### 4.2. Axios Service Functions

```typescript
import { api, setAccessToken } from './auth';

export const sessionService = {
  /**
   * 1. Lấy danh sách các thiết bị đang đăng nhập
   */
  async getSessions(): Promise<SessionInfo[]> {
    const response = await api.get<SessionInfo[]>('/api/identity/accounts/sessions');
    return response.data;
  },

  /**
   * 2. Đăng xuất từ xa một thiết bị
   * @param sessionId ID của phiên cần thu hồi
   */
  async revokeSession(sessionId: string): Promise<RevokedSessionResponse> {
    const response = await api.delete<RevokedSessionResponse>(`/api/identity/accounts/sessions/${sessionId}`);
    return response.data;
  },

  /**
   * 3. Đăng xuất khỏi TẤT CẢ các thiết bị trên toàn hệ thống
   */
  async logoutAll(): Promise<LogoutAllResponse> {
    const response = await api.post<LogoutAllResponse>('/api/identity/accounts/logout-all');
    // Xóa access token trong bộ nhớ và chuyển hướng người dùng
    setAccessToken(null);
    return response.data;
  },
};
```

---

## 5. 🎯 Checklist UI/UX Frontend Màn Hình Quản Lý Thiết Bị

- [ ] **Màn hình Danh sách Thiết bị (`/settings/devices` hoặc `/profile/sessions`):**
  - Hiển thị badge màu xanh lá nổi bật: `🟢 Thiết bị này` (Current Device) cho item có `isCurrent === true`.
  - Hiển thị icon tương ứng với thiết bị (Desktop icon cho Windows/macOS/Linux, Mobile icon cho iOS/Android).
  - Format thời gian thân thiện (Relative time): *"Hoạt động 5 phút trước"*, *"Đăng nhập 2 ngày trước"*.
  - Với thiết bị hiện tại: Hiển thị nút *"Đăng xuất"* bình thường.
  - Với các thiết bị khác: Hiển thị nút *"Đăng xuất từ xa"* (hoặc icon Thùng rác đỏ). Khi bấm, mở modal xác nhận: *"Bạn có chắc chắn muốn đăng xuất tài khoản khỏi [Tên thiết bị] không?"*.
  - Khi người dùng bấm **"Đăng xuất tất cả thiết bị"**:
    - Hiển thị dialog cảnh báo nguy hiểm: *"Thao tác này sẽ đăng xuất tài khoản của bạn trên mọi máy tính và điện thoại, bao gồm cả thiết bị này. Bạn sẽ cần đăng nhập lại."*
    - Sau khi thành công: Xóa state đăng nhập và điều hướng ngay về `/login`.
