# Thay Đổi Quy Tắc Nghiệp Vụ (Business Rule Changes) — Tháng 11/2026

Tài liệu này ghi nhận các thay đổi quy tắc nghiệp vụ trong đợt hoàn thiện phân hệ Vận hành bãi đỗ xe (Operations Completion) thuộc phạm vi PR `feat(parking): let owners assign staff and show walk-in places`.

---

## 1. Q-STAFF-ASSIGN: Phân Công Nhân Viên Bãi Đỗ Xe Qua Email & Xác Thực gRPC

### Trước thay đổi (Before)
- Giao diện Chủ bãi (`/owner/lots/[id]/staff`) sử dụng hàm `getAvailableStaffAccounts` gọi trực tiếp vào API quản trị `GET /api/identity/admin/accounts?role=Staff`.
- API này yêu cầu chính sách `IdentityAuthorization.AdminPolicy` (dành riêng cho SystemAdmin), dẫn đến việc Chủ bãi khi truy cập trang bị lỗi `HTTP 403 Forbidden`.
- Việc cung cấp API liệt kê toàn bộ nhân viên hệ thống vi phạm nguyên tắc đặc quyền tối thiểu (Least Privilege), gây rò rỉ dữ liệu cá nhân (PII Leakage) và rủi ro tấn công dò quét tài khoản (Account Enumeration).
- Chưa có hợp đồng RPC tra cứu vai trò giữa phân hệ Parking và Identity.

### Sau thay đổi (After)
- **Hợp đồng gRPC mới:** Bổ sung `contracts/grpc/identity_staff.proto` (package `smartparking.identity.staff.v1`) với RPC `LookupStaffAccount(email)`.
- **Chống dò quét tài khoản (Anti-User-Enumeration):**
  - Identity chỉ trả về `is_found = true` khi email tồn tại, đang hoạt động (`active`) và mang vai trò `Staff`.
  - Mọi trường hợp khác (email không tồn tại, email thuộc vai trò Customer/Owner/Admin, hoặc tài khoản bị khóa) đều trả về cùng một kết quả trung tính: `is_found = false, account_id = ""` (không ném mã lỗi `NOT_FOUND`).
  - Parking trả về cùng mã lỗi `404 Not Found` RFC 7807 (`parking.staff.not_found`, *"Không tìm thấy tài khoản nhân viên hợp lệ với email đã cung cấp."*) để ngăn chặn hoàn toàn việc suy diễn trạng thái tài khoản.
- **Quy trình phân công an toàn:**
  - Chủ bãi nhập email chính xác qua `POST /api/parking/owner/lots/{lotId}/assignments`. Không nhận `accountId` thô từ client (chống tấn công IDOR).
  - Kiểm tra quyền sở hữu bãi `IsLotOwnerAsync` trước khi gọi gRPC sang Identity.
  - Áp dụng Rate Limiting: 10 lần/phút cho mỗi chủ bãi (`OwnerStaffAssignmentRateLimiting.cs`, mã lỗi HTTP 429 RFC 7807) để chống brute-force.
  - Xử lý Idempotent: Gán trùng nhân viên đã có trong bãi trả về `200 OK`, không tạo bản ghi thứ hai và không nhân đôi audit log.
  - Ghi nhật ký kiểm toán `IAuditLog` (`lot.staff_assigned` và `lot.staff_removed`) trong cùng một transaction PostgreSQL với lệnh gán.

### Lý do thay đổi (Reason)
- Khắc phục triệt để lỗi 403 Forbidden trên giao diện Chủ bãi.
- Bảo vệ dữ liệu nhạy cảm PII, ngăn chặn kẻ xấu dò quét danh bạ nhân viên, tuân thủ tiêu chuẩn Zero-Backdoor RBAC và Clean Architecture.

---

## 2. Walk-in Places (BR-02.d): Số Chỗ Trống Cho Xe Không Đặt Trước

### Trước thay đổi (Before)
- Hệ thống chỉ hiển thị số chỗ trống ô tô tổng thể (`AvailableSpaces`).
- Khách vãng lai (không đặt trước qua ứng dụng) không biết bãi còn chỗ thực tế hay toàn bộ chỗ trống đã được giữ trước cho khách đã đặt lịch (held quota).
- Khách lái xe đến cổng có thể bị từ chối vào bãi dù trên ứng dụng thấy còn chỗ trống.

### Sau thay đổi (After)
- **Công thức tính toán thuần túy (`WalkInPlacesCalculator.cs`):**
  - `walkInPlaces = max(0, AvailableSlots - heldQuota)`
  - Định nghĩa 3 trạng thái rõ ràng:
    1. `full` ("Đã đầy"): Khi `AvailableSlots <= 0`.
    2. `walkInFull` ("Hết chỗ cho xe không đặt trước"): Khi `AvailableSlots > 0` nhưng `walkInPlaces == 0` (toàn bộ chỗ trống đã được giữ trước).
    3. `available` ("Còn chỗ"): Khi `walkInPlaces > 0`.
- **Ranh giới hiển thị tối ưu:**
  - Chỉ tính toán và hiển thị `WalkInPlaces` tại:
    1. **Trang chi tiết bãi đỗ xe (`GetLotAvailabilityQuery`):** Nơi khách hàng chuẩn bị ra quyết định đến bãi ngay.
    2. **Cổng kiểm soát (Gate check-in):** Nơi nhân viên trực và barrier kiểm tra điều kiện mở cổng.
  - **Kết quả tìm kiếm danh sách bãi (`SearchCustomerLotsQuery`):** Hoàn toàn KHÔNG tính toán và không trả trường `WalkInPlaces`.

### Lý do thay đổi (Reason)
- **Triệt tiêu N+1 HTTP calls:** Nếu tìm kiếm gọi Booking lấy held quota cho từng bãi, với 20 bãi ứng viên sẽ phát sinh 20 HTTP calls song song (timeout 2s mỗi call), gây nghẽn toàn bộ trang tìm kiếm khi mạng chập chờn.
- **Tuân thủ triết lý #314:** Hiện kết quả tìm kiếm danh sách bãi tức thì, chỉ tải chỉ số phụ chi tiết khi người dùng bấm vào xem bãi cụ thể.
- **Tránh sai lệch dữ liệu:** Tránh việc giao diện đọc giá trị mặc định `0` thành "hết chỗ cho xe không đặt trước" trên hàng loạt bãi tìm kiếm.
