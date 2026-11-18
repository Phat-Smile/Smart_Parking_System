# US-OWNER-010: Owner Revenue Summary Analytics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cung cấp endpoint Minimal API `GET /api/payment/owner/revenue/summary` trả về tổng doanh thu thực tế cho Chủ bãi xe theo hôm nay, tuần này, tháng này, tất cả thời gian; phân tách minh bạch giữa tiền mặt (cổng thu) vs online (VNPay/Ví); bảo đảm tuyệt đối tính toàn vẹn tài chính, chống tính đúp phụ phí overstay, chống rò rỉ IDOR (NFR-07) và xử lý chuẩn xác múi giờ Việt Nam UTC+7.

**Architecture:** Clean Architecture & DDD: DTOs & Constants tại Application; Repository Port tại Application (`IParkingFeePaymentRepository.GetRevenueMetricsAsync`); Database aggregation tối ưu 1 round-trip tại Persistence (`ParkingFeePaymentRepository`); Handler tích hợp Anti-IDOR qua `IParkingLotMembershipGateway` (gRPC sang Parking); Minimal API Endpoint tại WebAPI với policy `PaymentAuthorization.OwnerRevenueReportPolicy`.

**Tech Stack:** ASP.NET Core (.NET 10), Minimal APIs, MediatR, EF Core 9 / PostgreSQL, xUnit.v3 / Microsoft.Testing.Platform.

---

## File Structure & Responsibilities

| File Path | Trách nhiệm |
| :--- | :--- |
| `src/Services/Payment/Payment.Application/Features/OwnerRevenue/Queries/GetOwnerRevenueSummary/OwnerRevenueSummaryResponse.cs` | DTO kết quả doanh thu (`OwnerRevenueSummaryResponse`, `RevenuePeriodMetrics`) |
| `src/Services/Payment/Payment.Application/Common/Constants/OwnerRevenueErrors.cs` | Catalog mã lỗi nghiệp vụ: `ForbiddenRole`, `ForbiddenLotAccess`, `ParkingLotIdRequired`, `InvalidTimeZoneOffset`, `ParkingServiceUnavailable` |
| `src/Services/Payment/Payment.Application/Common/Interfaces/IParkingFeePaymentRepository.cs` | Bổ sung phương thức port aggregation: `GetRevenueMetricsAsync` |
| `src/Services/Payment/Payment.Persistence/Repositories/ParkingFeePaymentRepository.cs` | Thực thi truy vấn 1 round-trip trên PostgreSQL (bảng `fee_payments`) với `AsNoTracking()` |
| `src/Services/Payment/Payment.Application/Features/OwnerRevenue/Queries/GetOwnerRevenueSummary/GetOwnerRevenueSummaryQuery.cs` | MediatR Query nhận `ParkingLotId`, `TimeZoneOffsetMinutes`, `CallerAccountId`, `CallerRole` |
| `src/Services/Payment/Payment.Application/Features/OwnerRevenue/Queries/GetOwnerRevenueSummary/GetOwnerRevenueSummaryQueryHandler.cs` | Xử lý nghiệp vụ: Chặn IDOR (NFR-07), kiểm tra hợp lệ múi giờ, quy đổi mốc UTC, gọi Repository và map response |
| `src/Services/Payment/Payment.WebAPI/Security/PaymentAuthorization.cs` | Thêm policy `OwnerRevenueReportPolicy` cho phép `ParkingLotOwner`, `LotOwner`, `SystemAdmin` |
| `src/Services/Payment/Payment.WebAPI/Endpoints/ParkingFeeEndpoints.cs` | Đăng ký Minimal API `GET /api/payment/owner/revenue/summary` |
| `tests/Payment/Payment.UnitTests/Queries/GetOwnerRevenueSummaryQueryHandlerTests.cs` | Bộ Unit Tests toàn diện kiểm chứng 11 ca biên (Authentication, IDOR, Timezone, Zero revenue, Overstay deduplication) |

