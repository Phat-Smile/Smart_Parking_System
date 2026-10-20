# TECH-02 · Seed dữ liệu dùng chung (Sprint 1, Khoa / BE-2)

**Tình trạng: PARTIAL, chờ review/CI và tích hợp.** Seed bổ sung Floor/Zone/Slot chỉ chạy khi migrations cho ba bảng đã được áp dụng; với code S1 cũ sẽ thông báo SKIP. Bảng giá thuộc nhánh BE-5/Owner và **chưa seed** do thiếu schema chính thức trong nhánh TECH-02.

## File và trách nhiệm

| File | Vai trò |
| --- | --- |
| `Seed-ParkingCatalog.sql` | 4 bản ghi ParkingLot cố định từ snapshot nguồn, dùng `ON CONFLICT(id) DO UPDATE` |
| `Validate-ParkingCatalogSeed.sql` | Assert đúng 4 bản ghi chuẩn, Approved 3/Pending 1, so khớp kiểu, tọa độ, tên |
| `Seed-ParkingLayout.sql` / `Validate-ParkingLayoutSeed.sql` | 3 tầng, 3 khu (Car/Motorbike), 3 chỗ ô tô; tự bỏ qua nếu schema chưa có; validate trong transaction |
| `run_parking_seed.py` | Dry-run mặc định; buộc xác nhận DB disposable; chạy seed + validate trong cùng PostgreSQL transaction |
| `tests/database/test_seed_runner.py` | 7 unit tests for dry-run, guards and SQL contract |
| `tests/Parking/Parking.IntegrationTests/ParkingSeedIntegrationTests.cs` | Testcontainers PostgreSQL 17; EF migration; chạy seed 2 lần; kiểm chứng public catalog/detail |

Các ID: `10000000-0000-0000-0000-000000000001` đến `...0004` **không được thay** vì FE/BE test có thể tham chiếu. Địa chỉ trong dữ liệu là **giả lập**, không phải bãi có thật. Script có quyền ghi đè bốn ID nên **không chạy trên production hay database đang chứa dữ liệu người khác sửa tay**.

## Thực thi

1. Checkout code và tạo `.env` theo tài liệu `docs/development/Local-Development.md`.
2. Bật PostgreSQL: `docker compose up -d postgres`.
3. Apply migration Parking: dùng `scripts/database/Update-DevelopmentDatabases.ps1` hoặc lệnh `dotnet ef database update` cho `ParkingDbContext` từ tài liệu local development. **Chưa có migration thì không chạy seed.**
4. Thử trước ở chế độ không ghi dữ liệu:

```sh
python scripts/database/run_parking_seed.py
```

5. Trên máy có `psql`, thiết lập libpq `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGPASSWORD` qua environment/secret manager. Tuyệt đối không paste password vào tham số dòng lệnh hoặc commit `.env`. Chỉ với **DB test dùng một lần có tên kết thúc bằng `_test` trên localhost** (không chạy trên `parking_system`):

```sh
export ASPNETCORE_ENVIRONMENT=Development
export PGHOST=127.0.0.1 PGPORT=5433 PGDATABASE=parking_system_seed_test PGUSER=parking_dev
# Export PGPASSWORD securely from local .env/secret manager (do not commit it).
python scripts/database/run_parking_seed.py \
  --apply --confirm-disposable I_UNDERSTAND_THIS_MUTATES_DATA
```

**Nếu máy không có `psql` nhưng chạy được Docker**, và đã kết nối tới database disposable, có thể dùng PostgreSQL CLI *bên trong container* bằng stdin:

```sh
# Apply migration before this command. Create parking_system_seed_test first; it must be disposable.
cat scripts/database/Seed-ParkingCatalog.sql \
    scripts/database/Seed-ParkingLayout.sql \
    scripts/database/Validate-ParkingCatalogSeed.sql \
    scripts/database/Validate-ParkingLayoutSeed.sql \
  | docker compose exec -T postgres psql -X -v ON_ERROR_STOP=1 \
      --single-transaction -U parking_dev -d parking_system_seed_test
```

Đảm bảo biết chính xác server/container mình đang thực thi và không áp dụng cách Docker này cho production. `ON_ERROR_STOP` + `--single-transaction` làm fail cả lệnh khi SQL assert không đạt. `SELECT count(*)` có thể kiểm tra thêm mà không chỉnh dữ liệu.

## Kiểm thử

```sh
# Không cần DB/Docker
python -m unittest discover -s tests/database -v

# Cần .NET SDK 10.0.302 và Docker Testcontainers
# Trong repo root:
dotnet test tests/Parking/Parking.IntegrationTests/Parking.IntegrationTests.csproj
```

**Kiểm tra thực tế trong môi trường soạn:** Python test đã chạy; không có `dotnet`, `psql`, `docker`, do đó bài tích hợp PostgreSQL vẫn **NOT EXECUTED**.

## Phụ thuộc chưa đóng

- BE-4: cần migrate Floor/Zone/ParkingSlot trên DB test trước nếu muốn kiểm tra seed layout; không tạo migration ở TECH-02.
- BE-5/owner bảng giá thực tế: schema + bảng giá; chỉ owner schema tương ứng được seed và duy trì script. TECH-02 **chưa hoàn thành phần seed giá**.
- Khoa + reviewer: chạy SQL 2 lần trên database test sạch; xác nhận public GET chỉ có 3 Approved; test PR, review và merge.

**Tiêu chí `Xong`:** migration được merge; seed khu/chỗ/giá được xác nhận theo ownership; script chạy 2 lần không trùng; cả test Python và test .NET/Postgres pass; PR reviewed/merged; master-sheet cập nhật có bằng chứng. Không được đánh dấu `Xong` nếu bất kỳ dependency nào còn mở.
