# Đặc Tả Giao Diện API: Quản Lý Phương Tiện (Vehicle)

> **Dịch vụ:** Identity Service  
> **Phiên bản:** v1.0.0  
> **Trạng thái:** Sẵn sàng tích hợp  
> **User Story:** US-VEH-001  
> **Owner:** BE-1

---

## 1. Tổng quan

Người dùng đã đăng nhập có thể đăng ký và xem danh sách phương tiện của mình. Biển số xe được chuẩn hóa (`Trim()` + `ToUpperInvariant()`), phải là duy nhất trong toàn hệ thống.

---

## 2. POST /api/identity/accounts/me/vehicles

Thêm một phương tiện mới vào tài khoản.

| Thuộc tính | Chi tiết |
| :--- | :--- |
| **HTTP Method** | `POST` |
| **URL Path** | `/api/identity/accounts/me/vehicles` |
| **Auth** | 🔴 **BẮT BUỘC** — `Bearer <access_token>` |
| **Content-Type** | `application/json` |

### Request Body Schema

| Trường | Kiểu | Phân loại | Ràng buộc |
| :--- | :---: | :---: | :--- |
| `licensePlate` | `string` | 🔴 **BẮT BUỘC** | Biển số xe, tối đa 20 ký tự. Hệ thống tự động chuyển hoa và Trim. |
| `vehicleType` | `string` | 🔴 **BẮT BUỘC** | Loại phương tiện. Một trong: `"Car"`, `"Motorbike"` (không phân biệt hoa thường). |
| `nickName` | `string` hoặc `null` | 🟡 TÙY CHỌN | Tên gợi nhớ, tối đa 50 ký tự |

```json
{
  "licensePlate": "51A-12345",
  "vehicleType": "Car",
  "nickName": "Xe nhà"
}
```

### Phản Hồi Thành Công (`201 Created`)

| Trường | Kiểu | Ý nghĩa |
| :--- | :---: | :--- |
| `vehicleId` | `string (UUID)` | ID phương tiện vừa thêm |
| `accountId` | `string (UUID)` | ID tài khoản chủ sở hữu |
| `licensePlate` | `string` | Biển số đã chuẩn hóa (`"51A-12345"`) |
| `vehicleType` | `string` | `"Car"` hoặc `"Motorbike"` |
| `nickName` | `string` hoặc `null` | Tên gợi nhớ |
| `createdAtUtc` | `string (ISO-8601)` | Thời điểm tạo |

```json
{
  "vehicleId": "b7cc219f-28e5-5b7d-af98-3b65fa023456",
  "accountId": "a3bb108e-17d4-4a6c-9f87-2a54e9912345",
  "licensePlate": "51A-12345",
  "vehicleType": "Car",
  "nickName": "Xe nhà",
  "createdAtUtc": "2026-10-15T09:00:00Z"
}
```

### Mã Lỗi

| HTTP Status | Error Code | Nguyên nhân |
| :---: | :--- | :--- |
| `400 Bad Request` | `validation_error` | `licensePlate` để trống / quá dài; `vehicleType` thiếu |
| `400 Bad Request` | `identity.invalid_vehicle_type` | `vehicleType` không phải `"Car"` hay `"Motorbike"` |
| `401 Unauthorized` | — | Thiếu hoặc token hết hạn |
| `404 Not Found` | `identity.account_not_found` | Tài khoản không tồn tại |
| `409 Conflict` | `identity.vehicle_plate_already_registered` | Biển số đã được đăng ký trong hệ thống |

---

## 3. GET /api/identity/accounts/me/vehicles

Lấy danh sách tất cả phương tiện của tài khoản hiện tại.

| Thuộc tính | Chi tiết |
| :--- | :--- |
| **HTTP Method** | `GET` |
| **URL Path** | `/api/identity/accounts/me/vehicles` |
| **Auth** | 🔴 **BẮT BUỘC** — `Bearer <access_token>` |

### Phản Hồi Thành Công (`200 OK`)

Trả về mảng các phương tiện (có thể rỗng):

```json
[
  {
    "vehicleId": "b7cc219f-28e5-5b7d-af98-3b65fa023456",
    "accountId": "a3bb108e-17d4-4a6c-9f87-2a54e9912345",
    "licensePlate": "51A-12345",
    "vehicleType": "Car",
    "nickName": "Xe nhà",
    "createdAtUtc": "2026-10-15T09:00:00Z"
  }
]
```

### Mã Lỗi

| HTTP Status | Error Code | Nguyên nhân |
| :---: | :--- | :--- |
| `401 Unauthorized` | — | Thiếu hoặc token hết hạn |

---

## 4. TypeScript Interface

```typescript
export type VehicleType = 'Car' | 'Motorbike';

export interface AddVehicleRequest {
  licensePlate: string;
  vehicleType: VehicleType;
  nickName?: string | null;
}

export interface VehicleResponse {
  vehicleId: string;
  accountId: string;
  licensePlate: string;
  vehicleType: VehicleType;
  nickName: string | null;
  createdAtUtc: string;
}
```