---

### Task 1: DTOs & Error Constants
**Files:**
- Create: `src/Services/Payment/Payment.Application/Features/OwnerRevenue/Queries/GetOwnerRevenueSummary/OwnerRevenueSummaryResponse.cs`
- Create: `src/Services/Payment/Payment.Application/Common/Constants/OwnerRevenueErrors.cs`

- [ ] **Step 1.1: Tạo file DTO `OwnerRevenueSummaryResponse.cs`**
  ```csharp
  namespace ParkingSystem.Payment.Application.Features.OwnerRevenue.Queries.GetOwnerRevenueSummary;

  public sealed record RevenuePeriodMetrics(
      long TotalRevenue,
      long CashRevenue,
      long OnlineRevenue,
      long VnpayRevenue,
      long WalletRevenue,
      long ForfeitedDepositRevenue,
      int TransactionCount,
      int CashTransactionCount,
      int OnlineTransactionCount);

  public sealed record OwnerRevenueSummaryResponse(
      Guid? ParkingLotId,
      DateTime GeneratedAtUtc,
      string Currency,
      RevenuePeriodMetrics Today,
      RevenuePeriodMetrics ThisWeek,
      RevenuePeriodMetrics ThisMonth,
      RevenuePeriodMetrics AllTime);
  ```

- [ ] **Step 1.2: Tạo file `OwnerRevenueErrors.cs`**
  ```csharp
  using ParkingSystem.BuildingBlocks.Results;

  namespace ParkingSystem.Payment.Application.Common.Constants;

  public static class OwnerRevenueErrors
  {
      public static readonly Error ForbiddenRole = new("payment.forbidden_role", "Only parking lot owners or administrators can view revenue reports.");
      public static readonly Error ForbiddenLotAccess = new("payment.forbidden_lot_access", "You do not have permission to view revenue for this parking lot.");
      public static readonly Error ParkingLotIdRequired = new("payment.parking_lot_id_required", "Parking lot id is required for lot owners.");
      public static readonly Error InvalidParkingLotId = new("payment.invalid_parking_lot_id", "Parking lot id is invalid.");
      public static readonly Error InvalidTimeZoneOffset = new("payment.invalid_timezone_offset", "Timezone offset must be between -720 and 840 minutes.");
      public static readonly Error ParkingServiceUnavailable = new("payment.parking_service_unavailable", "Parking service is temporarily unavailable. Please try again.");
  }
  ```

- [ ] **Step 1.3: Build kiểm tra**
  Run: `dotnet build src\Services\Payment\Payment.Application\Payment.Application.csproj --no-restore -m:1 /p:UseSharedCompilation=false`
  Expected: Build succeeded.

---

### Task 2: Repository Port & Persistence Implementation
**Files:**
- Modify: `src/Services/Payment/Payment.Application/Common/Interfaces/IParkingFeePaymentRepository.cs`
- Modify: `src/Services/Payment/Payment.Persistence/Repositories/ParkingFeePaymentRepository.cs`

- [ ] **Step 2.1: Mở rộng `IParkingFeePaymentRepository.cs`**
  Thêm phương thức:
  ```csharp
  Task<OwnerRevenueMetricsResult> GetRevenueMetricsAsync(
      IReadOnlyList<Guid> lotIds,
      DateTime todayStartUtc,
      DateTime weekStartUtc,
      DateTime monthStartUtc,
      CancellationToken ct);
  ```

- [ ] **Step 2.2: Định nghĩa `OwnerRevenueMetricsResult` tại `Application/Common/Interfaces/`**
  Record chứa các aggregate buckets cho Today, Week, Month, AllTime.

- [ ] **Step 2.3: Triển khai trong `ParkingFeePaymentRepository.cs`**
  Dùng EF Core với `AsNoTracking()` lọc:
  - `Status == PaymentStatus.Succeeded`
  - `lotIds.Count == 0 || lotIds.Contains(p.ParkingLotId)`
  Gom nhóm và tính toán tổng số tiền và số lượng giao dịch.

