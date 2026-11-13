# Contract #5 (ReservationWalletContract) Bugfixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Triệt tiêu hoàn toàn 6 lỗi logic và tài chính (P3–P8) trong hợp đồng gRPC số 5 giữa Payment và Booking Saga; bổ sung các chốt chặn thứ tự Idempotency (M1, M2), chống IDOR (S1), mapping lỗi đầy đủ (S2), giải phóng lock toàn diện (S3), và kiểm chứng bằng cả Unit Test lẫn Integration Test trên PostgreSQL thật (M3).

**Architecture:** Tuân thủ Clean Architecture và DDD của Payment service: Domain Errors và Enums thuần ở Core/Domain; Repository port ở Application; Query & EF Core implementation ở Persistence; Handler xử lý Idempotency hai pha chuẩn (ReleasePendingAsync khi lỗi kinh doanh phục hồi được); gRPC service tại WebAPI làm Facade mapping tường minh.

**Tech Stack:** ASP.NET Core (.NET 10), gRPC Protos (`payment_reservation.proto`), MediatR, EF Core 9 / PostgreSQL, xUnit / Microsoft Testing Platform / Testcontainers.

---

## File Structure & Responsibilities

| File Path | Trách nhiệm |
| :--- | :--- |
| `src/Services/Payment/Payment.Domain/Enums/ReservationPrepaymentPurpose.cs` | Enum mục đích trừ cọc (Deposit = 1, Scheduled = 2) |
| `src/Services/Payment/Payment.Application/Common/Constants/ReservationDepositErrors.cs` | Thêm mã lỗi: `WalletNotActive`, `AlreadyRefunded`, `ReservationNotDebited`, `RefundAmountExceedsDeposit` |
| `src/Services/Payment/Payment.Application/Common/Interfaces/IWalletRepository.cs` | Bổ sung phương thức port tra cứu bút toán cọc/hoàn: `GetReservationLedgerEntriesAsync` |
| `src/Services/Payment/Payment.Persistence/Repositories/WalletRepository.cs` | Thực thi truy vấn ledger entries theo `reservationId` và `userId` (Anti-IDOR) |
| `src/Services/Payment/Payment.Application/Features/Wallets/Commands/DebitReservationDeposit/DebitReservationDepositCommand.cs` | Mở rộng Command nhận `Purpose` |
| `src/Services/Payment/Payment.Application/Features/Wallets/Commands/DebitReservationDeposit/DebitReservationDepositCommandHandler.cs` | **Thứ tự M1:** Kiểm tra Refund trước Replay Success $\rightarrow$ Dọn bản ghi cũ (P4) $\rightarrow$ Kiểm tra ví & Balance $\rightarrow$ Ghi sổ với Reason phân biệt (P7). `ReleasePendingAsync` cho mọi lỗi có thể phục hồi (S3). |
| `src/Services/Payment/Payment.Application/Features/Wallets/Commands/RefundReservationDeposit/RefundReservationDepositCommandHandler.cs` | **Thứ tự M2:** Replay Success trước tiên $\rightarrow$ Kiểm tra ví (không tạo ví ảo) $\rightarrow$ Kiểm tra số đã trừ & số đã hoàn $\rightarrow$ Ghi sổ. `ReleasePendingAsync` khi thất bại. |
| `src/Services/Payment/Payment.WebAPI/Grpc/ReservationWalletGrpcService.cs` | Mapping chuẩn `DebitOutcome` (P6), Anti-IDOR bắt buộc GUID hợp lệ (S1), Mapping lỗi `RefundReservation` (S2). |
| `tests/Payment/Payment.UnitTests/...` | Bộ Unit Tests kiểm chứng logic, mapping và edge cases |
| `tests/Payment/Payment.IntegrationTests/...` | Bộ Integration Tests (Testcontainers / PostgreSQL thật) kiểm chứng Unique Index, retry nạp tiền (P4), và replay/refund idempotency (M3) |

---

### Task 1: Domain Errors & Purpose Enum Extension (P6, P7 prep)
- [ ] **1.1. Tạo Domain Enum `ReservationPrepaymentPurpose.cs`**
  - Path: `src/Services/Payment/Payment.Domain/Enums/ReservationPrepaymentPurpose.cs`
  - Giá trị: `Deposit = 1`, `Scheduled = 2`.
