# Đặc Tả Giao Diện API: Hồ Sơ Cá Nhân (Profile)

> **Dịch vụ:** Identity Service  
> **Phiên bản:** v1.0.0  
> **Trạng thái:** Sẵn sàng tích hợp  
> **User Story:** US-PROFILE-001, US-PROFILE-002  
> **Owner:** BE-1

---

## 1. Tổng quan

Endpoint cho phép người dùng đã đăng nhập tạo hoặc cập nhật hồ sơ cá nhân. Hành vi là **upsert**: nếu chưa có hồ sơ sẽ tạo mới; nếu đã có sẽ cập nhật.

---

## 2. PUT /api/identity/accounts/me/profile

| Thuộc tính | Chi tiết |
| :--- | :--- |
| **HTTP Method** | `PUT` |
| **URL Path** | `/api/identity/accounts/me/profile` |
| **Auth** | 🔴 **BẮT BUỘC** — `Bearer <access_token>` |
| **Content-Type** | `application/json` |

### Request Body Schema

| Trường | Kiểu | Phân loại | Ràng buộc |
| :--- | :---: | :---: | :--- |
| `fullName` | `string` | 🔴 **BẮT BUỘC** | Tối đa 100 ký tự, không được để trống |
| `dateOfBirth` | `string (yyyy-MM-dd)` hoặc `null` | 🟡 TÙY CHỌN | Định dạng ngày ISO-8601 |
| `citizenIdNumber` | `string` hoặc `null` | 🟡 TÙY CHỌN | Tối đa 20 ký tự |
| `driverLicenseNumber` | `string` hoặc `null` | 🟡 TÙY CHỌN | Tối đa 20 ký tự |
| `avatarUrl` | `string` hoặc `null` | 🟡 TÙY CHỌN | URL ảnh đại diện, tối đa 500 ký tự |

```json
{
  "fullName": "Nguyễn Văn A",
  "dateOfBirth": "1995-05-20",
  "citizenIdNumber": "079095012345",
  "driverLicenseNumber": "B1-079095012",
  "avatarUrl": "https://example.com/avatars/user123.jpg"
}
```

### Phản Hồi Thành Công (`200 OK`)

| Trường | Kiểu | Ý nghĩa |
| :--- | :---: | :--- |
| `accountId` | `string (UUID)` | ID tài khoản |
| `fullName` | `string` | Họ và tên |
| `dateOfBirth` | `string` hoặc `null` | Ngày sinh |
| `citizenIdNumber` | `string` hoặc `null` | Số CCCD/CMND |
| `driverLicenseNumber` | `string` hoặc `null` | Số bằng lái xe |
| `avatarUrl` | `string` hoặc `null` | URL ảnh đại diện |
| `createdAtUtc` | `string (ISO-8601)` | Thời điểm tạo hồ sơ |
| `updatedAtUtc` | `string` hoặc `null` | Thời điểm cập nhật lần cuối |

```json
{
  "accountId": "a3bb108e-17d4-4a6c-9f87-2a54e9912345",
  "fullName": "Nguyễn Văn A",
  "dateOfBirth": "1995-05-20",
  "citizenIdNumber": "079095012345",
  "driverLicenseNumber": "B1-079095012",
  "avatarUrl": "https://example.com/avatars/user123.jpg",
  "createdAtUtc": "2026-10-15T08:00:00Z",
  "updatedAtUtc": "2026-10-15T09:30:00Z"
}
```

### Mã Lỗi

| HTTP Status | Error Code | Nguyên nhân |
| :---: | :--- | :--- |
| `400 Bad Request` | `validation_error` | `fullName` để trống, vượt độ dài |
| `401 Unauthorized` | — | Thiếu hoặc token hết hạn |
| `404 Not Found` | `identity.account_not_found` | Tài khoản không tồn tại |

---

## 3. GET /api/identity/accounts/me/profile

Lấy chi tiết hồ sơ cá nhân của tài khoản hiện tại (bao gồm cả CCCD, bằng lái, ngày sinh và ảnh đại diện). Nếu tài khoản chưa tạo hồ sơ, hệ thống sẽ trả về bản ghi mặc định với `fullName` lấy từ bảng Account và các trường chi tiết là `null`.