- [ ] **Step 2.4: Build kiểm tra**
  Run: `dotnet build src\Services\Payment\Payment.Persistence\Payment.Persistence.csproj --no-restore -m:1 /p:UseSharedCompilation=false`
  Expected: Build succeeded.

---

### Task 3: Application Query & Handler (Anti-IDOR & Timezone Engine)
**Files:**
- Create: `src/Services/Payment/Payment.Application/Features/OwnerRevenue/Queries/GetOwnerRevenueSummary/GetOwnerRevenueSummaryQuery.cs`
- Create: `src/Services/Payment/Payment.Application/Features/OwnerRevenue/Queries/GetOwnerRevenueSummary/GetOwnerRevenueSummaryQueryHandler.cs`

- [ ] **Step 3.1: Viết Query `GetOwnerRevenueSummaryQuery.cs`**
  ```csharp
  using MediatR;
  using ParkingSystem.BuildingBlocks.Results;

  namespace ParkingSystem.Payment.Application.Features.OwnerRevenue.Queries.GetOwnerRevenueSummary;

  public sealed record GetOwnerRevenueSummaryQuery(
      Guid? ParkingLotId,
      int TimeZoneOffsetMinutes,
      Guid? CallerAccountId,
      string? CallerRole) : IRequest<Result<OwnerRevenueSummaryResponse>>;
  ```

- [ ] **Step 3.2: Viết Handler `GetOwnerRevenueSummaryQueryHandler.cs`**
  Thứ tự kiểm tra nghiêm ngặt:
  1. Kiểm tra Caller Role: `SystemAdmin`, `ParkingLotOwner`, `LotOwner`. Khác $\rightarrow$ `ForbiddenRole` (403).
  2. Kiểm tra `TimeZoneOffsetMinutes`: Trong khoảng `[-720, 840]`. Khác $\rightarrow$ `InvalidTimeZoneOffset` (400).
  3. Kiểm tra Anti-IDOR (NFR-07):
     - Nếu Caller là Owner: Bắt buộc `ParkingLotId.HasValue`. Nếu không có $\rightarrow$ `ParkingLotIdRequired` (400).
     - Gọi `IParkingLotMembershipGateway.GetMembershipAsync(lotId, accountId)`.
     - Nếu gateway lỗi $\rightarrow$ `ParkingServiceUnavailable` (503).
     - Nếu không phải Owner của bãi $\rightarrow$ `ForbiddenLotAccess` (403).
     - TargetLots = `[lotId]`.
     - Nếu Caller là Admin: TargetLots = `ParkingLotId.HasValue ? [ParkingLotId.Value] : []`.
  4. Tính toán mốc UTC:
     - `var offset = TimeSpan.FromMinutes(request.TimeZoneOffsetMinutes);`
     - `var nowLocal = DateTime.UtcNow + offset;`
     - `var todayStartLocal = nowLocal.Date;`
     - `var todayStartUtc = todayStartLocal - offset;`
     - Tính `weekStartUtc` (thứ Hai) và `monthStartUtc` (ngày 1).
  5. Gọi Repository `_paymentRepository.GetRevenueMetricsAsync(...)`.
  6. Map kết quả sang `OwnerRevenueSummaryResponse`.

- [ ] **Step 3.3: Build kiểm tra**
  Run: `dotnet build src\Services\Payment\Payment.Application\Payment.Application.csproj --no-restore -m:1 /p:UseSharedCompilation=false`
  Expected: Build succeeded.

---

### Task 4: WebAPI Minimal API Endpoint & Security Policy
**Files:**
- Modify: `src/Services/Payment/Payment.WebAPI/Security/PaymentAuthorization.cs`
- Modify: `src/Services/Payment/Payment.WebAPI/Endpoints/ParkingFeeEndpoints.cs`