- [ ] **1.2. Thêm mã lỗi chuẩn vào `ReservationDepositErrors.cs`**
  - Path: `src/Services/Payment/Payment.Application/Common/Constants/ReservationDepositErrors.cs`
  - Thêm:
    - `WalletNotActive` (`"payment.wallet_not_active"`, `"Wallet is not active."`)
    - `AlreadyRefunded` (`"payment.reservation_already_refunded"`, `"Reservation was already refunded."`)
    - `ReservationNotDebited` (`"payment.reservation_not_debited"`, `"No deposit record found for this reservation."`)
    - `RefundAmountExceedsDeposit` (`"payment.refund_amount_exceeds_deposit"`, `"Refund amount exceeds debited deposit amount."`)
- [ ] **1.3. Build kiểm tra**
  - Lệnh: `dotnet build src\Services\Payment\Payment.Application\Payment.Application.csproj --no-restore -m:1 /p:UseSharedCompilation=false`

---

### Task 2: Repository Port & Persistence Implementation (P3, P5, P8 prep)
- [ ] **2.1. Thêm phương thức vào interface `IWalletRepository.cs`**
  - Path: `src/Services/Payment/Payment.Application/Common/Interfaces/IWalletRepository.cs`
  - Thêm: `Task<IReadOnlyList<WalletLedgerEntry>> GetReservationLedgerEntriesAsync(Guid reservationId, Guid? userId = null, CancellationToken cancellationToken = default);`
- [ ] **2.2. Triển khai truy vấn trong `WalletRepository.cs`**
  - Path: `src/Services/Payment/Payment.Persistence/Repositories/WalletRepository.cs`
  - Truy vấn `_dbContext.WalletLedgerEntries` lọc theo `ReferenceType.Reservation` và `ReferenceId == reservationId.ToString()`. Nếu có `userId`, kết hợp `_dbContext.Wallets` lọc `w.UserId == userId.Value`.
- [ ] **2.3. Build kiểm tra**
  - Lệnh: `dotnet build src\Services\Payment\Payment.Persistence\Payment.Persistence.csproj --no-restore -m:1 /p:UseSharedCompilation=false`

---

### Task 3: Vá `DebitReservationDepositCommandHandler` (P4, P5, P7, M1, S3)
- [ ] **3.1. Cập nhật `DebitReservationDepositCommand.cs`**
  - Path: `src/Services/Payment/Payment.Application/Features/Wallets/Commands/DebitReservationDeposit/DebitReservationDepositCommand.cs`
  - Bổ sung property: `ReservationPrepaymentPurpose Purpose = ReservationPrepaymentPurpose.Deposit`.
- [ ] **3.2. Cập nhật Handler `DebitReservationDepositCommandHandler.cs` theo thứ tự M1 & S3**
  - **Bước A (Chốt chặn Terminal State - M1):** Tra cứu `walletRepository.GetReservationLedgerEntriesAsync(request.ReservationId)`. Nếu ĐÃ CÓ bút toán `LedgerEntryType.Refund`, BẬT CỜ DỪNG NGAY và trả về `ReservationDepositErrors.AlreadyRefunded` (CẤM replay `Success` nếu đã bị hoàn tiền).
  - **Bước B (Idempotency Replay & Giải phóng cũ):**
    - Nếu `existingRecord.Status == "Success"`: Replay kết quả cũ.
    - Nếu `existingRecord.Status == "Pending"` và chưa hết hạn: trả `ReservationDepositErrors.RequestInProgress`.
    - Nếu `existingRecord.Status == "Failed"` hoặc hết hạn lock: gọi `await idempotencyStore.ReleasePendingAsync(existingRecord.Id, CancellationToken.None);` (P4).
  - **Bước C (Tạo Pending Lock):** Gọi `idempotencyStore.TryCreatePendingAsync(...)`.
  - **Bước D (Kiểm tra ví & số dư - S3):**
    - Nếu ví null $\rightarrow$ gọi `ReleasePendingAsync(pendingRecord.Id)` và trả `WalletNotFound`.
    - Nếu `wallet.Status != WalletStatus.Active` $\rightarrow$ gọi `ReleasePendingAsync(pendingRecord.Id)` và trả `WalletNotActive`.
    - Nếu `wallet.Balance < request.Amount` $\rightarrow$ gọi `ReleasePendingAsync(pendingRecord.Id)` và trả `InsufficientBalance` (P4).
  - **Bước E (Ghi sổ cái - P7):**
    - Phân biệt Reason: Nếu `request.Purpose == ReservationPrepaymentPurpose.Scheduled` thì ghi `"Scheduled reservation prepayment for {request.ReservationId}"`, ngược lại `"Reservation deposit for {request.ReservationId}"`.
    - Đánh dấu Idempotency `Success` và lưu DbContext.
