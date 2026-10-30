# Hợp đồng Sự kiện #7: `slot.status.changed` (Cập nhật trạng thái chỗ đỗ)

> **Mã hợp đồng:** `EVENT-PARKING-007`  
> **Topic / Routing Key:** `parking.slot-status-changed.v1`  
> **Nguồn phát (Publisher):** `Parking.WebAPI` (Parking Service)  
> **Nguồn nhận (Consumers):** 
> - `SignalR Hub` (Parking Service / Gateway): Broadcast tới Frontend Dashboard và Sơ đồ 3D (`NFR-02`).
> - `AI Recommendation Service`: Cập nhật ma trận chỗ trống phục vụ gợi ý bãi xe (`FR-AI-001`, `FR-AI-002`).

---

## 1. Mục đích nghiệp vụ

Vị trí đỗ xe ô tô (`ParkingSlot`) chịu sự chi phối từ 3 lớp trạng thái độc lập (Quy tắc `BR-06.a`):
1. **Lớp cảm biến (`SensorState`)**: Báo xe vào / ra từ cảm biến IoT tại từng slot (`Empty`, `Occupied`).
2. **Lớp ghi đè (`OverrideState`)**: Lệnh thủ công từ Chủ bãi hoặc Nhân viên bãi (`Maintenance`, `Unavailable`).
3. **Lớp điều phối cổng (`DispatchState`)**: Gán slot tự động khi xe qua cổng vào (`Assigned`, `NeedsVerification`).

Khi bất kỳ lớp nào thay đổi dẫn đến **trạng thái hiệu lực (`EffectiveStatus`)** của slot bị thay đổi, Parking Service phát sự kiện `SlotStatusChangedIntegrationEvent` qua Transactional Outbox.

---

## 2. Thứ tự ưu tiên 6 trạng thái hiệu lực (Quy tắc `BR-06.a` & Điểm mở #21)

Thứ tự ưu tiên được xác định nghiêm ngặt từ cao xuống thấp (trạng thái xuất hiện trước sẽ thắng tuyệt đối):

| Thứ tự | Trạng thái hiệu lực (`SlotEffectiveStatus`) | Mã số | Lớp chi phối | Ý nghĩa nghiệp vụ |
|---|---|---|---|---|
| **1 (Cao nhất)** | `Unavailable` | 4 | `OverrideState.Unavailable` | Bãi đóng, slot bị khóa cứng, cấm đỗ tuyệt đối. |
| **2** | `Maintenance` | 3 | `OverrideState.Maintenance` | Slot đang bảo trì kỹ thuật, sơn kẻ hoặc hỏng cảm biến. |
| **3** | `NeedsVerification` | 6 | `DispatchState.NeedsVerification` | Bất thường tại cổng (biển số lạ/mờ cần nhân viên kiểm tra). |
| **4** | `Assigned` | 5 | `DispatchState.Assigned` | Slot đã được điều phối cho một phiên xe vừa qua cổng vào. |
| **5** | `Occupied` | 2 | `SensorState.Occupied` | Cảm biến tại chỗ phát hiện xe đang đỗ. |
| **6 (Thấp nhất)** | `Available` | 1 | Mặc định (Tất cả lớp đều trống) | Slot sẵn sàng đón xe vào đỗ. |

---

## 3. Cấu trúc Message Schema (JSON / Event Contract)

### 3.1. Định dạng C# Record (`ParkingSystem.Contracts.Events.Parking`)

```csharp
namespace ParkingSystem.Contracts.Events.Parking;

public record SlotStatusChangedIntegrationEvent(
    Guid EventId,
    Guid LotId,
    Guid ZoneId,
    Guid SlotId,
    string SlotCode,
    string EffectiveStatus,
    string? PreviousStatus,
    string Source,
    DateTimeOffset OccurredAtUtc,
    int Version
);
```

### 3.2. Payload JSON mẫu

```json
{
  "eventId": "a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
  "lotId": "10000000-0000-0000-0000-000000000001",
  "zoneId": "20000000-0000-0000-0000-000000000001",
  "slotId": "30000000-0000-0000-0000-000000000001",
  "slotCode": "A-01",
  "effectiveStatus": "Occupied",
  "previousStatus": "Assigned",
  "source": "Sensor",
  "occurredAtUtc": "2026-10-30T09:50:00.000Z",
  "version": 4
}
```

### 3.3. Ý nghĩa các trường dữ liệu

| Trường | Kiểu dữ liệu | Bắt buộc | Mô tả |
|---|---|---|---|
| `eventId` | `Guid` (UUIDv4) | Có | Định danh duy nhất của sự kiện, phục vụ chống trùng lặp (Deduplication). |
| `lotId` | `Guid` | Có | Định danh bãi đỗ xe. |
| `zoneId` | `Guid` | Có | Định danh khu vực (Zone) chứa slot. |
| `slotId` | `Guid` | Có | Định danh duy nhất của slot đỗ. |
| `slotCode` | `string` | Có | Mã hiển thị của slot (ví dụ: `A-01`, `B-12`). |
| `effectiveStatus` | `string` | Có | Trạng thái hiệu lực mới: `Available`, `Occupied`, `Maintenance`, `Unavailable`, `Assigned`, `NeedsVerification`. |
| `previousStatus` | `string?` | Không | Trạng thái hiệu lực liền trước đó (dùng để phát hiện chuyển đổi trạng thái ở FE). |
| `source` | `string` | Có | Tác nhân gây thay đổi: `Sensor`, `ManualOverride`, `Dispatch`, `SystemReset`. |
| `occurredAtUtc` | `DateTimeOffset` | Có | Thời điểm thiết bị/tác nhân phát sinh sự kiện (`event_time`). |
| `version` | `int` | Có | Số thứ tự tăng dần (`RowVersion`), consumer dùng để bỏ qua các gói tin đến trễ/sai thứ tự. |

---

## 4. Quy tắc tiêu thụ sự kiện (Consumer Guidelines)

1. **Idempotency (Khử trùng lặp)**: Consumer phải lưu `eventId` và bỏ qua nếu sự kiện đã được xử lý trong vòng 7 ngày (`BR-06.d`).
2. **Out-of-Order Handling (Xử lý sự kiện đến trễ)**: Nếu nhận sự kiện có `version <= current_version` hoặc `occurredAtUtc < last_updated_utc`, consumer bỏ qua để tránh ghi đè dữ liệu mới bằng dữ liệu cũ.
3. **SignalR Broadcast**: Khi nhận sự kiện, Hub gửi thông điệp tới nhóm client đang mở bãi xe tương ứng (`lotId`):
   ```typescript
   connection.on("SlotStatusChanged", (event: SlotStatusChangedPayload) => {
       update3DSlotColor(event.slotCode, event.effectiveStatus);
   });
   ```
