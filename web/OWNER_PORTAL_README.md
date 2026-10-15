# Smart Parking System – Parking Owner Portal

Rebuilt Parking Owner portal of **Smart Parking System** designed and implemented to look and behave **EXACTLY** like the final reference prototype (`smart-parking-prototype.html` / `smart-parking-prototype.txt`).

---

## 🌟 Highlights & Specifications

1. **Brand Identity:**
   - Product Name: **Smart Parking System** everywhere.
   - Logo: Solid black rounded square with crisp white "P" accompanied by the product title.
   - Typography: **Be Vietnam Pro** (weights 400 through 800).
   - Language: 100% Vietnamese UI text, `vi-VN` currency formatting (`37,6 tr` / `272.090.469 đ`), date format `dd/mm/yyyy`.

2. **Design System & Aesthetics:**
   - **Monochrome UI Chrome:** `--bg: #f4f4f2`, `--card: #ffffff`, `--line: #e4e4e1`, `--ink: #0b0b0b`, `--mut: #66665f`, `--soft: #f0f0ee`.
   - **Rich Visual Accent Colors:**
     - Primary Green Revenue Fill: `#0E9F6E`
     - KPI Card Top Borders: `#0E9F6E`, `#E5484D`, `#2f9e44`, `#2F80ED`, `#F2A007`, `#8A5CF6`, `#0891b2`
     - Lot Card Header Gradients by Approval:
       - Approved: `#c9efd8 -> #7fcfa3`
       - Pending: `#fdebbd -> #f4c969`
       - Rejected: `#fcd3d3 -> #f19a9a`
       - Draft: `#e4e4e1 -> #cfcfca`
     - Slot Status System:
       - `available` (#2f9e44) — *Trống*
       - `reserved` (#2F80ED) — *Đã đặt trước*
       - `occupied` (#E5484D) — *Có xe*
       - `maintenance` (#F2A007) — *Bảo trì*
       - `verify` (#8A5CF6) — *Cần xác minh*

3. **3D WebGL Visualization (`mk3D` port):**
   - High-performance Three.js implementation in `src/components/viz/parkingScene3d.ts`.
   - **Scale:** 500 parking slots across 5 floors (T1..T5), 4 rows (A, B, C, D) x 25 bays per floor.
   - **Architecture:** Real meter scale (bay 2.5x5.0m, floor height 3.2m, 64x34m decks, drop beams, square columns, 4-turn helical ramp tower at east end, glass stair/lift core at west end, louvered roof canopy, asphalt plinth, grass and trees).
   - **InstancedMesh Cars:** Three archetypes (Sedan, SUV with roof rails, EV with light bar) rendered via `InstancedMesh` with `setColorAt` supporting white, blue, red, black, green, and orange finishes.
   - **Lighting & Materials:** Studio environment reflection map, PCF soft shadow directional key light, ACES Filmic tone mapping.
   - **Performance:** Analytic raycaster picking against floor planes (instant sub-millisecond selection without raycasting thousands of individual meshes).
   - **Lifecycle Management:** Complete WebGL memory disposal and `forceContextLoss()` upon component unmount or route change.

4. **Single Source of Truth Store (`src/store/useOwnerStore.ts`):**
   - Zustand store governing 500 spots, active floor filter, selected slot, view mode (`both` / `3d` / `2d`), parking lots, zones, reservations, and report filters.
   - Realtime simulator updates slot statuses every 5 seconds; 2D grid, 3D scene, KPIs, donut, and floor bars stay in perfect sync.
   - Deterministic revenue engine:
     $$\text{dayRev}(y, m, d) = \sum_{h=0}^{23} \text{hourly}(y, m, d, h)$$
     $$\text{monRev}(y, m) = \sum_{d=1}^{\text{dim}(y, m)} \text{dayRev}(y, m, d)$$
     $$\text{yrRev}(y) = \sum_{m=1}^{12} \text{monRev}(y, m)$$

---

## 📂 Page Route Architecture

| Route | Page Name | Key Features |
|---|---|---|
| `/owner/dashboard` | **Tổng quan** | 7 KPI cards, revenue card with Hôm nay / 7 ngày / Tháng này segmented control + interactive area chart, status donut (5-color conic gradient), 5-floor vertical bars, recent activities. *(Strictly NO 3D scene)* |
| `/owner/parking-lots` | **Bãi của tôi** | Lot cards with striped gradient headers, approval badges, capacity, operating hours, progress bars, rejection reason banner, "+ Thêm bãi mới" button |
| `/owner/parking-lots/new` | **Đăng ký bãi mới** | 3-step wizard (Cấu hình, Xem trước 3D/2D, Gửi duyệt) with GPS `lat, lng` validation, review summary, confirmation modal, pending success state |
| `/owner/parking-lots/[id]` | **Chi tiết bãi** | 6 tabs (Thông tin, Khu vực, Loại xe & sức chứa, Giá, Đặt chỗ, Duyệt), zone CRUD with capacity sum validation, edit/save toggle with toast notification, pending lock state |
| `/owner/layout` | **Sơ đồ 2D / 3D** | Toggle Cả hai / 3D / 2D, floor tabs (Tất cả tầng + 5 floors), live status chips, 3D interactive viewer, 2D plan with aisle dividers and emojis, slot inspector |
| `/owner/reservations` | **Đặt chỗ** | Search filter, status select filter, reservation table with colored chips |
| `/owner/sessions` | **Phiên đỗ xe** | Active sessions table (first 40 sessions), "Xem ô" quick action button jumping directly to layout |
| `/owner/reports` | **Báo cáo doanh thu** | Date picker (default 20/10/2025), 3 period KPI cards (Ngày/Tháng/Năm with % delta), Day/Month/Year interactive chart, revenue breakdown bars |

---

## 🛠 Setup & Running Locally

```powershell
# Navigate to web workspace
cd web

# Run development server
npm run dev -- --hostname 127.0.0.1 -p 3000

# Typecheck (0 errors)
npm run typecheck

# Lint (0 errors)
npm run lint

# Production build
npm run build

# Unit tests
npx vitest run src/test/owner-portal.test.ts

# Playwright E2E smoke tests
npx playwright test e2e/owner-portal.spec.ts
```

---

## 🔌 Swapping Mocks for Real Backend API

The current application uses typed domain models and deterministic in-memory repositories encapsulated in `src/types/owner.ts` and `src/store/useOwnerStore.ts`.

To connect to a live backend service:

1. **Repository Layer Replacement:**
   Create an API client service in `src/lib/api/ownerClient.ts` that implements:
   - `fetchSpots(lotId: number): Promise<ParkingSpot[]>`
   - `fetchLots(): Promise<ParkingLot[]>`
   - `createLot(data: CreateLotDto): Promise<ParkingLot>`
   - `updateSpotStatus(spotId: string, status: SlotStatusKey): Promise<void>`
   - `fetchRevenueReport(date: string, mode: 'day' | 'month' | 'year'): Promise<RevenueData>`

2. **Zustand Action Binding:**
   In `src/store/useOwnerStore.ts`, replace synchronous state mutations with async thunks or TanStack Query mutations:
   ```ts
   updateSpotStatus: async (id, s) => {
     await ownerApi.updateSpotStatus(id, s);
     set((state) => ({
       spots: state.spots.map((spot) => (spot.id === id ? { ...spot, s } : spot)),
     }));
   }
   ```

3. **Realtime WebSocket / SignalR Integration:**
   Replace `simulateTick()` in `src/app/(owner)/owner/dashboard/page.tsx` with a SignalR or WebSocket listener subscribing to parking spot occupancy events:
   ```ts
   connection.on('SpotStatusChanged', (spotId: string, newStatus: SlotStatusKey) => {
     useOwnerStore.getState().updateSpotStatus(spotId, newStatus);
   });
   ```

---

## 🧪 Verification & Acceptance

- **Visual Comparison:** 22 reference screenshots (`web/reference-screenshots/*.png`) compared side-by-side with 22 app screenshots (`web/app-screenshots/*.png`). Layout, font sizes, colors, and 3D geometry match the prototype.
- **Strict Brand Validation:** All titles, sidebars, metadata, and badges read "Smart Parking System". Zero occurrences of "PARKINI" or "Smart Parking AI".
- **Code Quality:** Strict TypeScript typing without `any`, clean feature-based modularity, zero ESLint warnings, 100% passing unit and E2E smoke tests.
