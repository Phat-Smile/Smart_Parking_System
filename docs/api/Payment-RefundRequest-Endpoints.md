# Payment Service — Refund Request API Endpoints

> **Tài liệu đặc tả API yêu cầu hoàn tiền do sự cố hệ thống (US-REFUND-001 / FR-PAY-005 / MF-13)**  
> **Service:** Payment Service  
> **Base Path:** `/api/payment/refund-requests`  
> **Trạng thái:** Triển khai (US-REFUND-001)

---

## 1. Tổng quan & Quy tắc nghiệp vụ (Business Rules)

- **BR-10.e**: Yêu cầu hoàn tiền do sự cố hệ thống. Khách hàng nộp yêu cầu -> Chủ bãi kiểm tra xác nhận số liệu (`owner-confirmation`) -> Quản trị viên (SystemAdmin) xem xét phê duyệt hoặc từ chối (`reject`).
- **Phân tách trách nhiệm (Separation of Duties)**: Người tạo yêu cầu (`CreatedBy`) tuyệt đối không thể là người duyệt (`ReviewedBy`). Vi phạm trả về `403 Forbidden` (`payment.creator_cannot_review`).
- **Quyết định D1**: Chỉ Khách hàng (`Customer`) có ví tiền trong hệ thống mới được tạo yêu cầu hoàn tiền.
- **Quyết định D2**: Quản trị viên chỉ có thể từ chối sau khi Chủ bãi đã xác nhận số liệu. Nếu Quản trị viên từ chối trước khi có xác nhận của Chủ bãi, hệ thống trả về `409 Conflict` (`payment.owner_not_confirmed`).
- **US-REFUND-001 AC2**: Khi Quản trị viên từ chối, trạng thái chuyển thành `Rejected`, ghi chép nhật ký kiểm toán (`audit_logs`) với lý do từ chối. Số dư ví (`wallets.balance`) và danh sách bút toán (`wallet_ledger_entries`) giữ nguyên không đổi.
- **Phạm vi ngoài (Out of scope - US-REFUND-002)**: Thao tác duyệt (`Approve`) và ghi có vào ví (`wallet credit`) thuộc phạm vi của US-REFUND-002. Cột `ledger_entry_id` luôn giữ giá trị `null` trong US-REFUND-001.

---

## 2. Bảng trạng thái (State Machine)

| Trạng thái hiện tại | Thao tác | Tác nhân | Điều kiện tiên quyết | Trạng thái tiếp theo |
|---|---|---|---|---|
| *(Chưa có)* | Tạo yêu cầu | Customer | Khách hàng đã có ví trong hệ thống | `Requested` (OwnerConfirmedBy = null) |
| `Requested` | Xác nhận số liệu | ParkingLotOwner / LotOwner | Là chủ sở hữu bãi xe (`IParkingLotMembershipGateway`), chưa từng xác nhận | `Requested` (OwnerConfirmedBy = OwnerId) |
| `Requested` | Từ chối yêu cầu | SystemAdmin (có claim `amr = mfa`) | Đã có `OwnerConfirmedBy != null`, Admin != Creator | `Rejected` (ReviewedBy = AdminId, ReviewedAt = now) |

---

## 3. Danh sách Endpoints

### 3.1. Tạo yêu cầu hoàn tiền
- **Phương thức:** `POST`
- **Đường dẫn:** `/api/payment/refund-requests`
- **Quyền truy cập:** `CustomerPolicy` (Yêu cầu vai trò `Customer`)

#### Request Headers
```http
Authorization: Bearer <Customer-JWT>
Content-Type: application/json
```

#### Request Body
```json
{
  "lotId": "11111111-1111-1111-1111-111111111111",
  "amount": 20000,
  "reason": "Hệ thống trừ tiền 2 lần khi thanh toán tại bãi",
  "referenceType": "ParkingSession",
  "referenceId": "ses-98765"
}
```

*Ghi chú:* `referenceType` nhận một trong các giá trị enum: `Reservation` (1), `ParkingSession` (2), `VnpayTransaction` (3), hoặc `null`.

#### Responses
- **`201 Created`**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Requested"
}
```
- **`400 Bad Request`**: Dữ liệu không hợp lệ (số tiền <= 0, số tiền không chẵn VND, lý do rỗng hoặc vượt quá 500 ký tự).
- **`401 Unauthorized`**: Chưa xác thực danh tính.
- **`403 Forbidden`**: Tài khoản không có vai trò `Customer` (`payment.customer_only`).
- **`404 Not Found`**: Khách hàng chưa kích hoạt ví (`payment.refund_request.wallet_not_found`).

---

### 3.2. Chủ bãi xác nhận số liệu
- **Phương thức:** `POST`
- **Đường dẫn:** `/api/payment/refund-requests/{id}/owner-confirmation`
- **Quyền truy cập:** `OwnerPolicy` (Yêu cầu vai trò `ParkingLotOwner` hoặc `LotOwner`)

#### Request Headers
```http
Authorization: Bearer <Owner-JWT>
```

#### Responses
- **`200 OK`**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Requested",
  "ownerConfirmedBy": "22222222-2222-2222-2222-222222222222"
}
```
- **`401 Unauthorized`**: Chưa xác thực danh tính.
- **`403 Forbidden`**: Người gọi không phải chủ sở hữu bãi đỗ xe này (`payment.forbidden_lot_access` hoặc `payment.forbidden_role`).
- **`404 Not Found`**: Không tìm thấy yêu cầu hoàn tiền (`payment.refund_request.not_found`).
- **`409 Conflict`**: Yêu cầu không ở trạng thái `Requested` (`payment.invalid_status`), đã được xác nhận trước đó (`payment.already_confirmed`), hoặc xung đột dữ liệu đồng thời (`payment.concurrency_conflict`).
- **`503 Service Unavailable`**: Dịch vụ Parking tạm thời không khả dụng (`payment.parking_service_unavailable`).

