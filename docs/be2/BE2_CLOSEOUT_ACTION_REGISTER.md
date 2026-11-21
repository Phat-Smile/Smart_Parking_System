# BE-2 — Sổ đóng việc tổng thể (21/11/2026)

**Mục tiêu:** Khép toàn bộ danh mục BE-2 mà **không chép lại thay đổi của Lộc hoặc người khác**, không tự nhận đã nghiệm thu khi chưa có bằng chứng. Đây là lịch kiểm duyệt và xử lý, **không phải chứng nhận release**. Nguồn: `BE_V4 (1).xlsx`, `context.docx`, `SRS.docx`, `phan-cong-tranh-trung.md`, `tong-ket-tuan-16-20-11.html`, các ZIP log BE-2 đã gửi đến 21/11.

## A. Tổng phạm vi và tình trạng hiện tại

| Nhóm | Số lượng | Trạng thái đúng | Xử lý đã thực hiện / cần đóng |
|---|---:|---|---|
| Backlog BE-2 OJT | **22 / 72 SP** | Chưa có bằng chứng đóng toàn bộ | Đã dựng ma trận 22 task với chủ sở hữu, điều kiện chứng nhận; đối chiếu từng dòng với develop và PR thực tế |
| LightGBM sau OJT | **1 / 8 SP** | Tách khỏi release OJT | Chỉ làm sau khi baseline và dữ liệu thật được chốt |
| Task lịch sử `Xong` trong Excel | **15** | Chỉ có ghi nhận cũ | Đối chiếu source/AC/test trên develop; không viết lại 15 chức năng |
| 4 nhánh tách #329 | **Lộc đang làm** | Đã giao người phụ trách | **Không code lại** AI Ranking, Gate Arrival, cảm biến/Pricing/Privacy, GPS Dashboard |
| PR tích hợp cũ | **52 file** | Đã push, nhưng không được merge nguyên | So sánh Git cũ: **36 trùng blob**, **6 cùng path khác nội dung**, **10 chưa trùng trực tiếp**. Chờ semantic review 16 path; không cherry-pick bừa |
| FR/ANPR evidence Phase 1 | 13 file | Đã push `127af506…` | Không tạo lại; công cụ mới chỉ bổ sung kiểm chứng còn thiếu |
| Vehicle classifier baseline | 8 file | Đã push `d4632b47…` | Model offline hoạt động trên ảnh tổng hợp; **chưa nghiệm thu dữ liệu camera thật** |

## B. Việc mình thực hiện ngay trong gói này

| Mã | Việc | Sản phẩm thực thi | Kết quả được phép công bố |
|---|---|---|---|
| CLOSE-01 | Kiểm duyệt toàn bộ **23 task/80 SP**, 22 task/72 SP OJT | `closeout-matrix.json`, `audit.mjs`, báo cáo Markdown/JSON từ Git local | Có quyết định chủ sở hữu/DoD từng task; **không tự đánh dấu Done** |
| CLOSE-02 | Xác minh **16 file** còn tranh chấp | Đọc blob Git ở develop, nhánh tích hợp, bốn nhánh Lộc; so sánh SHA, ghi thiếu refs là chưa xác minh | Phân biệt đã giống develop / giống split / cần xem khác biệt. Không sửa Git |
| CLOSE-03 | Khóa chất lượng train/test US-AI-004 | `dataset-audit.mjs`: kiểm tra cùng xe, cùng phiên ghi hình, SHA ảnh trùng, pHash gần trùng qua các split; báo cáo tổng hợp không chứa định danh | Chặn rò rỉ dữ liệu; **không suy ra mô hình chính xác** |
| CLOSE-04 | Kiểm thử công cụ, chuẩn hóa log | Node test TAP, một worker; ZIP log; phát hành nhánh công cụ riêng | Có thể commit/push **chỉ file công cụ mới** khi PASS; không đụng source Lộc |

## C. Toàn bộ những việc còn phải kiểm duyệt/giải quyết, theo đúng thứ tự phụ thuộc