- [ ] **3.3. Build kiểm tra**
  - Lệnh: `dotnet build src\Services\Payment\Payment.Application\Payment.Application.csproj --no-restore -m:1 /p:UseSharedCompilation=false`

---

### Task 4: Vá `RefundReservationDepositCommandHandler` (P3, M2)
- [ ] **4.1. Cập nhật Handler `RefundReservationDepositCommandHandler.cs` theo thứ tự M2**
  - **Bước A (Idempotency Replay - M2):**
    - Nếu `existingRecord.Status == "Success"`: Replay kết quả cũ ngay lập tức (bảo đảm worker retry nhận kết quả thành công, không bị chặn bởi bước kiểm tra hạn mức còn lại = 0).
    - Nếu `existingRecord.Status == "Pending"` và chưa hết hạn: trả `ReservationDepositErrors.RequestInProgress`.
    - Nếu `existingRecord.Status == "Failed"` hoặc hết hạn lock: gọi `ReleasePendingAsync(existingRecord.Id)`.
  - **Bước B (Tạo Pending Lock):** Gọi `idempotencyStore.TryCreatePendingAsync(...)`.
  - **Bước C (Kiểm tra ví - P3):**
    - Gọi `walletRepository.GetByUserIdAsync(request.UserId)`. TUYỆT ĐỐI KHÔNG dùng `GetOrCreateByUserIdAsync`.
    - Nếu ví null $\rightarrow$ giải phóng lock và trả `WalletNotFound`.
    - Nếu `wallet.Status != WalletStatus.Active` $\rightarrow$ giải phóng lock và trả `WalletNotActive`.
  - **Bước D (Kiểm tra lịch sử trừ cọc & hạn mức hoàn - P3):**
    - Lấy entries từ `walletRepository.GetReservationLedgerEntriesAsync(request.ReservationId, request.UserId)`.
    - `totalDebited` = tổng `Math.Abs(Amount)` các bút toán `ReservationDeposit`.
    - `totalRefunded` = tổng `Amount` các bút toán `Refund`.
    - Nếu `totalDebited == 0` $\rightarrow$ giải phóng lock và trả `ReservationDepositErrors.ReservationNotDebited`.
    - Nếu `request.Amount > (totalDebited - totalRefunded)` $\rightarrow$ giải phóng lock và trả `ReservationDepositErrors.RefundAmountExceedsDeposit`.
  - **Bước E (Ghi sổ cái & Hoàn tất):**
    - Thêm bút toán `LedgerEntryType.Refund`.
    - Đánh dấu Idempotency `Success` và lưu DbContext.
- [ ] **4.2. Build kiểm tra**
  - Lệnh: `dotnet build src\Services\Payment\Payment.Application\Payment.Application.csproj --no-restore -m:1 /p:UseSharedCompilation=false`

---

### Task 5: Vá Facade gRPC `ReservationWalletGrpcService.cs` (P6, P8, S1, S2)
- [ ] **5.1. Cập nhật `DebitReservation`**
  - Map `request.Purpose` từ gRPC proto sang `ReservationPrepaymentPurpose`.
  - Map mã lỗi tường minh sang `DebitOutcome`:
    - `InsufficientBalance` / `WalletNotFound` $\rightarrow$ `DebitOutcome.InsufficientBalance` (Theo proto: "no wallet counts as balance 0")
    - `WalletNotActive` $\rightarrow$ `DebitOutcome.WalletNotActive`
    - `AlreadyRefunded` $\rightarrow$ `DebitOutcome.AlreadyRefunded`
    - `IdempotencyKeyReused` $\rightarrow$ `DebitOutcome.AmountMismatch`
    - Còn lại $\rightarrow$ `DebitOutcome.Unspecified`
- [ ] **5.2. Cập nhật `GetReservationDebit` (P8 Anti-IDOR & S1)**
  - Bắt buộc kiểm tra: Nếu `!Guid.TryParse(request.AccountId, out var userId)` hoặc `!Guid.TryParse(request.ReservationId, out var reservationId)` $\rightarrow$ trả ngay `State = DebitState.Unspecified, AmountVnd = 0`, KHÔNG thực hiện tra cứu.
  - Tra cứu qua `walletRepository.GetReservationLedgerEntriesAsync(reservationId, userId)`.
  - Trả về đúng `DebitState`: `Refunded` (khi có Refund), `Debited` (khi có Deposit), hoặc `NotDebited`.
