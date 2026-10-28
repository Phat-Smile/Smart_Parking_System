# Dữ Liệu Giả Lập Định Danh Chủ Bãi (Mock Identity & Tax Data Sheet)

> **Mục đích:** Phục vụ kiểm thử tích hợp (Integration Test), chạy tự động (Autotest) và Demo nghiệm thu toàn trình (OJT Acceptance Demo) cho quy trình Xét duyệt Chủ bãi & Hồ sơ người dùng.  
> **Nguyên tắc an toàn dữ liệu:** Không sử dụng thông tin CCCD thật hay mã số thuế doanh nghiệp thật trong cơ sở dữ liệu thật, tuân thủ Nghị định 13/2023/NĐ-CP và [AGENTS.md](file:///f:/Ky_5/Smart_Parking_System/AGENTS.md).

---

## 1. Dữ Liệu Mẫu Hợp Lệ Cho Demo Nghiệm Thu (Happy Paths)

Dưới đây là các bộ dữ liệu giả lập được cấu hình sẵn phù hợp với các quy tắc xác thực (Regex Validation Rules) của hệ thống:

| STT | Loại chủ thể | Họ và tên | SĐT di động (VN) | Mã số thuế (MST) | Số CCCD giả lập | Tên Doanh nghiệp / Hộ kinh doanh | Mục đích kịch bản Demo |
|:---:|:---|:---|:---:|:---:|:---:|:---|:---|
| **01** | **Cá nhân** (`Individual`) | Nguyễn Văn An | `0912345678` | *(Để trống)* | `079095012345` | *(Không áp dụng)* | Demo luồng Chủ bãi cá nhân tự quản lý bãi đỗ nhỏ. |
| **02** | **Hộ kinh doanh** (`Business`) | Trần Thị Bình | `0987654321` | `0318923456` | `001198034567` | Hộ Kinh Doanh Bãi Xe Sài Gòn | Demo luồng Hộ KD đăng ký kinh doanh dịch vụ giữ xe. |
| **03** | **Doanh nghiệp** (`Business`) | Lê Hoàng Cường | `0903112233` | `0102345678` | `040092008899` | Công Ty TNHH Dịch Vụ Đỗ Xe ParkWise | Demo luồng Công ty vận hành chuỗi bãi đỗ thông minh. |
| **04** | **Chi nhánh DN** (`Business`) | Phạm Minh Đức | `0977889900` | `0312345678-001` | `024097001122` | Chi Nhánh 1 - ParkWise Miền Đông | Demo MST 13 ký tự chuẩn chi nhánh trực thuộc. |

---

## 2. Dữ Liệu Kiểm Thử Biên & Lỗi Nhập Liệu (Edge Cases & Validation Failures)

Dùng để demo cho ban giám khảo thấy hệ thống có khả năng phòng thủ và validation chặt chẽ:

| STT | Trường kiểm thử | Dữ liệu nhập sai | Lỗi mong đợi từ Backend (Validation Error) | Mã lỗi HTTP |
|:---:|:---|:---:|:---|:---:|
| **E1** | **Mã số thuế** | `03123ABC` *(có chữ)* | `Mã số thuế phải gồm 10 chữ số hoặc 13 chữ số dạng xxxxxxxxxx-xxx.` | `400 Bad Request` |
| **E2** | **Mã số thuế** | `12345678` *(thiếu 8 số)* | `Mã số thuế phải gồm 10 chữ số hoặc 13 chữ số dạng xxxxxxxxxx-xxx.` | `400 Bad Request` |
| **E3** | **Số điện thoại** | `02838123456` *(máy bàn)* | `Số điện thoại phải là số di động Việt Nam hợp lệ.` | `400 Bad Request` |
| **E4** | **Họ và tên** | `A` *(1 ký tự)* | `Họ và tên phải có ít nhất 2 ký tự.` | `400 Bad Request` |
| **E5** | **Trạng thái kép** | Nộp khi đơn đang `Pending` | `Tài khoản đã có hồ sơ đăng ký chủ bãi đang chờ duyệt.` | `409 Conflict` |
| **E6** | **Tự duyệt** | Admin tự duyệt đơn của mình | `Quản trị viên không thể tự phê duyệt hồ sơ chủ bãi của chính mình.` | `409 Conflict` |

---

## 3. Kịch Bản Demo Toàn Trình (Demo Runbook)

Khi nghiệm thu với giảng viên / hội đồng, người demo thực hiện tuần tự theo 4 bước sau:

1. **Bước 1 — Khách hàng đăng ký:**  
   * Đăng ký tài khoản Customer bằng email `owner.demo@example.com`.
   * Xác thực mã OTP gửi về email (`123456` hoặc OTP trong log console).
2. **Bước 2 — Nộp hồ sơ nâng cấp chủ bãi:**  
   * Đăng nhập, vào trang Quản lý tài khoản ➔ chọn **"Đăng ký đối tác Chủ bãi"**.
   * Nhập bộ dữ liệu **STT 03**: MST `0102345678`, Tên DN `Công Ty TNHH Dịch Vụ Đỗ Xe ParkWise`, SĐT `0903112233`.
   * Bấm **Gửi hồ sơ**. Hệ thống hiển thị huy hiệu: **"Hồ sơ đang chờ duyệt" (Pending)**.
3. **Bước 3 — Quản trị viên thẩm định:**  
   * Đăng nhập tài khoản Admin (`admin@parkwise.vn`).
   * Vào danh mục **Hồ sơ Chủ bãi** ➔ Thấy hồ sơ `owner.demo@example.com` với đầy đủ MST và Tên công ty.
   * Thử bấm **Từ chối (Reject)** với lý do: *"Cần cập nhật lại số điện thoại hotline"* ➔ Bên phía khách hàng nhận trạng thái **Rejected** và đọc được lý do.
   * Khách hàng bấm **Nộp lại (Resubmit)**.
   * Admin bấm **Phê duyệt (Approve)**.
4. **Bước 4 — Nghiệm thu kết quả:**  
   * Khách hàng tải lại trang hoặc đăng nhập lại.
   * Tài khoản tự động được nâng cấp lên vai trò **`ParkingLotOwner`**.
   * Mở khóa phân hệ **Quản lý bãi đỗ xe** (`/owner/lots`) để bắt đầu đăng ký bãi xe mới!
