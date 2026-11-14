# Simulated Arrival History Generator (WP-AI-D1)

Bộ script Python thuần mô phỏng lịch sử phiên đỗ xe thực tế cho môi trường phát triển / demo Smart Parking, phục vụ bài toán dự báo AI còn chỗ (`BR-08.e`, `IArrivalSnapshotReader`).

## Cấu trúc thư mục
- `profiles.py`: Cấu hình "tính cách" và trọng số theo giờ cho 3 bãi đỗ đã duyệt (Lot A, B, C) khớp với `Seed-ParkingCatalog.sql` và `Seed-ParkingLayout.sql`.
- `generate_arrival_history.py`: Script sinh dữ liệu phiên đỗ mô phỏng (hạt giống cố định, đảm bảo không vượt sức chứa, không trùng phiên xe).
- `test_arrival_generator.py`: Bộ unit test kiểm tra tính tất định, bất biến sức chứa, không chồng lấn biển số và an toàn lược đồ.
- `eval_predictions.py`: Bộ công cụ đánh giá AI-D2 tính chỉ số Brier Score và MAE tách theo các khung giờ (Cao điểm sáng/trưa/chiều/tối/ngoài giờ).
- `Cleanup-ArrivalHistory.sql`: Tệp SQL dọn dẹp an toàn các phiên mô phỏng (dựa theo tiền tố biển số `SIMC%` và `SIMM%`).
*(Lưu ý: Tệp `Seed-ArrivalHistory.sql` sinh ra ~1.7 MB không commit vào Git theo tin 031; sinh lại tức thì bằng lệnh bên dưới).*

## Hướng dẫn sử dụng

### 1. Chạy sinh dữ liệu SQL
```bash
python generate_arrival_history.py --days 35 --seed 42 --out-dir .
```

Các tham số:
- `--days`: Số ngày mô phỏng lịch sử (28 đến 56 ngày, mặc định: 35).
- `--seed`: Hạt giống ngẫu nhiên (mặc định: 42).
- `--out-dir`: Thư mục lưu tệp SQL (mặc định: thư mục hiện tại).
- `--dry-run`: Chỉ in số liệu thống kê phiên đỗ, không ghi tệp.

### 2. Chạy kiểm thử tự động
```bash
python -m unittest test_arrival_generator.py
```

## Các quy tắc bất biến được bảo đảm
1. **Sức chứa từng khu:** Số xe hoạt động đồng thời tại bất kỳ thời điểm nào không bao giờ vượt quá sức chứa phân khu (`CAR-A`: max 2 ô tô, `MOTO-B`: max 20 xe máy, `CAR-C`: max 1 ô tô).
2. **Không trùng lịch cùng xe:** Không có xe nào (theo biển số) có 2 phiên đỗ trùng hoặc đè lên nhau về thời gian.
3. **Chuẩn hóa biển số:** Biển số viết hoa, không dấu gạch nối, đúng chuẩn hệ thống. Dải biển số thực tế sinh ra: Ô tô `SIMC001` - `SIMC052` (52 xe), Xe máy `SIMM001` - `SIMM136` (136 xe).
4. **Chuẩn hóa thời gian:** `entry_time < last_paid_at <= exit_time`, thời gian rời bãi sau khi thanh toán luôn nằm trong cửa sổ 15 phút (`BR-09.d`).
5. **Dọn dẹp an toàn:** Tệp `Cleanup-ArrivalHistory.sql` chỉ xóa các bản ghi có biển số tiền tố `SIMC%` và `SIMM%`, không ảnh hưởng tới dữ liệu thật hay dữ liệu demo khác.

## 3. Chạy đánh giá dự báo AI-D2 (Brier / MAE)
```bash
python eval_predictions.py
```
Hỗ trợ cờ `--csv` xuất bảng chi tiết và `--synthetic` để đánh giá trên dữ liệu mô phỏng khi không kết nối trực tiếp database.
