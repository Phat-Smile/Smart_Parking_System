# COVERAGE_GAP_REPORT.md
> Test coverage gaps — tính đến 2026-10-15

---

## 1. Tổng quan

| Metric | Value |
|---|---|
| Tổng tests trên develop | **461 tests** (12 assemblies) |
| Identity unit tests | 257 |
| Parking unit tests | 25 |
| Notification unit tests | 17 |
| Architecture tests | 17 |
| WebFoundation tests | 26 |
| Payment unit tests | 1 (placeholder) |
| Booking unit tests | ~0 |

---

## 2. Gaps theo service

### 2.1 Identity — 257 tests, nhưng thiếu

| Gap | Test nên có | Priority |
|---|---|---|
| `GET /me` handler | `Returns_full_profile_for_authenticated_user`, `Handles_missing_profile_gracefully`, `Returns_401_without_jwt` | P0 |
| List sessions | `Returns_only_active_sessions`, `Marks_current_session` | P1 |
| Revoke session | `Revokes_correct_session`, `Rejects_other_users_session` | P1 |
| Admin search | `Filters_by_keyword`, `Filters_by_role`, `Paginates_correctly` | P1 |
| Admin suspend | `Suspends_account_and_revokes_sessions`, `Rejects_self_suspend`, `Rejects_suspend_admin` | P1 |
| Admin activate | `Activates_suspended_account` | P1 |
| Cookie integration | `Login_sets_sp_refresh_cookie`, `Refresh_reads_from_cookie`, `Logout_clears_cookie` | P1 |

### 2.2 Parking — 25 tests, nhưng thiếu

| Gap | Test nên có |
|---|---|
| Zone/Spot entities | N/A — chưa implement |
| ConfigurePricing (branch) | Tests tồn tại trên branch nhưng chưa merge |
| Exit session flow | N/A — chưa implement |

### 2.3 Booking — 0 tests trên develop

| Gap | Trạng thái |
|---|---|
| `ReservationTransitionTests` (198 lines) | Tồn tại trên `features/Implementation_ReservationDomain` — cần merge |
| Application command tests | Chưa có |
| Integration tests | Chưa có |

### 2.4 Payment — 1 test trên develop (placeholder)

| Gap | Trạng thái |
|---|---|
| `WalletTests` (232 lines) | Tồn tại trên `features/Implementation_WalletLedgerDomain` — cần merge |
| `VnPayServiceTests` (268 lines) | Tồn tại trên `features/Implementation_VnPaySandbox` — cần merge |
| `PricingEngineTests` (233 lines) | Tồn tại trên `features/Implementation_PricingEngine` — cần merge |

### 2.5 Notification — 17 tests

| Gap | |
|---|---|
| Consumer tests cho missing events | Booking/Payment events chưa define |

### 2.6 AI Service — 0 tests

Toàn bộ AI logic chưa implement.

---

## 3. Integration Test Coverage

| Test Project | Status |
|---|---|
| `Identity.IntegrationTests` | Có — chạy với Testcontainers |
| `Parking.IntegrationTests` | Có — chạy với Testcontainers |
| `Payment.IntegrationTests` | Có nhưng chỉ test Outbox isolation |
| `Booking.IntegrationTests` | Có nhưng chỉ test Outbox isolation |
| `Notification.IntegrationTests` | Có — tests consumers |

---

## 4. E2E Test Coverage (Playwright)

| Test File | Status |
|---|---|
| `web/e2e/login.spec.ts` | ✅ Exists |
| `web/e2e/registration.spec.ts` | ✅ Exists |
| `web/e2e/forgot-password.spec.ts` | ✅ Exists |
| `web/e2e/reset-password.spec.ts` | ✅ Exists |
| `web/e2e/sign-up.spec.ts` | ✅ Exists |
| `web/e2e/verify-otp.spec.ts` | ✅ Exists |
| `web/e2e/security-settings.spec.ts` | ❌ Deleted on feature branch |
| Profile/Vehicles E2E | ❌ Missing |
| Map/Parking E2E | ❌ Missing |
| Booking flow E2E | ❌ Missing |

---

## 5. Test chạy trước khi tạo PR

Checklist trước mỗi PR từ Identity:

```bash
# 1. Unit tests (không cần Docker, ~30s)
dotnet test --project tests/Identity/Identity.UnitTests/Identity.UnitTests.csproj

# 2. Architecture tests (không cần Docker, ~5s)
dotnet test --project tests/ArchitectureTests/ArchitectureTests.csproj

# 3. Integration tests (cần Docker, ~2-3 min)
docker compose up -d postgres rabbitmq
dotnet test --project tests/Identity/Identity.IntegrationTests/Identity.IntegrationTests.csproj
```

PR pipeline (Jenkins) sẽ skip integration tests — nhưng merge vào develop sẽ chạy toàn bộ.
