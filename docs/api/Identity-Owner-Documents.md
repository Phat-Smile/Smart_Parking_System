# Đặc Tả Giao Diện API: Giấy Tờ Hồ Sơ Chủ Bãi (Owner Documents)

> **Dịch vụ:** Identity Service  
> **Phiên bản:** v1.0.0  
> **User Story:** US-ADMIN-001  
> **Quy chuẩn:** [AGENTS.md](../../AGENTS.md) mục 5.1 (có hồ sơ giấy tờ trước khi cấp quyền Chủ bãi); vòng đời hồ sơ ở [Identity-Owner-Application-Contract.md](Identity-Owner-Application-Contract.md)  
> **Owner:** BE Team (Lộc)

---

## 1. Tổng quan

Một người chỉ được cấp quyền Chủ bãi khi hồ sơ đã chọn loại chủ thể và nộp đủ các giấy tờ **bắt buộc** của loại đó, rồi Admin duyệt. Tải lên không có nghĩa là đã được xác minh: Admin xem từng giấy tờ và tự quyết định.

Thứ tự thao tác của người nộp hồ sơ:

1. Đăng ký rồi xác thực email.
2. Tạo hồ sơ: đăng ký với `accountType = ParkingLotOwner`, hoặc `POST /api/identity/accounts/me/owner-application/apply` với tài khoản đã xác thực email. Hồ sơ ở trạng thái `Pending`.
3. Chọn loại chủ thể: `PUT /api/identity/accounts/me/owner-application`.
4. Xem checklist của loại đó, tải giấy tờ theo checklist.
5. Chờ Admin duyệt. Hồ sơ bị từ chối thì sửa giấy tờ rồi `resubmit` (chi tiết ở tài liệu hợp đồng hồ sơ).

Hồ sơ đã `Approved` thì khóa: không đổi loại chủ thể, không tải lên, không xóa giấy tờ (409).

### 1.1. Checklist theo loại chủ thể

Danh sách do BE trả về qua `GET /api/identity/owner-document-requirements`; FE đọc từ route này, không ghi cứng. Giá trị hiện tại:

| Loại chủ thể (`ownerType`) | Bắt buộc | Tùy chọn |
| :--- | :--- | :--- |
| `Individual` | `PremisesRightProof` | `AuthorizationLetter` |
| `HouseholdBusiness` | `HouseholdBusinessRegistration`, `PremisesRightProof` | `AuthorizationLetter` |
| `Enterprise` | `EnterpriseRegistration`, `PremisesRightProof` | `AuthorizationLetter` |

### 1.2. Ràng buộc tệp

* Định dạng PDF, PNG hoặc JPEG. BE kiểm nội dung tệp, không chỉ phần mở rộng hay `Content-Type` do client gửi.
* Mặc định tối đa 5 MB mỗi tệp và 10 tệp mỗi hồ sơ (cấu hình `OwnerDocuments:MaxFileSizeBytes`, `OwnerDocuments:MaxFilesPerApplication`).
* Mỗi loại giấy tờ có một tệp: tải lại cùng loại thì thay thế tệp cũ.
* Tệp lưu riêng tư. Không có đường dẫn công khai; chỉ đọc được qua các route dưới đây, kèm quyền.

---

## 2. Danh Sách Endpoint

| Method | Endpoint | Quyền (Auth) | Mô tả |
| :---: | :--- | :---: | :--- |
| `GET` | `/api/identity/owner-document-requirements?ownerType=` | `Bearer` | Checklist giấy tờ của một loại chủ thể |
| `PUT` | `/api/identity/accounts/me/owner-application` | `Bearer` | Chọn loại chủ thể cho hồ sơ |
| `GET` | `/api/identity/accounts/me/owner-application/documents` | `Bearer` | Giấy tờ mình đã tải lên |
| `POST` | `/api/identity/accounts/me/owner-application/documents` | `Bearer` | Tải lên một giấy tờ (`multipart/form-data`) |
| `DELETE` | `/api/identity/accounts/me/owner-application/documents/{documentType}` | `Bearer` | Xóa một giấy tờ |
| `GET` | `/api/identity/admin/owner-applications/{id}/documents` | `Admin` | Admin xem giấy tờ của hồ sơ, kèm kết quả đối chiếu checklist |
| `GET` | `/api/identity/admin/owner-applications/{id}/documents/{documentType}` | `Admin` | Admin tải một giấy tờ |

`{id}` là `accountId` của người nộp hồ sơ. Quyền `Admin` là vai trò `SystemAdmin`.

---

## 3. Chi Tiết Từng Endpoint

