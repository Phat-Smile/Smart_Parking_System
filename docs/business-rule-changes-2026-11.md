# Thay Đổi Luật Nghiệp Vụ (Business Rule Updates) — Tháng 11/2026

Tài liệu này ghi lại các điều chỉnh luật nghiệp vụ cho hệ thống Smart Parking / ParkWise, áp dụng trong đợt cập nhật PR B (`features/Implementation_StaffGateConfirmation`).

---

## 1. BR-05.a: Xác Nhận Lượt Đọc Biển Số Cổng Từ Trình Duyệt Web (Gate Reading Web Client Staff Confirmation)

### 1.1. Trước thay đổi
- Mọi lượt nhận diện biển số (Gate Reading) từ camera cổng hoặc thiết bị quét gửi về qua `POST /api/parking/gate-readings` đều được phân loại tự động dựa trên độ tin cậy nhận diện (Confidence):
  * Confidence >= 85% và biển số hợp lệ: Tự động chấp thuận (`Accepted`), không yêu cầu nhân viên xác nhận.
  * Confidence < 85% hoặc không đọc được biển số: Mở sự cố `LowConfidencePlate` để nhân viên xác nhận (`PendingStaff`).
- Quy định này không phân biệt nguồn gửi là thiết bị cổng phần cứng chuyên dụng (IoT Gate Camera / Device) hay trình duyệt web của nhân viên trực quầy (Web Client).

### 1.2. Sau thay đổi (Cập nhật GATE-A4 / Q-A4)
- Bổ sung trường `Source` ("device" | "web") trong `RecordGateReadingCommand` và request body API:
  * Nguồn thiết bị (`Source == "device"`): Tiếp tục áp dụng quy tắc tự động nhận diện theo ngưỡng độ tin cậy 85%.
  * Nguồn trình duyệt web (`Source == "web"`): **100% các lượt đọc biển số gửi từ trình duyệt web của nhân viên đều bắt buộc nhân viên xác nhận (`PendingStaff`)**, mở sự cố `LowConfidencePlate` với lý do `StaffConfirmationRequired`.
- Trên giao diện Web Staff (`StaffEntryPage.tsx`), các lượt đọc từ camera web hiển thị rõ trạng thái chờ nhân viên kiểm tra, xác nhận hoặc hiệu chỉnh biển số trước khi mở rào và lập phiên gửi xe.

### 1.3. Lý do thay đổi
- Camera web hoặc thiết bị stream qua trình duyệt thường có góc quay, độ phân giải, độ trễ và điều kiện ánh sáng không ổn định bằng camera công nghiệp tại cổng chuyên dụng.
- Ngăn chặn việc nhân viên bấm nhầm hoặc camera web vô tình quét phải biển số xe đi ngang qua / xe đậu gần cổng mà hệ thống tự động mở rào chắn ngoài ý muốn.
- Tăng cường trách nhiệm đối soát trực quan (Human-in-the-Loop) của nhân viên trực cổng khi thao tác trên ứng dụng web, đảm bảo tính chính xác tuyệt đối của phiên gửi xe vào bãi.

---

## 2. BR-09.d: Thời Hạn Rời Bãi và Cơ Chế Gia Hạn Một Lần (Exit Window & One-Time Extension)

### 2.1. Trước thay đổi
- Sau khi thanh toán cước phí gửi xe (trạng thái phiên chuyển sang `Paid`), khách hàng có thời hạn 15 phút (`ExitWindow = TimeSpan.FromMinutes(15)`) tính từ thời điểm thanh toán (`LastPaidAt`) để đưa xe ra khỏi bãi.
- Quy định so sánh cứng: Nếu thời điểm kiểm tra xuất bãi (`CompleteParkingSession` hoặc `VerifyParkingSessionExit`) vượt quá `LastPaidAt + 15 phút` (tính đến từng giây, T + 15:01 là quá hạn), phiên tự động bị thu hồi về trạng thái `Active`, rào chắn giữ đóng và khách bắt buộc phải thanh toán thêm phụ thu trước khi được phép xuất bãi.
- Không có cơ chế xử lý khi xảy ra sự cố khách quan tại bãi (ùn tắc cục bộ tại cổng ra, trục trặc cơ học cần barrier, xe phía trước chết máy, sự cố đường truyền...).

### 2.2. Sau thay đổi (Cập nhật Q-EXIT-WINDOW)
1. **Ghi nhận thời điểm xe tới cổng ra (`ExitArrivedAt`)**:
   - Khi xe tới cổng ra và hệ thống thực hiện kiểm tra xuất bãi lần đầu (qua camera đọc biển số, quét mã QR hoặc nhân viên đối soát), thời điểm tới cổng `ExitArrivedAt` được ghi nhận cố định vào phiên đỗ.
   - Thời hạn xuất bãi 15 phút được tính so khớp theo thời điểm xe tới cổng (`effectiveArrival = ExitArrivedAt ?? arrivedAt`). Nếu xe đã tới cổng trong vòng 15 phút sau khi thanh toán, quyền xuất bãi của xe được bảo lưu, không bị phạt quá hạn ngay cả khi quá trình mở rào chắn hoặc đối soát kéo dài quá 15 phút.
2. **Cơ chế gia hạn 15 phút một lần duy nhất (`ExtendExitWindow`)**:
   - Cung cấp endpoint bảo vệ phân quyền Staff: `POST /api/parking/sessions/{sessionId}/extend-exit-window`.
   - Nhân viên trực quầy được phép gia hạn thêm **15 phút một lần duy nhất** (`HasExitWindowExtension = true`) cho phiên xe đang ở trạng thái `Paid`.
   - Thời hạn rời bãi mới: `ExitWindowEndsAt = LastPaidAt + 15 phút + 15 phút = LastPaidAt + 30 phút`.
   - Bắt buộc phải có lý do giải trình (`Reason`), độ dài tối đa 500 ký tự.
   - Mọi lượt gia hạn đều được ghi nhận vào nhật ký kiểm toán hệ thống (`IAuditLog` với hành động `AuditActions.SessionExitWindowExtended = "session.exit_window_extended"`), lưu vết ai thực hiện, bãi xe nào, thời điểm, lý do và mốc thời gian hết hạn mới.
   - Hệ thống từ chối gia hạn lần 2 hoặc gia hạn cho phiên không ở trạng thái `Paid`.
3. **Cập nhật giao diện Staff Exit (`StaffExitPage.tsx`)**:
   - Khi phiên xe đã thanh toán (`Paid`), giao diện cung cấp nút **"Gia hạn 15 phút"**.
   - Bấm nút sẽ mở modal chuyên biệt yêu cầu nhập lý do bắt buộc (tối đa 500 ký tự, đếm ký tự thực tế, vô hiệu hóa nút xác nhận khi lý do để trống).
   - Xác nhận gia hạn thành công sẽ hiển thị thông báo phản hồi trực quan và cập nhật trạng thái phiên tức thì.

### 2.3. Lý do thay đổi
- Khắc phục bất cập thực tế: Khách hàng đã thanh toán và đã di chuyển tới cổng ra đúng hạn, nhưng vì bãi xe đông đúc, xếp hàng dài hoặc barrier gặp sự cố mà bị phạt tính thêm tiền oan uổng.
- Trao quyền xử lý linh hoạt cho nhân viên trực bãi khi có sự cố bất khả kháng, đồng thời duy trì kỷ luật kiểm toán nghiêm ngặt thông qua ghi nhận lý do bắt buộc và nhật ký `IAuditLog`.