- [ ] **Step 4.1: Thêm Policy `OwnerRevenueReportPolicy` vào `PaymentAuthorization.cs`**
  ```csharp
  public const string OwnerRevenueReportPolicy = "payment-owner-revenue-report";
  ```
  Cho phép `ParkingLotOwner`, `LotOwner`, `SystemAdmin`.

- [ ] **Step 4.2: Đăng ký Endpoint trong `ParkingFeeEndpoints.cs`**
  ```csharp
  app.MapGet("/api/payment/owner/revenue/summary", async (
          [FromQuery] Guid? parkingLotId,
          [FromQuery] int? timeZoneOffsetMinutes,
          HttpContext httpContext,
          ISender sender,
          CancellationToken ct) =>
      {
          var callerAccountId = Guid.TryParse(httpContext.User.FindFirst("sub")?.Value, out var id) ? id : (Guid?)null;
          var callerRole = httpContext.User.FindFirst("role")?.Value ?? string.Empty;

          var query = new GetOwnerRevenueSummaryQuery(
              parkingLotId,
              timeZoneOffsetMinutes ?? 420,
              callerAccountId,
              callerRole);

          var result = await sender.Send(query, ct);
          return result.IsSuccess
              ? Results.Ok(ApiResponse<OwnerRevenueSummaryResponse>.Success(result.Value, "Get owner revenue summary successfully"))
              : MapError(httpContext, result.Error!);
      })
      .RequireAuthorization(PaymentAuthorization.OwnerRevenueReportPolicy);
  ```

- [ ] **Step 4.3: Build WebAPI**
  Run: `dotnet build src\Services\Payment\Payment.WebAPI\Payment.WebAPI.csproj --no-restore -m:1 /p:UseSharedCompilation=false`
  Expected: Build succeeded.

---

### Task 5: Kỷ luật Unit Test Toàn diện & Bịt mọi lỗ hổng (TDD)
**Files:**
- Create: `tests/Payment/Payment.UnitTests/Queries/GetOwnerRevenueSummaryQueryHandlerTests.cs`

- [ ] **Step 5.1: Viết bộ 11 Unit Tests kiểm chứng toàn diện:**
  1. `HandleAsync_WhenUnauthenticated_ReturnsUnauthorized401`
  2. `HandleAsync_WhenCallerIsCustomerOrStaff_ReturnsForbiddenRole403`
  3. `HandleAsync_WhenOwnerCallsWithoutLotId_ReturnsParkingLotIdRequired400`
  4. `HandleAsync_WhenTimezoneOffsetOutOfRange_ReturnsInvalidTimezone400`
  5. `HandleAsync_WhenOwnerDoesNotOwnLot_ReturnsForbiddenLotAccess403` (Anti-IDOR)
  6. `HandleAsync_WhenParkingServiceUnavailable_ReturnsServiceUnavailable503`
  7. `HandleAsync_WhenAdminQueriesWithoutLotId_CalculatesSystemWideRevenue200`
  8. `HandleAsync_WhenValidOwner_ReturnsAccurateMetricsByTodayWeekMonthAllTime200`
  9. `HandleAsync_WhenLotHasZeroPaidSessions_ReturnsAllZeroMetrics200`
  10. `HandleAsync_WhenOverstayPaid_CountsOnlySucceededWithoutDoubleCounting200`
  11. `HandleAsync_TimezoneBoundary_CorrectlyBucketsMidnightTransactionsAtUtcPlus7`

- [ ] **Step 5.2: Chạy bộ test thực tế bằng PowerShell**
  Run: `dotnet build tests\Payment\Payment.UnitTests\Payment.UnitTests.csproj --no-restore -m:1 /p:UseSharedCompilation=false`
  Run: `& "tests\Payment\Payment.UnitTests\bin\Debug\net10.0\Payment.UnitTests.exe"`
  Expected: Tất cả các test đều PASS 100%.
