# Dữ liệu demo: tài khoản, vai trò, hồ sơ Chủ bãi, nhân viên và bãi mẫu

Một lệnh dựng sẵn mọi thứ để chạy bốn luồng demo Sprint 2: [Seed-DemoData.ps1](../../scripts/dev/Seed-DemoData.ps1). Script đi qua API thật (đăng ký, nộp hồ sơ, tải giấy tờ, Admin duyệt), nên dữ liệu tạo ra đúng quy tắc của hệ thống. Hai việc không có API là xác thực email và nâng quyền SystemAdmin đầu tiên; script làm hai việc đó bằng `psql` trong container PostgreSQL.

**Chỉ dùng trên máy phát triển hoặc máy demo.** Các tài khoản dùng chung một mật khẩu công khai, và bốn bãi mẫu có id cố định bị ghi đè (xem [README-Seed-Shared.md](../../scripts/database/README-Seed-Shared.md)). Không chạy trên cơ sở dữ liệu dùng chung hay có dữ liệu thật.

## Điều kiện

1. Đã có `.env` và khóa JWT (xem [Local-Development.md](Local-Development.md)).
2. Hạ tầng và hai API đang chạy, đã migrate Identity và Parking:

```powershell
docker compose up -d postgres rabbitmq identity-api parking-api
.\scripts\database\Update-DevelopmentDatabases.ps1
```

3. Windows PowerShell 5.1 (có sẵn trong Windows). Script cần `docker` trên `PATH` khi dùng chế độ SQL mặc định.

Không cần Gateway, Notification hay Web: script gọi thẳng cổng của Identity (5001) và Parking (5002). Nhờ vậy giới hạn tốc độ của Gateway (đăng ký 5 lần mỗi phút mỗi IP, đăng nhập 10) không chặn việc tạo sáu tài khoản. Không cần đọc mã OTP vì email được đánh dấu đã xác thực bằng SQL.

## Chạy

Chạy thử, không ghi gì, chỉ in kế hoạch:

```powershell
.\scripts\dev\Seed-DemoData.ps1
```

Chạy thật:

```powershell
.\scripts\dev\Seed-DemoData.ps1 -Apply -ConfirmDemoData I_UNDERSTAND_THIS_CREATES_DEMO_ACCOUNTS
```

Chạy lại bao nhiêu lần cũng được: mỗi bước bỏ qua phần đã có (email đã đăng ký, giấy tờ đã tải, hồ sơ đã ở trạng thái đích). Nếu buổi tập đã từ chối `owner.pending@` hoặc cho `owner.rejected@` nộp lại, chạy lại sẽ đưa hai hồ sơ về đúng trạng thái demo; hồ sơ đã Approved thì không hoàn tác được. Script dừng ngay ở lỗi đầu tiên và in nguyên nhân.

| Tham số | Mặc định | Ý nghĩa |
|---|---|---|
| `-IdentityUrl`, `-ParkingUrl`, `-PaymentUrl` | `http://127.0.0.1:5001`, `:5002`, `:5004` | Địa chỉ các API. Từ chối địa chỉ không phải máy cục bộ nếu không có `-AllowRemoteHost` |
| `-ServiceKey` | `SERVICE_KEY` trong môi trường hoặc `.env`, nếu không thì bỏ trống | Gửi qua header `X-Service-Key` khi `.env` bật khóa dịch vụ |
| `-Password` | `Demo@12345` | Mật khẩu chung của mọi tài khoản demo |
| `-EmailDomain` | `demo.local` | Tên miền email của tài khoản demo |
| `-TaxCodePrefix` | `09000000` | Tám chữ số đầu của ba mã số thuế demo (thêm 01, 02, 03). Đổi nếu báo trùng mã số thuế |
| `-SqlMode` | `Docker` | `Docker`: chạy `psql` qua `docker compose exec postgres`. `Skip`: bạn đã tự chạy SQL ở mục "Chạy SQL bằng tay" |
| `-PostgresContainer` | trống | Tên container để dùng `docker exec` thay cho dịch vụ compose `postgres` |
| `-SkipParkingCatalog` | tắt | Không nạp bốn bãi mẫu (đã có sẵn) |
| `-SkipLotDetails` | tắt | Không đặt giờ mở cửa và giá cho bãi mẫu |
| `-SkipWalletSeed` | tắt | Bỏ qua bước nạp số dư ví cho hai tài khoản khách hàng demo |
| `-WalletSeedAmount` | `200000` | Số dư ví mục tiêu (VND) cho customer và customer2 (qua giả lập VNPay IPN) |
| `-AdminTotpSecret` | `DEMO_ADMIN_TOTP_SECRET` trong môi trường hoặc `.env` | Khóa bí mật Base32 TOTP của Admin để tự động vượt qua thử thách MFA |
| `-AdminTotpCode` | trống | Mã OTP 6 chữ số trực tiếp nếu không dùng secret tính toán tự động |

