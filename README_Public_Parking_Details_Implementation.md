# Triển khai thông tin bãi đỗ đầy đủ + dữ liệu QA — ParkWise

> **Bản patch để áp dụng và kiểm chứng, chưa triển khai lên máy người dùng.**
>
> **Source để soạn patch:** ZIP `Smart_Parking_System-develop-full-src-a40485e990.zip`, snapshot `a40485e990` ngày 10/11/2026. Máy người dùng từng kiểm thử `develop` local ở `e375f27`; **phải `git apply --check` trước**, không mặc định 2 snapshot giống nhau.
>
> **Không có .NET SDK / dependencies frontend trong môi trường đóng gói:** chưa build .NET, chưa chạy full tests, chưa E2E; không báo PASS khi chưa chạy. Dữ liệu seed là **DEMO / QA**, không phải bãi thật, không có ảnh thật hay pháp lý được xác minh.

## 1. Có gì trong bản thay đổi

- `GET /api/parking/lots/{id}`: giữ các trường cũ, **thêm** `compatibility`, `amenities`, `prebookable`, `bookableVehicleTypes`, `floorCount`, `carCapacity`, `motorbikeCapacity`, `publicInfo` (nullable). Các trường về loại xe và tiện ích dùng **cùng `CustomerLotFeaturesReader` với `/search`** để giảm khả năng FE/BE lệch contract.
- `publicInfo` được lưu dạng `jsonb` nullable trên `parking.parking_lots.public_info`, chứa mô tả, hướng dẫn vào bãi, nội quy, ảnh tham chiếu, cờ `photosAreIllustrative`, phương thức thanh toán do đơn vị vận hành công bố và số điểm sạc xe điện.
- Owner (JWT + check owner assignment) có thể `PUT /api/parking/owner/lots/{lotId}/public-info` để cập nhật các trường mô tả, kèm validation + audit. URL ảnh **chỉ trỏ đến asset `/images/parkwise/` đã có**; **chưa thực hiện upload ảnh thật**.
- FE thực tế hiển thị các trường công bố, số tầng/sức chứa, nhãn ảnh minh họa, sơ đồ tầng/khu vực/ô đỗ từ public `/layout` (thông tin đọc thật, không dùng mock spots), và **không cho chọn xe máy để tạo booking ô tô**. Giá/total chỉ được suy từ chính sách CAR `Block` 60 phút; điều kiện giá khác không được tự tính sai.
- Không thay đổi booking transaction/payment, không tạo đặt trước cho xe máy. Không giả mạo số ô cụ thể `A-01` trong toast.
- Script seed idempotent cho 3 bãi demo A/B/C trong **database đúng tên** `parking_system_qa_migration`. Seed giờ mở cửa, feature flags, bảng giá demo, nội quy, ảnh minh họa, hướng dẫn, thông tin sạc; bảo đảm B có khu xe máy nếu chưa có.
- Script kiểm chứng **read-only** 11 assertion API trên bãi B.

## 2. Phạm vi đầy đủ theo 10 nhóm thông tin

| Nhóm | Cách đáp ứng từ code + seed | Giới hạn |
|---|---|---|
| Nhận diện | Tên/địa chỉ/loại hiện có; `publicInfo` mô tả, ảnh minh họa QA | Chưa có ảnh thật, upload/private media/CDN |
| Vị trí | Latitude/longitude, `/nearby` hiện có, link chỉ đường FE | Dữ liệu tọa độ A/B/C là ví dụ, không xác thực địa chỉ ngoài đời |
| Giờ hoạt động | `openingHours` từ DB; seed bảy ngày, 24h hoặc 06–22h | Cần Owner xác nhận giờ thật |
| Loại xe | `compatibility` chung với `/search` | Car-only booking không đồng nghĩa cấm xe máy gửi trực tiếp |
| Sức chứa | Floor/zone/slot, tổng Car/Motorbike; availability realtime API | Chỗ trống **hiện tại** không bảo đảm chỗ tại ngày đặt |
| Cấu trúc | API `/layout` đã tồn tại; FE mới hiển thị tầng/khu vực/ô theo dữ liệu thật | Chưa có 3D model động cho bãi thật (không giả dùng 3D preview) |
| Tiện ích | `amenities` từ flags Owner và `evChargingPoints` metadata | Điểm sạc demo, chưa xác minh thiết bị thật |
| Giá | Pricing API hiện có; seed CAR/motorbike demo | Phí đêm và báo giá hoàn chỉnh khi qua ngày cần kiểm chứng Booking/Pricing |
| Đặt trước | `prebookable` / `bookableVehicleTypes` và FE chỉ cho CAR | Tiền cọc BookingDialog vẫn phải đối chiếu policy, không nên release chưa test |
| Quy định | Nội quy, hướng dẫn vào bãi, phương thức thanh toán trong `publicInfo` | Dữ liệu QA có tiền tố `[DEMO]`; không phải chính sách pháp lý |