### 3.1. Checklist (`GET /api/identity/owner-document-requirements?ownerType=Individual`)
* **Response (`200 OK`):**
```json
{
  "ownerType": "Individual",
  "documents": [
    {
      "documentType": "PremisesRightProof",
      "name": "Giấy tờ chứng minh quyền sử dụng hoặc thuê mặt bằng bãi xe",
      "required": true,
      "acceptedContentTypes": ["application/pdf", "image/png", "image/jpeg"],
      "maxSizeBytes": 5242880
    }
  ]
}
```
* `ownerType` thiếu hoặc không phải một trong ba tên trên (không phân biệt hoa thường; không nhận số): `400`, `errorCode = request.validation_failed`.

### 3.2. Chọn loại chủ thể (`PUT /api/identity/accounts/me/owner-application`)
* **Request Body:** `{ "ownerType": "HouseholdBusiness" }`
* **Response (`200 OK`):** trạng thái hồ sơ, có thêm trường `ownerType`.
```json
{
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "owner@example.com",
  "status": "Pending",
  "taxCode": null,
  "businessName": null,
  "rejectionReason": null,
  "reviewedAtUtc": null,
  "ownerType": "HouseholdBusiness"
}
```
* Lỗi: `400` `request.validation_failed`; `404` `identity.owner_application_not_found` (chưa có hồ sơ); `409` `identity.owner_application_locked` (đã duyệt).

### 3.3. Danh sách giấy tờ của tôi (`GET /api/identity/accounts/me/owner-application/documents`)
* **Response (`200 OK`):**
```json
[
  {
    "id": "0b1a8a39-6b2f-4f55-9a9c-6a4f6d3b3f10",
    "documentType": "PremisesRightProof",
    "fileName": "mat_bang.pdf",
    "contentType": "application/pdf",
    "sizeBytes": 183204,
    "uploadedAtUtc": "2026-10-24T08:15:00Z"
  }
]
```
* Lỗi: `404` `identity.owner_application_not_found`.

### 3.4. Tải lên (`POST /api/identity/accounts/me/owner-application/documents`)
* **Content-Type:** `multipart/form-data`, hai trường: `documentType` (một tên trong checklist của loại chủ thể đã chọn, không phân biệt hoa thường) và `file`.
* **Response (`201 Created`):** một phần tử như ở mục 3.3 (`documentType` trả về đúng cách viết của checklist).
* Lỗi:

| HTTP | `errorCode` | Khi nào |
| :---: | :--- | :--- |
| `400` | `identity.owner_document_type_required` | Thiếu `documentType` |
| `400` | `identity.owner_document_file_required` | Thiếu `file` hoặc tệp rỗng |
| `400` | `identity.owner_type_required` | Chưa chọn loại chủ thể |
| `400` | `identity.owner_document_invalid_type` | `documentType` không thuộc checklist của loại đã chọn |
| `400` | `identity.owner_document_unsupported_content` | Nội dung không phải PDF, PNG hoặc JPEG |
| `404` | `identity.owner_application_not_found` | Chưa có hồ sơ |
| `409` | `identity.owner_application_locked` | Hồ sơ đã duyệt |
| `409` | `identity.owner_document_limit_reached` | Đã đủ số tệp tối đa |
| `413` | `identity.owner_document_too_large` | Tệp vượt dung lượng tối đa |
| `415` | `request.invalid_content_type` | Không phải `multipart/form-data` |

### 3.5. Xóa (`DELETE /api/identity/accounts/me/owner-application/documents/{documentType}`)
* **Response (`200 OK`):** `{ "message": "Xóa giấy tờ thành công." }`
* Lỗi: `404` `identity.owner_application_not_found` hoặc `identity.owner_document_not_found`; `409` `identity.owner_application_locked`.