---

### 3.3. Quản trị viên từ chối yêu cầu
- **Phương thức:** `POST`
- **Đường dẫn:** `/api/payment/refund-requests/{id}/reject`
- **Quyền truy cập:** `AdminPolicy` (Yêu cầu vai trò `SystemAdmin` và xác thực đa yếu tố `amr = mfa`)

#### Request Headers
```http
Authorization: Bearer <Admin-MFA-JWT>
Content-Type: application/json
```

#### Request Body
```json
{
  "reason": "Khách hàng sử dụng dịch vụ bình thường theo camera đối soát"
}
```

#### Responses
- **`200 OK`**:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Rejected",
  "reviewedBy": "33333333-3333-3333-3333-333333333333",
  "reviewedAt": "2026-11-20T10:30:00+00:00"
}
```
- **`400 Bad Request`**: Thiếu lý do từ chối hoặc lý do vượt quá 500 ký tự.
- **`401 Unauthorized`**: Chưa xác thực danh tính.
- **`403 Forbidden`**: Người từ chối chính là người tạo yêu cầu (`payment.creator_cannot_review`), hoặc người gọi thiếu quyền Admin / thiếu MFA.
- **`404 Not Found`**: Không tìm thấy yêu cầu hoàn tiền (`payment.refund_request.not_found`).
- **`409 Conflict`**: Chủ bãi chưa xác nhận số liệu (`payment.owner_not_confirmed`), yêu cầu không ở trạng thái `Requested`, đã được duyệt/từ chối trước đó (`payment.already_reviewed`), hoặc xung đột dữ liệu đồng thời (`payment.concurrency_conflict`).

---

### 3.4. Danh sách yêu cầu hoàn tiền theo quyền hạn
- **Phương thức:** `GET`
- **Đường dẫn:** `/api/payment/refund-requests`
- **Quyền truy cập:** Đã xác thực (`RequireAuthorization`)

#### Query Parameters
| Tham số | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| `status` | `RefundStatus` (enum / int) | Không | Lọc theo trạng thái: `1` (Requested), `2` (Approved), `3` (Rejected) |
| `lotId` | `Guid` | Có điều kiện | **Bắt buộc đối với Chủ bãi** (`ParkingLotOwner`/`LotOwner`). Tùy chọn đối với Admin và Customer. |
| `page` | `int` | Không | Trang số (mặc định: `1`, tối thiểu: `1`) |
| `pageSize` | `int` | Không | Số phần tử mỗi trang (mặc định: `20`, giới hạn: `1` đến `50`) |

#### Phân quyền tra cứu (Scoping Rules)
1. **SystemAdmin**: Tra cứu toàn bộ yêu cầu trong hệ thống hoặc lọc theo `lotId` và `status`.
2. **ParkingLotOwner / LotOwner**: **Bắt buộc truyền `lotId`**. Hệ thống xác thực qua `IParkingLotMembershipGateway`. Nếu thiếu `lotId` -> `400 Bad Request` (`payment.refund_request.lot_id_required`). Nếu không có quyền quản lý bãi đó -> `403 Forbidden` (`payment.forbidden_lot_access`).
3. **Customer**: Tự động lọc theo `wallet_id` gắn liền với tài khoản của chính khách hàng. Khách hàng không thể xem yêu cầu của người khác.

#### Responses
- **`200 OK`**:
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "walletId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "lotId": "11111111-1111-1111-1111-111111111111",
      "amount": 20000,
      "reason": "Hệ thống trừ tiền 2 lần khi thanh toán tại bãi",
      "referenceType": "ParkingSession",
      "referenceId": "ses-98765",
      "status": "Requested",
      "createdBy": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
      "ownerConfirmedBy": "22222222-2222-2222-2222-222222222222",
      "reviewedBy": null,
      "reviewedAt": null,
      "createdAt": "2026-11-20T10:00:00+00:00"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```
- **`400 Bad Request`**: Chủ bãi không truyền `lotId`, hoặc `page`/`pageSize` không hợp lệ.
- **`401 Unauthorized`**: Chưa xác thực danh tính.
- **`403 Forbidden`**: Không có quyền truy cập bãi đỗ xe hoặc vai trò không hợp lệ.
- **`503 Service Unavailable`**: Dịch vụ Parking tạm thời không khả dụng.
