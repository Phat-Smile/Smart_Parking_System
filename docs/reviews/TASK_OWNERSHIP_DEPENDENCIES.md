# TASK_OWNERSHIP_DEPENDENCIES.md
> Phân tích ownership và dependencies — tính đến 2026-10-15

---

## 1. Trạng thái Booking Service (BE-3 Bách)

### Trên `develop`
- **Domain:** EMPTY — `Booking.Domain.csproj` không có file `.cs` nào
- **Application:** Chỉ có boilerplate (ValidationBehavior, Outbox/Inbox interfaces)
- **Endpoints:** Không có
- **Migrations:** 3 (foundation — outbox_messages, inbox_messages, __EFMigrationsHistory only)

### Trên feature branch `features/Implementation_ReservationDomain`
```
Booking.Domain/Reservations/
├── Reservation.cs               (158 lines — Reservation aggregate)
├── ReservationStatus.cs         (10 lines — enum: Pending/Confirmed/Cancelled/Expired/Completed)
├── ReservationType.cs           (7 lines — enum: Prepaid/WalkIn)
├── VehicleGroup.cs              (7 lines — enum: Car/Motorbike)
├── CancelReason.cs              (9 lines)
├── PrepaidOutcome.cs            (8 lines)
└── ReservationDomainException.cs (12 lines)

Booking.Persistence/Reservations/
└── ReservationConfiguration.cs  (104 lines — EF config)

Migrations/
└── 20261012152051_AddReservation.cs (70 lines)

tests/Booking/Booking.UnitTests/Reservations/
└── ReservationTransitionTests.cs    (198 lines)
```

**Status:** PR tồn tại trên remote — **chưa được merge vào develop**.

### Tại sao trống?
Branch `features/Implementation_ReservationDomain` có commit ngày **2026-10-12** bởi "Hundereds-Bach". Trước ngày đó, service hoàn toàn trống. PR chưa được approve và merge.

---

## 2. Trạng thái Payment Service (BE-5 Bảo / Trần Gia Bảo)

### Trên `develop`
- **Domain:** EMPTY — `Payment.Domain.csproj` không có file `.cs` nào
- Tương tự Booking — chỉ có foundation boilerplate

### Trên các feature branches (unmerged)

| Branch | Nội dung | Dòng code |
|---|---|---|
| `features/Implementation_WalletLedgerDomain` | `Wallet.cs` aggregate, `WalletLedgerEntry.cs`, `WalletStatus/LedgerEntryType/ReferenceType` enums, EF config, migration | ~450 |
| `features/Implementation_PricingEngine` | `PricingEngine.cs` domain service, `DomainErrorMessages` constants | ~200 |
| `features/Implementation_VnPaySandbox` | `IVnPayService.cs`, `VnPayService.cs`, `VnPayOptions.cs`, `VnPayResponseCodes.cs` | ~350 |
| `features/Implementation_WalletBalance` | Wallet balance lookup endpoint | ~100 |
| `features/Implementation_ConfigurePricing` | Note: branch này sửa **Parking** service (ConfigurePricing command), không phải Payment | ~300 |

**Tổng test:** Bảo có ~500+ dòng unit tests trên các branches (WalletTests 232 lines, VnPayServiceTests 268 lines, PricingEngineTests 233 lines).

**Status:** Tất cả các branches đều **chưa merge vào develop**. Bảo đã làm rất nhiều work nhưng không có gì trên develop branch.

---

## 3. Dependency Map cho các tasks còn lại

```
Identity (BE-1 Lộc)
├── GET /me              [P0] → unblocks FE Profile page
├── Cookie Refresh       [P1] → unblocks cross-tab auth UX
├── Sessions CRUD        [P1] → unblocks Security Settings page
├── Admin Search         [P1] → unblocks Admin User Management page
├── Admin Suspend/Activate [P1] → unblocks Admin User Management page
└── gRPC VehicleLookup   [✅ done]

Parking (BE-4 Thịnh)
├── PricingPolicy domain [branch: features/Implementation_ConfigurePricing]
├── Zone/Spot entities   [not started]
├── Capacity management  [not started — depends on Zone/Spot]
└── Session close/payment [blocked — depends on Payment]

Booking (BE-3 Bách)
├── Reservation domain   [branch: features/Implementation_ReservationDomain — merge pending]
├── Hold/Confirm flow    [not started — depends on Reservation + Parking capacity]
└── Reservation → Session link [blocked — depends on Parking]

Payment (BE-5 Bảo)
├── Wallet domain        [branch: WalletLedgerDomain — merge pending]
├── PricingEngine        [branch: PricingEngine — merge pending]
├── VnPay integration    [branch: VnPaySandbox — merge pending]
└── Charge flow          [partially — depends on merge of above]

Notification
└── Booking/Payment consumers [blocked — depends on events from Booking/Payment]

AI Service
└── All ML logic          [blocked — depends on Parking data]
```

---

## 4. Danh sách tasks của BE-1 (Lộc / locTX-1002)

### Có thể làm ngay (unblocked)

| Task | File cần tạo | Effort | Priority |
|---|---|---|---|
| `GET /api/identity/accounts/me` | `GetMeQuery`, `GetMeQueryHandler`, `AccountEndpoints.cs` thêm route | 2h | P0 |
| `GET /api/identity/accounts/sessions` | `ListSessionsQuery`, handler | 1.5h | P1 |
| `DELETE /api/identity/accounts/sessions/{sessionId}` | `RevokeSessionCommand`, handler | 1h | P1 |
| `GET /api/identity/admin/accounts` | `SearchAccountsQuery`, handler + paging | 3h | P1 |
| `POST .../admin/accounts/{id}/suspend` | `SuspendAccountCommand`, handler | 2h | P1 |
| `POST .../admin/accounts/{id}/activate` | `ActivateAccountCommand`, handler | 1.5h | P1 |

### Làm song song được

| Task | Ghi chú |
|---|---|
| Cookie auth (Set-Cookie khi login) | Không conflict với các tasks trên |
| Unit tests cho tất cả tasks trên | Viết cùng lúc với implementation |

### Chỉ cần chuẩn bị / chờ

| Task | Chờ gì |
|---|---|
| Emit `AccountSuspended` event qua Outbox | Chờ architecture decision về event naming |
| Profile CRUD (PUT /me) | Chờ FE design hoàn chỉnh về fields |

### Blocked (không thể làm một mình)

| Task | Blocked bởi |
|---|---|
| `AccountActivated` integration event consumer ở Notification | Notification team |
| Vehicle REST CRUD | Cần confirm VehicleOwnership aggregate design |

---

## 5. Merge queue ưu tiên

Các PRs cần merge vào `develop` càng sớm càng tốt để unblock pipeline:

```
Priority 1: features/Implementation_ReservationDomain (Booking — Bách)
Priority 2: features/Implementation_WalletLedgerDomain (Payment — Bảo)
Priority 3: features/Implementation_PricingEngine (Payment — Bảo)
Priority 4: features/Implementation_VnPaySandbox (Payment — Bảo)
Priority 5: features/Implementation_ConfigurePricing (Parking — Thịnh)
Priority 6: features/Implementation_WalletBalance (Payment — Bảo)
```

**Note:** Mỗi branch cũng có Jenkinsfile diff (`100 +/-`) — kiểm tra xem có conflict không trước khi merge.