| # | Phạm vi | Ai phụ trách | Công việc cụ thể | Điều kiện đóng, không tự đoán |
|---|---|---|---|---|
| 1 | **15 task lịch sử đã Xong**: Seed, Mapbox, search/catalog, owner/admin, layout, map, IoT simulator | Khoa kiểm duyệt; code owner theo backlog/nhóm | Đọc develop mới nhất, API và tests; chỉ tạo issue với phần thật sự thiếu | Gắn đúng commit, test liên quan, review owner vào **từng dòng 15 task** |
| 2 | **US-SEARCH-006 OSRM ETA** | Khoa + Lộc (#329) | So sánh ETA + cache/fallback trên nhánh split, không ghi đè | Split branch chốt logic giữ/bỏ, test ETA khi provider lỗi và timezone đúng |
| 3 | **US-AI-002 Arrival Availability** | Khoa + Lộc (#329) | Đối chiếu held quota, mẫu dữ liệu thiếu và cảnh báo tin cậy | Hợp đồng đầu ra + test nguồn dữ liệu thực + xác nhận owner |
| 4 | **US-AI-001, US-PRIV-001, IoT/Gate/Pricing/Frontend GPS** | **Lộc** | Không làm lại; chỉ kiểm độc lập sau 4 PR | 4 PR tích hợp và được owner nghiệm thu; không nhận branch chưa merge thành develop |
| 5 | **US-AI-004 dữ liệu & mô hình** | **Khoa** cho baseline/đánh giá; Lộc giữ OCR UI | Đã viết baseline (nhánh `…VehicleTypeBaseline`) và **bộ phát hiện leakage mới**; tiếp theo dùng dữ liệu thật có quyền sử dụng | Dữ liệu tách theo cùng xe/recording session, đủ class/ánh sáng/góc máy; metrics holdout và threshold **được Lead duyệt**, manual-review fallback. **Không có dữ liệu thì BLOCKED_DATA** |
| 6 | **TECH-14 Performance** | Khoa chuẩn bị bằng chứng, Lộc hoặc máy QA chạy | Chỉ lập workload và thu thập số đo từ QA biệt lập; không chạy load trên ACER | NFR-02 realtime <2s, NFR-04 AI <1s dưới tải được thống nhất; không dùng 8-request smoke làm chuẩn |
| 7 | **TECH-15 Business E2E MF-01→13** | **Lộc** | Không dựng trùng stack; nhận kết quả Booking→Gate→Session→Payment→Exit từ người chạy | E2E thật, có log và lỗi phân loại rõ; không cho synthetic test thay thế |
| 8 | **BE-PK03 hợp đồng tính tiền** | **BE-5 + Lead**, Khoa chỉ phối hợp | Giá block đầu/tiếp, overnight, version, wallet/refund settlement | Công thức được chốt và các ca biên tiền được test trước khi sửa Pricing/Payment |
| 9 | **Nhân viên thấy phút đến trễ/no-show** | Chưa giao rõ; giao thoa Lộc #334, BE-3 | Đề xuất owner duy nhất rồi làm implementation ở module được cho phép | Booking & UI thống nhất timestamp/no-show và không thay đổi luồng nhắc đã giao |
| 10 | **Lệnh mở barrier cổng ra liên kết sự kiện camera** | Chưa giao rõ; BE-4 và Lộc | Xác nhận ai sửa Gate/StaffExit và hợp đồng event | Không mở sai xe, có audit/correlation ID, test đọc sai biển, thanh toán chưa xong |
| 11 | **16 file chưa chốt** | Lộc/Lead và Khoa | 6 divergent review semantic, 10 chưa trùng xét phạm vi; không stage/cherry-pick | Mỗi file có kết luận `already covered` / `intentionally refactored` / `missing with assigned owner` |
| 12 | **Tất cả FR và bàn giao** | Lead + các owner | Gom bằng chứng 23 task, 2 cross-service gaps, dữ liệu/model, NFR, E2E; không kéo Jenkins lên máy cá nhân | PR đúng convention, review và CI theo chính sách nhóm; **squash merge** do nhóm thực hiện |

## D. Không chờ nhau ở những việc độc lập

- Khoa có thể chạy ngay công cụ ma trận, diff Git read-only và QA data split — **đã viết trong gói này**.
- Lộc có thể tiếp tục bốn #329 và #332/#333/#334, UI, demo/E2E. Không phát hành bản sửa trùng.
- Bách giữ RefundRequests/migration Payment. Đình Anh giữ owner editor/staff. BE-5 và Lead xác nhận công thức giá. BE-3/BE-4 cùng Lead phân công đúng một chủ sở hữu cho hai hạng mục Gate/Booking giao thoa.
- **Không chạy Jenkins, Docker, .NET, model training hay benchmark load** trên máy ACER bằng script này. Quy định Jenkins xanh trước **merge** là việc của hạ tầng nhóm, không phải yêu cầu khởi động Jenkins trên máy người dùng.

## E. Trạng thái kết thúc và chống công bố nhầm

**Đã code và kiểm tra trong gói:** ma trận 23 task, so sánh 16 file theo Git, kiểm tra rò rỉ data split, test tự động công cụ. **Chưa có dữ liệu để kết luận:** mô hình ảnh camera thật, p95 NFR thực tải, E2E thật, hợp đồng giá BE-5, kết luận semantic của Lộc trên 16 file. Những mục đó được giữ `BLOCKED/OWNED_ELSEWHERE` chứ không biến thành `PASS`.

**Phạm vi Git:** chỉ các file mới thuộc `tools/be2-closeout/` và tài liệu `docs/be2/`; không chỉnh các PR #329, không rewrite nhánh có merge commit, không push `develop`, không force-push. Commit title tiếng Anh: `test(be2): add comprehensive closeout and dataset guards`.