## Dữ liệu tạo ra

Mật khẩu chung `Demo@12345`. Họ tên không dấu có chủ ý (tệp script chỉ chứa ASCII để Windows PowerShell 5.1 đọc đúng).

| Email | Vai trò | Trạng thái | Dùng cho |
|---|---|---|---|
| `admin@demo.local` | SystemAdmin | Đã xác thực | Duyệt hồ sơ Chủ bãi và bãi, gán nhân viên (hỗ trợ MFA bước 2) |
| `owner@demo.local` | ParkingLotOwner | Hồ sơ Approved (loại hộ kinh doanh, đủ giấy tờ bắt buộc) | Chủ bãi sở hữu cả bốn bãi mẫu; chạy ngay luồng bãi đỗ mà không chờ duyệt |
| `owner.pending@demo.local` | Customer | Hồ sơ Pending (loại cá nhân, đủ giấy tờ) | Admin xem giấy tờ và duyệt ngay trên sân khấu |
| `owner.rejected@demo.local` | Customer | Hồ sơ Rejected có lý do (loại doanh nghiệp, đủ giấy tờ) | Người nộp xem lý do, sửa và nộp lại |
| `staff@demo.local` | Staff | Đã xác thực | Nhân viên mở phiên đỗ ở bãi A và B |
| `customer@demo.local` | Customer | Có hồ sơ cá nhân, một ô tô `51A-123.45`, một xe máy `59X1-234.56`, ví 200.000đ | Khách tìm bãi, xem xe của tôi |
| `customer2@demo.local` | Customer | Khách thứ 2 (Khach Hang Demo Hai), ô tô `51B-999.88`, ví 200.000đ | Kịch bản tranh chấp/đấu cọc slot cuối cùng (Concurrency) |

Giấy tờ là tệp PDF mẫu ghi rõ "DEMO DOCUMENT - NOT A REAL PAPER". Không dùng giấy tờ thật để thử (AGENTS.md mục 5.1). Tệp nằm trong thư mục lưu giấy tờ của container Identity, không nằm trong Git hay cơ sở dữ liệu.

Bốn bãi mẫu lấy từ `Seed-ParkingCatalog.sql` và `Seed-ParkingLayout.sql` (id cố định, không đổi):

| Id | Bãi | Trạng thái | Khu | Nhân viên | Giờ mở cửa | Giá ô tô |
|---|---|---|---|---|---|---|
| `10000000-0000-0000-0000-000000000001` | Bãi đỗ Trung tâm A | Approved | CAR-A, 2 chỗ (A-01, A-02) | `staff@` | 24 giờ | 15.000 mỗi 30 phút |
| `10000000-0000-0000-0000-000000000002` | Bãi đỗ Ngoài trời B | Approved | MOTO-B, sức chứa 20 xe máy | `staff@` | 24 giờ | 12.000 mỗi 30 phút |
| `10000000-0000-0000-0000-000000000003` | Bãi đỗ Tầng hầm C | Approved | CAR-C, 1 chỗ (C-01) | không | 07:00 đến 21:00 | 20.000 mỗi 30 phút |
| `10000000-0000-0000-0000-000000000004` | Bãi chờ duyệt | Pending | chưa có | không | chưa đặt | chưa đặt |

Cả ba bãi Approved có giá xe máy 5.000 mỗi lượt (`PerEntry`) và phí qua đêm. Chủ bãi `owner@` được gán Owner cho cả bốn bãi, nên bãi Pending hiện trong hàng chờ của Admin và trong danh sách bãi của chủ.

## Gắn với bốn luồng demo

