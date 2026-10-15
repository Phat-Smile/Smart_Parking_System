# PROJECT_CONTEXT.md
> Trạng thái dự án tính đến 2026-10-15

---

## 1. Tổng quan hệ thống

**Smart Parking System** — hệ thống đặt chỗ và quản lý bãi đỗ xe thông minh (OJT Kỳ 5).

| Thuộc tính | Giá trị |
|---|---|
| Solution | `ParkingSystem.slnx` — 41 .NET project |
| SDK | .NET 10.0.302 (pinned `global.json`) |
| Language | C# 14 |
| Branch chính | `develop` |
| Remote | `Phat-Smile/Smart_Parking_System` |
| Tests (develop) | **461 tests** ✅ (12 assemblies) |

---

## 2. Tiến độ triển khai theo service

| Service | Owner | Trên `develop` | Trên Feature Branches | Ưu tiên |
|---|---|---|---|---|
| **Identity** | BE-1 Lộc | ~70% | 5 PRs unmerged (cookie, GET /me, sessions, admin endpoints) | 🔴 Critical — unblock FE |
| **Parking** | BE-4 Thịnh | ~25% | `features/Implementation_ConfigurePricing` (pricing + commands) | 🟠 High |
| **Booking** | BE-3 Bách | 0% business | `features/Implementation_ReservationDomain` (Reservation domain) | 🔴 Critical — empty on develop |
| **Payment** | BE-5 Bảo | 0% business | 5+ branches: Wallet, PricingEngine, VnPay, WalletBalance | 🔴 Critical — empty on develop |
| **Notification** | BE-? | ~15% | — | 🟡 Medium |
| **AI Service** | — | ~5% | — | 🟢 Low (OJT scope) |
| **Gateway** | Phát | Foundation | — | ✅ Done for now |
| **Frontend** | FE team | ~20% | Auth UI done, Map scaffold exists | 🟠 High |

---

## 3. Vấn đề cấp bách nhất

### 3.1. Feature branches CHƯA merge vào develop

Booking và Payment đều có work đã làm nhưng **chưa được merge PR**:

| Branch | Service | Nội dung | Tác giả |
|---|---|---|---|
| `features/Implementation_ReservationDomain` | Booking | Reservation aggregate (158 lines), migration, 198-line unit tests | BE-3 Bách |
| `features/Implementation_WalletLedgerDomain` | Payment | Wallet aggregate, WalletLedgerEntry, persistence, 232-line unit tests | BE-5 Bảo |
| `features/Implementation_PricingEngine` | Payment | PricingEngine domain service (178 lines), 233-line tests | BE-5 Bảo |
| `features/Implementation_VnPaySandbox` | Payment | IVnPayService + VnPayService (185 lines), 268-line unit tests | BE-5 Bảo |
| `features/Implementation_WalletBalance` | Payment | Wallet balance lookup endpoint | BE-5 Bảo |
| `features/Implementation_ConfigurePricing` | Parking | ConfigurePricing command + PricingPolicy domain | BE-4 Thịnh |

**→ Hành động cần thiết:** Review và merge các PRs này vào develop.

### 3.2. Identity API contract gap

FE kỳ vọng nhưng BE chưa cung cấp:
- `GET /api/identity/accounts/me` — endpoint này **không tồn tại**
- `GET /api/identity/accounts/sessions` — **không tồn tại**
- `DELETE /api/identity/accounts/sessions/{sessionId}` — **không tồn tại**
- Admin: `GET /api/identity/admin/accounts` (search), `POST .../suspend`, `POST .../activate` — **không tồn tại**
- Cookie `sp_refresh` cho refresh token — **không implement**

Xem chi tiết: `FE_BE_API_CONTRACT_AUDIT.md`

### 3.3. Mapbox — FE đã dùng, rules cần update

- FE hiện tại: `mapbox-gl 3.32.0`, `react-map-gl 8.1.3`, `map-scene.tsx` đã dùng Mapbox
- `.notes/CLAUDE.md` vẫn còn rule "không dùng Mapbox" — **STALE**
- User đã xác nhận: chuyển sang Mapbox

Xem chi tiết: `MAPBOX_MIGRATION_PLAN.md`

---

## 4. Kiến trúc foundation (đã hoàn thành)

- ✅ Per-service PostgreSQL schema + EF migrations
- ✅ Custom Outbox/Inbox (transactional)
- ✅ MassTransit + RabbitMQ bus wiring
- ✅ YARP gateway với route config
- ✅ Health endpoints (`/health/live`, `/health/ready`)
- ✅ OpenTelemetry + Jaeger + Prometheus
- ✅ gRPC VehicleLookup (Identity → Parking)
- ✅ JWT RS256 asymmetric

---

## 5. Môi trường hạ tầng

| Component | Host Port | Container |
|---|---|---|
| PostgreSQL 17 | 5433 | 5432 |
| RabbitMQ 4 | 5672/15672/1883 | same |
| Identity | 5001 | 8080 |
| Parking | 5002 | 8080 |
| Booking | 5003 | 8080 |
| Payment | 5004 | 8080 |
| Notification | 5005 | 8080 |
| AI | 8000 | 8000 |
| Gateway | 5000 | 8080 |