| Thuộc tính | Chi tiết |
| :--- | :--- |
| **HTTP Method** | `GET` |
| **URL Path** | `/api/identity/accounts/me/profile` |
| **Auth** | 🔴 **BẮT BUỘC** — `Bearer <access_token>` |

### Phản Hồi Thành Công (`200 OK`)

> **Lưu ý bảo mật (US-PRIV-002):** API tuyệt đối không trả số CCCD/GPLX đầy đủ. Dữ liệu trả về luôn được che tự động (masking), chỉ hiển thị 4 ký tự cuối (ví dụ: `********2345` cho CCCD, `*****012` cho GPLX).

| Trường | Kiểu | Ý nghĩa |
| :--- | :---: | :--- |
| `accountId` | `string (UUID)` | ID tài khoản |
| `fullName` | `string` | Họ và tên |
| `dateOfBirth` | `string` hoặc `null` | Ngày sinh (yyyy-MM-dd) |
| `citizenIdNumber` | `string` hoặc `null` | Số CCCD/CMND đã che (`********2345`) |
| `driverLicenseNumber` | `string` hoặc `null` | Số bằng lái xe đã che (`*****012`) |
| `avatarUrl` | `string` hoặc `null` | URL ảnh đại diện |
| `createdAtUtc` | `string (ISO-8601)` | Thời điểm tạo hồ sơ |
| `updatedAtUtc` | `string` hoặc `null` | Thời điểm cập nhật lần cuối |

```json
{
  "accountId": "a3bb108e-17d4-4a6c-9f87-2a54e9912345",
  "fullName": "Nguyễn Văn A",
  "dateOfBirth": "1995-05-20",
  "citizenIdNumber": "********2345",
  "driverLicenseNumber": "*****012",
  "avatarUrl": "https://example.com/avatars/user123.jpg",
  "createdAtUtc": "2026-10-15T08:00:00Z",
  "updatedAtUtc": "2026-10-15T09:30:00Z"
}
```

### Mã Lỗi

| HTTP Status | Error Code | Nguyên nhân |
| :---: | :--- | :--- |
| `401 Unauthorized` | — | Thiếu hoặc token hết hạn |
| `404 Not Found` | `identity.account_not_found` | Tài khoản không tồn tại |

---

## 4. DELETE /api/identity/accounts/me/profile/identity

Xóa triệt để thông tin định danh cá nhân nhạy cảm (`citizenIdNumber` và `driverLicenseNumber`) khỏi hồ sơ cá nhân theo tiêu chuẩn quyền riêng tư dữ liệu **US-PRIV-002**. Sau khi xóa, mọi chức năng khác (đăng nhập, tìm bãi, xe, phiên) vẫn hoạt động hoàn toàn bình thường.

| Thuộc tính | Chi tiết |
| :--- | :--- |
| **HTTP Method** | `DELETE` |
| **URL Path** | `/api/identity/accounts/me/profile/identity` |
| **Auth** | 🔴 **BẮT BUỘC** — `Bearer <access_token>` |

### Phản Hồi Thành Công (`204 No Content`)
- Thành công: HTTP 204 không có body.

### Mã Lỗi

| HTTP Status | Error Code | Nguyên nhân |
| :---: | :--- | :--- |
| `401 Unauthorized` | — | Thiếu hoặc token hết hạn |
| `404 Not Found` | `identity.account_not_found` | Tài khoản không tồn tại |

---

## 5. TypeScript Interface

```typescript
export interface UpsertProfileRequest {
  fullName: string;
  dateOfBirth?: string | null;      // "yyyy-MM-dd"
  citizenIdNumber?: string | null;
  driverLicenseNumber?: string | null;
  avatarUrl?: string | null;
}

export interface ProfileResponse {
  accountId: string;
  fullName: string;
  dateOfBirth: string | null;
  citizenIdNumber: string | null;     // Masked: "********2345"
  driverLicenseNumber: string | null; // Masked: "*****012"
  avatarUrl: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}
```

