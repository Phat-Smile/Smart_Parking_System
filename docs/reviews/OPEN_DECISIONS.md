# OPEN_DECISIONS.md
> Các điểm quyết định chưa chốt — tính đến 2026-10-15
> **KHÔNG được tự chốt — cần ý kiến nhóm/mentor**

---

## Nhóm 1 — Architecture / Tech

| ID | Câu hỏi | Service liên quan | Impact |
|---|---|---|---|
| OD-ARCH-01 | **Cookie SameSite=Strict vs None:** FE dev chạy port 3000, Gateway port 5000 → cross-origin. Cookie SameSite=Strict sẽ bị block. Cần Lax hoặc None+Secure, hoặc đồng nhất origin qua proxy? | Identity, Frontend | Auth UX trên dev env |
| OD-ARCH-02 | **Mapbox token type cho AI service:** AI service tính ETA (routing) phía server — dùng Mapbox Directions API cần secret token, hoặc giữ OSRM (open-source)? | AI Service | Cost, offline capability |
| OD-ARCH-03 | **Mapbox quota plan:** Free tier giới hạn 50k map loads/tháng, 100k geocoding requests/tháng. OJT demo có vượt không? | Frontend | Cost risk |
| OD-ARCH-04 | **`isCurrentSession` field:** Detect bằng JWT `jti` claim hay session ID từ cookie? `jti` cần được lưu vào `AuthSession`. Hiện tại không rõ `jti` có được persist không. | Identity | Sessions list UX |

---

## Nhóm 2 — Business Logic (từ SRS/BR)

| ID | Câu hỏi | Ref | Impact |
|---|---|---|---|
| OD-BIZ-01 | **Bảng trạng thái Reservation:** Sau khi Pending → timeout bao lâu? Cancel có refund không? State machine chưa được định nghĩa đầy đủ. | Điểm 33 | Booking service design |
| OD-BIZ-02 | **Tranh chấp chỗ cuối:** Nếu Hold chỗ cuối cùng và xe vãng lai đến cùng lúc — ai được ưu tiên? | Điểm 37, BR-03 | Parking capacity service |
| OD-BIZ-03 | **VNPay refund cho guest:** Guest không có ví nội bộ. BR-09.c vs BR-10.e mâu thuẫn về cách hoàn tiền khi hủy đặt chỗ. | Điểm 36 | Payment service design |
| OD-BIZ-04 | **Access token sau đổi mật khẩu:** Token cũ có bị invalidate không? Cần revoke mechanism hay chờ expire? | — | Identity security |
| OD-BIZ-05 | **Thứ tự ưu tiên SpotStatus:** Khi ô có nhiều state (`Reserved`, `Occupied`, `OutOfService`) — state nào thắng khi hiển thị? | — | Parking display |
| OD-BIZ-06 | **Event names xe vào/ra:** "VehicleEntered" hay "ParkingSessionOpened"? Owner là Identity hay Parking? Payload gồm những gì? | — | Event contract, Notification |

---

## Nhóm 3 — PR Merge (cần action ngay)

| ID | Vấn đề | Người cần quyết định |
|---|---|---|
| OD-PR-01 | 6 feature branches của Booking/Payment/Parking chưa được merge vào develop — review và approve | Team Lead (Lộc) |
| OD-PR-02 | Jenkinsfile trên tất cả branches có diff lớn (100 lines) — có conflict không? Cần squash hay preserve history? | Phát (CI owner) |

---

## Nhóm 4 — Cập nhật tài liệu bắt buộc

| ID | Tài liệu cần cập nhật | Lý do |
|---|---|---|
| OD-DOC-01 | `.notes/CLAUDE.md` — xóa "Không còn Mapbox", thêm "Dùng Mapbox" | FE đã dùng Mapbox, user confirm |
| OD-DOC-02 | `memory/architecture-decisions.md` — update Mapbox decision | Stale memory |
| OD-DOC-03 | `memory/feedback-project-rules.md` — update Mapbox rule | Stale memory |

---

## Nhóm 5 — Chưa có spec

| ID | Thiếu gì | Cần từ ai |
|---|---|---|
| OD-SPEC-01 | Parking Lot Approval workflow — ai approve? Quy trình ra sao? | BA / Mentor |
| OD-SPEC-02 | Vehicle CRUD API design cho REST (gRPC lookup đã có) | Lộc viết spec |
| OD-SPEC-03 | Admin "Activate account" — activate từ trạng thái nào? Suspended only hay cả Disabled? | BA / Mentor |