## 3. Áp dụng code an toàn (không sửa nhánh OSRM, không push tự động)

**Chỉ nên áp dụng ở bản copy/nhánh feature mới bắt nguồn từ develop đã fetch.** Ví dụ trong Git Bash:

```bash
cd /d/OJT26/Smart_Parking_System
# Nếu có mạng: git fetch origin
# Tạo worktree riêng; nhánh mới chưa tồn tại.
git worktree add -b features/Implementation_PublicParkingLotDetails \
  /d/OJT26/Smart_Parking_System_PublicDetails origin/develop
cd /d/OJT26/Smart_Parking_System_PublicDetails

# Đặt file .patch cạnh thư mục project (ví dụ /d/OJT26/...)
git apply --check --ignore-whitespace /d/OJT26/SmartParking_PublicDetails_QA.patch
# --ignore-whitespace chỉ xử lý khác biệt CRLF/LF của snapshot ZIP.
# Nếu --check báo hunk failed: DỪNG, cần chỉnh patch theo source develop thực tế.
git apply --ignore-whitespace /d/OJT26/SmartParking_PublicDetails_QA.patch

git diff --stat
```

**Không dùng `git reset --hard`, `git clean -fd`, `git push` hoặc `git rebase` để ép patch.** Vì patch được soạn trên snapshot `a40485e990`, có thể cần chỉnh thủ công nếu file trên `e375f27` đã khác.

## 4. Build/test code (chưa chạy trong môi trường đóng gói)

```bash
# Thực hiện trong worktree có patch
 dotnet build src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj
 dotnet test tests/Parking/Parking.UnitTests --filter-class '*ConfigurePublicInfoTests'
 dotnet test tests/Parking/Parking.IntegrationTests --filter-class '*LotDetailPricingAvailabilityHttpTests'
 dotnet test tests/Parking/Parking.IntegrationTests --filter-class '*NearbyParkingLotIntegrationTests'

 cd web
 npm ci
 npm run build
 npm test -- --run
```

Nếu `npm run build`/`npm test` trên source mới khác script, kiểm tra `web/package.json`. Testcontainers cần Docker. Khi test fail, giữ log rồi phân tích, không sửa assertion để ép PASS.

## 5. Migrate database QA, tuyệt đối không cập nhật DB gốc

Bạn đã có `parking_system_qa_migration` chứa placeholder cho 7 orphan sessions; migration đã đến `20261020030000_AddParkingLotOpeningHours`. **Cần thêm các migration về features/layout trước khi seed.**

Tại repo có compose (Git Bash):

```bash
cd /d/OJT26/Smart_Parking_System
# Backup database QA, chứa cả placeholder đã dùng để thông migration FK
 docker compose exec -T postgres \
   sh -c 'pg_dump -U "$POSTGRES_USER" -d parking_system_qa_migration -Fc' \
   > /d/OJT26/qa_before_public_details.dump
 test -s /d/OJT26/qa_before_public_details.dump || exit 1

QA_DB_PORT="$(docker compose port postgres 5432 | tr -d '\r' | awk -F: '{print $NF}')"
QA_CONN="$(docker compose exec -T postgres \
  sh -c 'printf "Host=127.0.0.1;Port=%s;Database=parking_system_qa_migration;Username=%s;Password=%s" "$1" "$POSTGRES_USER" "$POSTGRES_PASSWORD"' \
  sh "$QA_DB_PORT")"
[[ "$QA_CONN" == *"Database=parking_system_qa_migration;"* ]] || exit 1
[[ "$QA_CONN" == *"Port=5433;"* ]] || exit 1
```

Trong **worktree mới có patch** và **cùng terminal đã tạo `QA_CONN`**:

```bash
cd /d/OJT26/Smart_Parking_System_PublicDetails
ConnectionStrings__DefaultConnection="$QA_CONN" \
 dotnet ef database update 20261112080000_AddParkingLotPublicInfo \
 --project src/Services/Parking/Parking.Persistence/Parking.Persistence.csproj \
 --startup-project src/Services/Parking/Parking.WebAPI/Parking.WebAPI.csproj \
 --context ParkingDbContext --connection "$QA_CONN"
```

**Chỉ chạy seed sau khi migration hoàn tất.** Quay về repo gốc nơi chạy Docker:

```bash
cd /d/OJT26/Smart_Parking_System
 docker compose exec -T postgres \
   sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d parking_system_qa_migration' \
   < /d/OJT26/Smart_Parking_System_PublicDetails/scripts/database/Seed-ParkingPublicDetail-QA.sql
```

