# MAPBOX_MIGRATION_PLAN.md
> Xác nhận và kế hoạch Mapbox — 2026-10-15

---

## 1. Hiện trạng (thực tế trong code)

### Frontend đã dùng Mapbox từ đầu

| File | Nội dung |
|---|---|
| `web/package.json` | `mapbox-gl@3.32.0`, `react-map-gl@8.1.3`, `@types/mapbox-gl@3.4.1` |
| `web/src/shared/map/map-canvas.tsx` | `import { getPublicMapboxToken }` — đọc Mapbox token từ env |
| `web/src/shared/map/map-scene.tsx` | `import Map from "react-map-gl/mapbox"` — render Mapbox map |

### KHÔNG CÓ Leaflet trong codebase

```
grep "leaflet" web/package.json → (nothing)
grep "leaflet" web/src/**/* → (nothing)
```

### Rules CLAUDE.md còn cũ

- `.notes/CLAUDE.md` vẫn ghi: "Bản đồ chỉ dùng OpenStreetMap + Leaflet, Nominatim, OSRM. Không còn Mapbox."
- Memory file `architecture-decisions.md` và `feedback-project-rules.md` đã lưu rule "no Mapbox" — **STALE**
- Thực tế: FE team đã build với Mapbox từ commit đầu

---

## 2. Quyết định người dùng đã xác nhận

User (Lộc, BE-1, Tech Lead) đã **xác nhận chuyển sang Mapbox**. Đây là quyết định chính thức cần được phản ánh trong:

1. `.notes/CLAUDE.md` — xóa dòng "Không còn Mapbox", thêm "Dùng Mapbox"
2. `docs/architecture/` hoặc một file decision mới
3. Memory files (cập nhật)

---

## 3. Phạm vi tác động

### 3.1 Frontend (ảnh hưởng ít — đã dùng Mapbox rồi)

| Component | Hiện tại | Cần làm |
|---|---|---|
| `map-scene.tsx` | ✅ Đã dùng `react-map-gl/mapbox` | Chỉ cần thiết kế business markers |
| `map-canvas.tsx` | ✅ Đọc `NEXT_PUBLIC_MAPBOX_TOKEN` | Cần thêm env var vào Docker + `.env.example` |
| Map search page | ❌ Chưa có | Build với Mapbox GL JS + react-map-gl |
| Geocoding | ❌ Chưa có | Dùng **Mapbox Geocoding API** |
| Routing/ETA | ❌ Chưa có | Dùng **Mapbox Directions API** (hoặc giữ OSRM cho AI service) |

### 3.2 AI Service (ảnh hưởng nhỏ)

- AI Service dùng OSRM cho ETA calculation (chưa implement)
- OSRM vẫn có thể dùng cho AI-side routing (server-to-server, không cần Mapbox token)
- Hoặc dùng Mapbox Directions API từ AI service (cần token)
- **Quyết định này chưa cần ngay** — AI service chưa có ML code

### 3.3 Backend (KHÔNG ảnh hưởng)

- Backend không gọi map API trực tiếp
- `lat`/`lng` coordinates trong `ParkingLot` entity vẫn là `decimal` — không phụ thuộc provider
- Geocoding khi tạo lot mới: FE gọi Mapbox API, gửi lat/lng qua REST API

### 3.4 Pricing (KHÔNG ảnh hưởng)

- `TECH-03` (distance-based pricing) nếu cần sẽ dùng AI service ETA
- Không ảnh hưởng đến Mapbox migration

---

## 4. Kế hoạch thực hiện

### Phase 1 — Setup & Infra (không cần approval từ đây)

- [ ] Thêm `NEXT_PUBLIC_MAPBOX_TOKEN` vào `.env.example` (giá trị placeholder)
- [ ] Cập nhật `docker-compose.yml` để truyền env var này vào FE container
- [ ] Cập nhật `.notes/CLAUDE.md` — xóa "không dùng Mapbox"
- [ ] Cập nhật `docs/architecture/` nếu có mô tả về map provider

### Phase 2 — FE Map Implementation

> **CẦN APPROVAL trước khi implement**

- [ ] Parking search map page với Mapbox GL
- [ ] Geocoding khi search bãi đỗ → Mapbox Geocoding API
- [ ] Lot markers, clustering
- [ ] Zoom vào lot detail

### Phase 3 — AI ETA (nếu cần Mapbox route)

> **Chưa cần trong sprint này**

---

## 5. Điểm cần quyết định (OPEN)

| # | Câu hỏi | Tác động |
|---|---|---|
| OD-MAP-01 | Dùng Mapbox token loại nào? (public vs. secret) | Token trong FE phải là PUBLIC token (dùng `NEXT_PUBLIC_`) |
| OD-MAP-02 | AI service dùng Mapbox Directions hay giữ OSRM? | Nếu Mapbox → cần token ở server side (secret) |
| OD-MAP-03 | Rate limit / quota plan nào? | Mapbox free tier giới hạn requests/tháng |

---

## 6. Files cần cập nhật ngay (non-code)

```
.notes/CLAUDE.md          — xóa dòng "Không còn Mapbox"
.env.example              — thêm NEXT_PUBLIC_MAPBOX_TOKEN=
docker-compose.yml         — thêm env var vào FE service
docs/reviews/architecture-decisions.md (hoặc memory) — update
```

**Lưu ý:** Không cần migration code — FE đã cài đúng packages rồi.
