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

---

## 3. Q-V2c: Tra Cứu Phí Đỗ Xe Công Khai Ẩn Danh Qua Session QR

### Trước thay đổi (Before)
- Không có endpoint tra cứu phí công khai cho khách quét QR tại bãi.
- Chỉ có endpoint xem trước phí `GET /api/payment/parking-sessions/{sessionId}/fee` yêu cầu xác thực JWT bearer token với chính sách `PaymentAuthorization.ViewSessionFeePolicy`. Khách vãng lai chưa cài ứng dụng hoặc chưa đăng nhập không thể kiểm tra số tiền đỗ xe trước khi thanh toán.

### Sau thay đổi (After)
- **Endpoint công khai mới:** `GET /api/payment/public/sessions/fee?qr=<token>` (`AllowAnonymous()`), không yêu cầu Authorization header.
- **Hợp đồng gRPC Parking mở rộng:** Thêm RPC `GetSessionFeeContextByQr(GetSessionFeeContextByQrRequest)` vào `contracts/grpc/parking_session_fee.proto`. Server Parking xử lý bằng `GetSessionFeeContextByQrQuery` tái sử dụng `sessions.FindOpenByQrTokenAsync`.
- **Phản hồi 404 đồng nhất (`payment.session.not_found`):** Trả về cùng một mã lỗi HTTP 404 RFC 7807 cho cả 3 trường hợp: (1) định dạng `qr` sai hoặc rỗng, (2) không tìm thấy phiên trong DB, (3) phiên đã `Completed`, `Cancelled` hoặc không còn `Active`. Ngăn chặn hoàn toàn kẻ xấu dò quét IDOR.
- **Tuyệt đối KHÔNG trả `paymentUrl` (Điều kiện 4 Tin 200):** Loại bỏ `paymentUrl` khỏi phản hồi DTO để tránh tác dụng phụ tạo giao dịch thanh toán VNPay trái phép từ một thao tác đọc HTTP GET. Khách thanh toán qua luồng checkout thanh toán riêng.
- **Mặt nạ dữ liệu cá nhân NFR-15:** Biển số xe bắt buộc được che theo định dạng `51F-***.89`. Không trả `userId`, `accountId` thô.
- **Giới hạn tần suất an toàn (Rate Limiting):** Cấu hình policy `AnonymousPublicFeePolicy` tại `PaymentRateLimiting.cs` giới hạn 20 req/phút/IP. Trả về HTTP 429 Too Many Requests kèm Header `Retry-After` và RFC 6585 ProblemDetails.

### Lý do thay đổi (Reason)
- Hỗ trợ khách vãng lai và người lái xe tra cứu phí đỗ xe tạm tính tức thì bằng cách quét mã QR trên vé giấy/phiên đỗ xe.
- Bảo vệ an toàn hệ thống chống dò quét mã QR, ngăn chặn tạo link thanh toán rác, và bảo vệ dữ liệu PII theo chuẩn NFR-15.

---

## 4. Email Notifications: Mail Xác Nhận Đặt Chỗ & Hóa Đơn Điện Tử Quyết Toán

### Trước thay đổi (Before)
- Notification Service chỉ ghi nhận thông báo nội bộ trong ứng dụng (`ICustomerNotificationStore`) khi nhận các sự kiện đặt chỗ và hoàn thành phiên đỗ.
- Chưa có tính năng gửi email xác nhận đặt chỗ hay email hóa đơn điện tử quyết toán cho người dùng.

### Sau thay đổi (After)
- **Hoàn thiện `ReservationConfirmedNotificationConsumer`:** Tiếp nhận `ReservationConfirmedIntegrationEvent` để gửi email xác nhận đặt chỗ chi tiết (thời gian hẹn, tiền cọc, mã đặt chỗ).
- **Thêm mới `ParkingSessionReceiptNotificationConsumer`:** Tiếp nhận `ElectronicInvoiceIssuedIntegrationEvent` phát từ Payment Service (chứa `MaskedPlateNumber` theo NFR-15, tổng phí, tiền đã cọc/trả, số tiền còn phải nạp, giờ thanh toán/hạn ra cổng với số tiền thật từ sổ thanh toán Payment).
- **Tra cứu email độc lập trước transaction Inbox:** Notification Service tra cứu thông tin email và trạng thái tài khoản từ Identity Service qua gRPC batch lookup trước khi bước vào transaction Inbox DB.
- **Điều kiện gửi email nghiêm ngặt (Điều kiện 6 Tin 200):** Chỉ gửi email cho tài khoản có email đã xác thực (`IsEmailVerified == true`). Với khách vãng lai (không có `AccountId`) hoặc tài khoản chưa xác thực email, consumer bỏ qua việc gửi email một cách an toàn (no-op, ghi log) mà không báo lỗi.
- **Tính Idempotent toàn vẹn:** Đăng ký consumer qua `AddIdempotentConsumer` và quản lý qua `IInboxExecutionCoordinator`, đảm bảo không gửi email trùng lặp khi RabbitMQ phân phối lại sự kiện.

### Lý do thay đổi (Reason)
- Cung cấp trải nghiệm thông báo toàn trình qua email cho khách hàng, giúp khách hàng lưu trữ hóa đơn điện tử và xác nhận giữ chỗ một cách minh bạch.
- Tránh spam email đến các tài khoản chưa kích hoạt hoặc địa chỉ email chưa được người dùng xác thực.

---

## 5. Staff List Enrichment Với DisplayName Và Masked Email

### Trước thay đổi (Before)
- API `GET /api/parking/owner/lots/{lotId}/assignments` chỉ trả về danh sách đối tượng chứa `accountId` dạng GUID.
- Giao diện Chủ bãi không thể hiển thị tên nhân viên và email để phân biệt các nhân sự đang trực bãi.

### Sau thay đổi (After)
- **Hợp đồng gRPC mới:** Bổ sung RPC `BatchLookupAccounts` trong `contracts/grpc/identity_staff.proto`.
- **Tối ưu hóa hiệu năng 1 RPC Call:** Parking Service gọi duy nhất một lệnh gRPC Batch sang Identity để lấy `DisplayName` và `Email` cho toàn bộ danh sách `accountId` trong bãi, loại bỏ hoàn toàn vấn đề N+1 query.
- **Mặt nạ bảo vệ email (PII Masking):** Email nhân viên được che một phần theo định dạng `u***@domain.com` (ví dụ: `nguyen.van.a@gmail.com` -> `n***@gmail.com`).

### Lý do thay đổi (Reason)
- Giúp Chủ bãi nhận diện và quản lý nhân viên bãi trực quan, thuận tiện.
- Bảo vệ dữ liệu cá nhân của nhân viên bãi và đảm bảo hiệu năng tối ưu của hệ thống liên dịch vụ.