- **DEMO-01, hồ sơ Chủ bãi:** đăng nhập `admin@`, mở hàng chờ hồ sơ, xem giấy tờ của `owner.pending@` (route xem giấy tờ của Admin nằm ở PR #121), duyệt; `owner.pending@` đăng nhập lại để nhận quyền Chủ bãi. Hoặc đăng nhập `owner.rejected@`, đọc lý do, tải lại giấy tờ, nộp lại. Cảnh đăng ký mới bằng giao diện vẫn làm trực tiếp được; mã OTP nằm trong log của `notification-api`.
- **DEMO-02, bãi đỗ:** đăng nhập `owner@`, xem hoặc sửa giờ mở cửa, giá, layout của bãi A, B, C; bãi Pending để Admin duyệt hoặc từ chối trên sân khấu. Danh sách bãi của chủ và của Admin nằm ở PR #118.
- **DEMO-03a, tìm bãi và nạp ví:** đăng nhập `customer@`, tìm ba bãi Approved (có giờ và giá). Nạp ví cần VNPay sandbox cấu hình và đường cho IPN (xem `.env.example`); script không tạo số dư, vì ví chứa tiền thật.
- **DEMO-04a, nhân viên mở phiên:** đăng nhập `staff@`, mở phiên ô tô ở bãi A với biển `51A-123.45`, xe máy ở bãi B với biển `59X1-234.56`. Route nhân viên xem bãi được gán chưa có, giao diện nhân viên đang dùng id bãi cố định. Khách chưa thấy mã QR của mình vì tra biển số sang Identity chưa nối.

## Chạy SQL bằng tay

Khi không dùng Docker cho PostgreSQL, tự chạy hai câu sau trên cơ sở dữ liệu `parking_system` của máy demo, rồi chạy script với `-SqlMode Skip`:

```sql
UPDATE identity.accounts SET status = 'EmailVerified'
 WHERE email IN ('admin@demo.local','owner@demo.local','owner.pending@demo.local','owner.rejected@demo.local','staff@demo.local','customer@demo.local','customer2@demo.local');
UPDATE identity.accounts SET role = 'SystemAdmin' WHERE email = 'admin@demo.local';
```

Hai câu này phải chạy **sau** lượt đăng ký (script đăng ký trước, rồi mới dừng ở bước đăng nhập nếu thiếu SQL). Cách đơn giản: chạy script một lần (nó dừng ở đăng nhập với gợi ý), chạy hai câu SQL, rồi chạy lại với `-SqlMode Skip`. Bốn bãi mẫu phải có sẵn: nạp bằng `Seed-ParkingCatalog.sql` và `Seed-ParkingLayout.sql` như trong README-Seed-Shared.md.

## Sự cố thường gặp

| Triệu chứng | Nguyên nhân và cách xử lý |
|---|---|
| `Cannot reach http://127.0.0.1:5001` | `identity-api` chưa chạy. `docker compose up -d identity-api parking-api` |
| `HTTP 401` ngay ở bước kiểm tra dịch vụ | `.env` có `SERVICE_KEY` nhưng script không đọc được. Truyền `-ServiceKey` |
| `Login ... HTTP 403`, gợi ý "email is not verified" | Bước SQL chưa chạy. Dùng `-SqlMode Docker` (cần container `postgres` đang chạy) hoặc làm theo mục "Chạy SQL bằng tay" |
| `psql failed` | Container PostgreSQL chưa chạy hoặc chưa migrate Identity và Parking |
| Báo trùng mã số thuế khi nộp hồ sơ | Cơ sở dữ liệu đã có mã `0900000001` đến `03`. Truyền `-TaxCodePrefix` khác |
| `Lot ... does not exist in Parking` | Bốn bãi mẫu chưa nạp: chạy không kèm `-SkipParkingCatalog`, và đã migrate Parking |
| Cảnh báo `WARN` ở bước giờ mở cửa hoặc giá | Bước làm đẹp dữ liệu, không chặn demo. Đọc mã lỗi trong dòng cảnh báo, đặt lại bằng giao diện chủ bãi |

## Giới hạn

- Không tạo số dư ví (ví chứa tiền thật), không tạo phiên đỗ, đặt chỗ hay thanh toán.
- Không đưa mọi thứ về trạng thái ban đầu: mật khẩu đã đổi, hồ sơ đã Approved, bãi hay tài khoản do người dùng tự tạo đều không bị đụng tới. Muốn làm lại từ đầu thì tạo lại cơ sở dữ liệu dev (`docker compose down -v` xóa toàn bộ dữ liệu dev, chỉ làm khi chắc chắn) rồi migrate và chạy lại.
- Mật khẩu công khai và việc tự đánh dấu email đã xác thực chỉ chấp nhận được trên máy phát triển. Script từ chối địa chỉ không phải máy cục bộ, và không ghi gì nếu thiếu `-Apply` cùng `-ConfirmDemoData`.