- [ ] **5.3. Cập nhật `RefundReservation` (S2)**
  - Nếu thất bại:
    - Nếu lỗi là `ReservationDepositErrors.ReservationNotDebited` $\rightarrow$ trả `State = DebitState.NotDebited`.
    - Nếu lỗi là `ReservationDepositErrors.AlreadyRefunded` $\rightarrow$ trả `State = DebitState.Refunded`.
    - Còn lại $\rightarrow$ trả `State = DebitState.Unspecified`.
- [ ] **5.4. Build kiểm tra**
  - Lệnh: `dotnet build src\Services\Payment\Payment.WebAPI\Payment.WebAPI.csproj --no-restore -m:1 /p:UseSharedCompilation=false`

---

### Task 6: Unit Tests Suite (TDD & Verification)
- [ ] **6.1. Cập nhật/Thêm test cases cho `DebitReservationDepositCommandHandlerTests.cs`**
  - Test case: Thiếu tiền giải phóng pending idempotency và cho phép gọi lại thành công sau khi nạp đủ tiền (P4, S3).
  - Test case: Đặt chỗ đã có bút toán Refund bị từ chối với `AlreadyRefunded` kể cả khi có bản ghi Success cũ (M1, P5).
  - Test case: Ghi sổ cái đúng reason khi purpose là `Scheduled` vs `Deposit` (P7).
- [ ] **6.2. Cập nhật/Thêm test cases cho `RefundReservationDepositCommandHandlerTests.cs`**
  - Test case: Không tự tạo ví mới khi ví chưa tồn tại (P3).
  - Test case: Từ chối hoàn tiền khi chưa từng bị trừ cọc (P3).
  - Test case: Từ chối hoàn tiền khi số tiền hoàn vượt quá số tiền đã cọc (P3).
  - Test case: Gọi lại lần 2 cùng key và hash $\rightarrow$ Replay `Success` thành công, không bị lỗi vượt hạn mức (M2).
- [ ] **6.3. Cập nhật/Thêm test cases cho `ReservationWalletGrpcServiceTests.cs`**
  - Test case: Map đầy đủ các `DebitOutcome` (AmountMismatch, WalletNotActive, AlreadyRefunded) (P6).
  - Test case: `GetReservationDebit` từ chối tra cứu khi `AccountId` không hợp lệ (S1, P8).
  - Test case: `RefundReservation` map đúng `NotDebited` khi chưa trừ cọc (S2).
- [ ] **6.4. Chạy toàn bộ Unit Tests**
  - Lệnh build: `dotnet build tests\Payment\Payment.UnitTests\Payment.UnitTests.csproj --no-restore -m:1 /p:UseSharedCompilation=false`
  - Lệnh chạy: `& "tests\Payment\Payment.UnitTests\bin\Debug\net10.0\Payment.UnitTests.exe"`

---

### Task 7: Integration Tests trên PostgreSQL thật (M3)
- [ ] **7.1. Tạo file test `ReservationWalletContractIntegrationTests.cs`**
  - Path: `tests/Payment/Payment.IntegrationTests/ReservationWalletContractIntegrationTests.cs`
  - Kế thừa `PostgreSqlContainerFixture` (chạy trên PostgreSQL thật qua Docker).
  - Test 1: Khách thiếu tiền $\rightarrow$ gọi Debit bị từ chối $\rightarrow$ Nạp tiền vào ví $\rightarrow$ Gọi lại Debit cùng `ReservationId` $\rightarrow$ Thành công (chứng minh Postgres Unique Index không gây deadlock P4).
  - Test 2: Trừ cọc thành công $\rightarrow$ Hoàn cọc thành công $\rightarrow$ Gọi lại Debit $\rightarrow$ Nhận `AlreadyRefunded` (chứng minh thứ tự M1).
  - Test 3: Hoàn cọc lần 1 thành công $\rightarrow$ Gọi lại Refund lần 2 với cùng key/hash $\rightarrow$ Replay thành công, số dư ví chỉ cộng đúng 1 lần (chứng minh thứ tự M2).
- [ ] **7.2. Chạy Integration Tests**
  - Build test: `dotnet build tests\Payment\Payment.IntegrationTests\Payment.IntegrationTests.csproj --no-restore -m:1 /p:UseSharedCompilation=false`
  - Chạy test: `& "tests\Payment\Payment.IntegrationTests\bin\Debug\net10.0\Payment.IntegrationTests.exe"`