### 3.6. Admin xem giấy tờ của một hồ sơ (`GET /api/identity/admin/owner-applications/{id}/documents`)
* **Auth:** `Bearer <admin_token>`. Xem được hồ sơ ở mọi trạng thái (`Pending`, `Approved`, `Rejected`).
* **Response (`200 OK`):**
```json
{
  "accountId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Pending",
  "ownerType": "HouseholdBusiness",
  "isComplete": false,
  "missingDocumentTypes": ["HouseholdBusinessRegistration"],
  "documents": [
    {
      "id": "0b1a8a39-6b2f-4f55-9a9c-6a4f6d3b3f10",
      "documentType": "PremisesRightProof",
      "fileName": "mat_bang.pdf",
      "contentType": "application/pdf",
      "sizeBytes": 183204,
      "uploadedAtUtc": "2026-10-24T08:15:00Z"
    }
  ]
}
```
* `isComplete` và `missingDocumentTypes` là đúng kết quả mà thao tác duyệt sẽ dùng (mục 4). Hồ sơ chưa chọn loại chủ thể không có checklist: `ownerType = null`, `isComplete = false`, `missingDocumentTypes = []`.
* `documents` có thể gồm cả giấy tờ nằm ngoài checklist hiện tại (đã tải khi còn loại chủ thể khác); giấy tờ đó không tính vào `isComplete`.
* Để hiển thị checklist đầy đủ (tên, bắt buộc hay tùy chọn), gọi thêm mục 3.1 với `ownerType` ở trên.
* Lỗi: `401`, `403` (không phải Admin), `404` `identity.owner_application_not_found`.
* Mỗi lần Admin tải một tệp (mục 3.7), BE ghi một dòng log gồm mã Admin, mã hồ sơ và loại giấy tờ; không ghi tên tệp. Việc xem danh sách không ghi log.

### 3.7. Admin tải một giấy tờ (`GET /api/identity/admin/owner-applications/{id}/documents/{documentType}`)
* **Auth:** `Bearer <admin_token>`. `documentType` là một tên trong bất kỳ checklist nào, không phân biệt hoa thường.
* **Response (`200 OK`):** nội dung tệp, `Content-Type` theo loại đã được BE xác minh khi tải lên, `Content-Disposition: attachment` kèm tên tệp gốc, `Cache-Control: no-store, private`, `X-Content-Type-Options: nosniff`.
* **FE:** route cần header `Authorization`, nên không dùng được trực tiếp trong `<img src>` hay `<a href>`. Gọi bằng `fetch` kèm Bearer, đọc `blob()` rồi `URL.createObjectURL` để xem; gọi `URL.revokeObjectURL` khi đóng.
* Lỗi: `401`, `403`, `404` với một trong ba `errorCode`:

| `errorCode` | Khi nào |
| :--- | :--- |
| `identity.owner_application_not_found` | Không có hồ sơ cho `{id}` |
| `identity.owner_document_not_found` | Loại không thuộc checklist nào, hoặc hồ sơ này chưa có giấy tờ loại đó |
| `identity.owner_document_file_unavailable` | Có bản ghi nhưng tệp không còn đọc được; yêu cầu người nộp tải lại |

---

## 4. Điều kiện duyệt hồ sơ

`POST /api/identity/admin/owner-applications/{id}/approve` chỉ thành công khi hồ sơ đang `Pending`, đã chọn loại chủ thể và đã có đủ mọi giấy tờ bắt buộc của loại đó. Khi không đạt, BE không đổi gì: hồ sơ vẫn `Pending`, vai trò vẫn là Customer, các phiên đăng nhập không bị thu hồi, không phát sự kiện. Việc kiểm tra và quyết định nằm trong một giao dịch giữ khóa dòng của tài khoản; tải lên và xóa giấy tờ của hồ sơ đó xếp hàng sau khóa này, nên một giấy tờ bị xóa đúng lúc Admin bấm duyệt không làm hồ sơ được duyệt khi thiếu giấy tờ.

| HTTP | `errorCode` | Khi nào |
| :---: | :--- | :--- |
| `409` | `identity.owner_application_not_pending` | Hồ sơ không ở trạng thái `Pending` |
| `409` | `identity.owner_type_required` | Chưa chọn loại chủ thể |
| `409` | `identity.owner_type_invalid` | Loại chủ thể lưu trong dữ liệu không hợp lệ (dữ liệu hỏng) |
| `409` | `identity.owner_documents_incomplete` | Còn thiếu giấy tờ bắt buộc |

`reject` không đòi hỏi giấy tờ: Admin có thể từ chối bất kỳ hồ sơ `Pending` nào.

---

## 5. TypeScript Interfaces Cho Frontend

```typescript
export type OwnerType = "Individual" | "HouseholdBusiness" | "Enterprise";

export interface OwnerDocumentRequirement {
  documentType: string;
  name: string;
  required: boolean;
  acceptedContentTypes: string[];
  maxSizeBytes: number;
}

export interface OwnerDocumentRequirements {
  ownerType: OwnerType;
  documents: OwnerDocumentRequirement[];
}

export interface OwnerDocument {
  id: string;
  documentType: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
}

export interface OwnerApplicationDocumentsReview {
  accountId: string;
  status: "Pending" | "Approved" | "Rejected";
  ownerType: OwnerType | null;
  isComplete: boolean;
  missingDocumentTypes: string[];
  documents: OwnerDocument[];
}
```