SQL sẽ **RAISE EXCEPTION và rollback** nếu không phải database `parking_system_qa_migration`, nếu thiếu migration `public_info`, hoặc nếu không đủ ba bãi Approved. Không chạy script trên DB gốc hay production. Không chạy seeder với `Seeding__Reset=true`.

## 6. Kiểm tra HTTP và E2E

Dừng **Parking API QA cũ ở 5502**, chạy lại API từ worktree mới với `ConnectionStrings__DefaultConnection="$QA_CONN"`, `Seeding__Enabled=false`, cổng QA 5502; giữ RabbitMQ QA tách riêng như runbook trước. Các container khác vẫn hoạt động độc lập.

```bash
curl -sS 'http://localhost:5502/api/parking/lots/10000000-0000-0000-0000-000000000002' | python -m json.tool
node scripts/database/verify-parking-public-detail.mjs http://localhost:5502
```

**QA chỉ đạt** nếu API có `openingHours.days[7]`, `pricing` chứa CAR, `availability.motorbike` không null, `compatibility`/`amenities` cùng nguồn search, `publicInfo` rõ `[DEMO]`, và FE nhìn thấy nội dung tương ứng. Kiểm thử FE Network và Browser: chọn bãi B, xem marker + trang chi tiết, loại xe moto không được gửi hold Car, ảnh được gắn nhãn minh họa, timezone, price missing, Pending 404, quyền owner PUT, các trường nullable. Giá/Booking phải E2E với Booking + Payment trước release.

## 7. API mới / cập nhật

`GET /api/parking/lots/{id}` (public Approved):

```json
{
  "id": "10000000-0000-0000-0000-000000000002",
  "name": "Bãi đỗ Ngoài trời B",
  "address": "02 Đường Mẫu, TP. Hồ Chí Minh",
  "latitude": 10.7821,
  "longitude": 106.6954,
  "type": "Outdoor",
  "openingHours": { "days": [] },
  "pricing": [],
  "availability": { "car": { "available": 10 }, "motorbike": null },
  "compatibility": ["car", "motorbike"],
  "amenities": ["cctv"],
  "prebookable": true,
  "bookableVehicleTypes": ["car"],
  "floorCount": 1,
  "carCapacity": 10,
  "motorbikeCapacity": 20,
  "publicInfo": {
    "description": "[DEMO] ...",
    "entryInstructions": "[DEMO] ...",
    "rules": ["[DEMO] ..."],
    "photoUrls": ["/images/parkwise/dashboard-parking-entrance.webp"],
    "photosAreIllustrative": true,
    "paymentMethods": ["[DEMO] Tiền mặt"],
    "evChargingPoints": 0
  }
}
```

> Đây là **ví dụ contract**, không phải JSON thực tế đã nhận. Các trường `openingHours`, `pricing`, `motorbike` phải được xác minh lại sau seed, không suy ra từ minh họa.

`PUT /api/parking/owner/lots/{lotId}/public-info` (JWT Owner, check lot ownership): body dạng `publicInfo` bên trên, các chuỗi giới hạn độ dài, ảnh chỉ nhận đường dẫn asset đã được xuất bản. **Không nhận file upload**, không thay đổi approval status.

## 8. Các giới hạn còn lại cần xử lý trước production

- Chưa xác minh code sau patch áp dụng sạch trên `e375f27` hay `origin/develop` mới nhất; chưa chạy .NET build, frontend build, DB migrate, QA test, E2E thực tế.
- Phần **upload ảnh thật** cần quy trình private upload → duyệt → public CDN, không thể khẳng định đã có chỉ từ việc thêm URL metadata. Ảnh demo đang là asset hiện có, được gắn nhãn minh họa.
- **3D bản đồ bãi thật** cần đưa layout geometry thực tế vào 3D component, không sử dụng dữ liệu preview 24 ô; patch hiện hiển thị live floor/zone/slot summaries và giữ 2D/3D preview riêng biệt.
- **Báo giá booking, phí qua đêm và tiền cọc**: `BookingDialog` trong source nền vẫn có deposit mặc định; phải lấy policy từ BE và kiểm thử đầy đủ trước khi cho khách giao dịch thật. Đối chiếu Car-only.
- Chính sách giờ hoạt động, phí, loại xe, EV, quy định trong seed đều ghi `[DEMO]`; chủ bãi phải nhập/duyệt dữ liệu thật trước khi khách hàng dựa vào quyết định.
- Chưa triển khai AI Top 3 trong patch này; gợi ý cần dùng dữ liệu verified và hợp đồng AI hiện có, không tự chế ETA/xác suất/giá.
