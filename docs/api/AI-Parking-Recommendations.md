# AI Parking Recommendations API & Contract Specification

> **Mã tính năng:** US-AI-001 / FR-AI-001  
> **Quy chuẩn nghiệp vụ:** BR-08 (Gợi ý tối đa 3 bãi đỗ xe theo tiện ích đa tiêu chí)  
> **Phiên bản:** v1.0  
> **Dịch vụ cung cấp:** `ai-service` (cổng gRPC 50051 & HTTP nội bộ `/internal/ai/recommendations`)  
> **Dịch vụ tiêu thụ:** `Parking.WebAPI` (nội bộ cluster, không public trực tiếp ra Internet)

---

## 1. Tổng quan & Luồng nghiệp vụ

Khi khách hàng tìm kiếm bãi đỗ xe với tọa độ và thời điểm dự kiến tới, Parking Service lọc ra tối đa 10 bãi đỗ phù hợp (đã được duyệt, đúng loại xe, trạng thái hoạt động) và gửi sang `ai-service` để chấm điểm và xếp hạng tiện ích đa tiêu chí:

```
Khách hàng (Web/App) ──► GET /api/parking/lots/recommendations?lat&lon&vehicleType&arrivalAt&durationMinutes
                              │
                    Parking.WebAPI (Lọc tối đa 10 bãi ứng viên)
                              │
                              ▼ (gRPC hoặc HTTP nội bộ kèm Header x-arrival-service-key)
                        AI Service (Python 3.12, xử lý < 10ms)
                              ├─ Xác suất còn chỗ lúc tới (Laplace Smoothing)
                              ├─ Thời gian di chuyển (Haversine + Cache 60s)
                              ├─ Chấm điểm tiện ích Min-Max (0..100)
                              ├─ Xử phạt 30 điểm nếu xác suất < 30% (BR-08.a)
                              ├─ Chặn không cho bãi có nguy cơ đầy xếp Hạng 1 (BR-08.a)
                              └─ Sinh lý do trực quan (reasons) bằng Tiếng Việt
```

---

## 2. Quy tắc chấm điểm (Scoring Invariants — BR-08)

1. **Chuẩn hóa Min-Max (0..100):**
   * Thời gian lái xe: Thấp hơn là tốt hơn ($S_t$).
   * Giá gửi xe: Thấp hơn là tốt hơn ($S_c$).
   * Xác suất còn chỗ lúc tới: Cao hơn là tốt hơn ($S_p$).
2. **Trọng số mặc định (BR-08.c):**
   * Thời gian lái xe: `0.40`
   * Xác suất còn chỗ: `0.40`
   * Mức giá: `0.20`
3. **Cảnh báo và Phạt điểm (BR-08.a):**
   * Nếu xác suất $p < 30\%$:
     * Gắn cờ `warningHighFullRisk = true`.
     * Trừ 30 điểm phạt: $S_{final} = \max(0, S_{raw} - 30)$.
     * **Luật Hạng 1:** Bãi có $p < 30\%$ **không được xếp Hạng 1** nếu còn bất kỳ bãi nào khác có $p \ge 30\%$.
4. **Trường hợp thiếu thời gian lái xe (BR-08.d):**
   * Tự động điều chỉnh phân bổ trọng số: Xác suất `0.67`, Giá `0.33`, Thời gian `0.0`.
   * Giao diện và API thông báo lý do: `"Chưa có thời gian lái xe (được sắp theo xác suất & giá)"`.
5. **Hòa điểm (Tie-breaking):**
   * Xác suất cao hơn $\rightarrow$ Giá thấp hơn $\rightarrow$ Thời gian ngắn hơn $\rightarrow$ Mã bãi (`lotId`) tăng dần theo thứ tự từ điển.

---

## 3. Đặc tả Hợp đồng gRPC (`contracts/grpc/parking_recommendation.proto`)

```protobuf
syntax = "proto3";

package smartparking.parking.recommendation.v1;

option csharp_namespace = "SmartParking.Contracts.Parking.Recommendation.V1";

service ParkingRecommendationContract {
  rpc RecommendLots(RecommendLotsRequest) returns (RecommendLotsResponse);
}
```

---

## 4. Đặc tả HTTP REST Nội bộ

### `POST /internal/ai/recommendations`

* **Bảo mật:** Bắt buộc Header `x-arrival-service-key: <ARRIVAL_API_KEY>` (Fail-closed: Trả về HTTP 403 nếu sai hoặc thiếu).

#### Request Body
```json
{
  "customerLatitude": 10.7725,
  "customerLongitude": 106.6980,
  "vehicleType": "Car",
  "arrivalAt": "2026-11-07T10:00:00Z",
  "durationMinutes": 120,
  "candidateLots": [
    {
      "lotId": "11111111-1111-1111-1111-111111111111",
      "name": "Bãi đỗ A (Trung tâm)",
      "latitude": 10.7750,
      "longitude": 106.7000,
      "priceForDuration": 20000.0,
      "heldQuota": 2,
      "heldQuotaKnown": true,
      "snapshots": []
    }
  ]
}
```

> [!NOTE] **Giới hạn số suất giữ (Held Quota Limitation):**  
> Trường `heldQuota` được đọc song song qua `IHeldQuotaProvider` từ Booking Service (với cache 60s, timeout 2s). Hiện số suất giữ lấy từ bộ đọc tạm (`StubHeldQuotaReader`), luôn bằng 0, cho tới khi có hợp đồng #4 với Booking. Đây là **số suất giữ hiện tại** tại thời điểm gọi API, chưa phải số suất giữ thực tế tại thời điểm xe tới trong tương lai. Đây là giới hạn kỹ thuật đã biết và được thừa nhận. Khi Booking không phản hồi hoặc timeout, hệ thống gửi `heldQuota = 0` kèm `heldQuotaKnown = false` để AI gắn cờ `insufficientData = true` thay vì âm thầm ngộ nhận bãi không có suất giữ.

#### Response Body (HTTP 200 OK)
```json
{
  "recommendedLots": [
    {
      "lotId": "11111111-1111-1111-1111-111111111111",
      "name": "Bãi đỗ A (Trung tâm)",
      "rank": 1,
      "driveTimeMinutes": 5.2,
      "probabilityPercent": 85.0,
      "price": 20000.0,
      "warningHighFullRisk": false,
      "reasons": [
        "Lựa chọn tối ưu tổng thể",
        "Gần bạn nhất (khoảng 5 phút lái xe)",
        "Tỷ lệ còn chỗ rất cao"
      ],
      "sampleCount": 16,
      "insufficientData": false,
      "baselineSource": "lot_weekday_15m"
    }
  ],
  "fallbackUsed": false,
  "fallbackReason": null
}
```

> [!IMPORTANT] **Quy tắc Trả Xác suất Khi Dự phòng (Fail-Closed Fallback):**  
> Khi AI service gặp sự cố, timeout (>700ms) hoặc không có dữ liệu, hệ thống kích hoạt chế độ Fallback sắp xếp theo khoảng cách Haversine và sức chứa thực tế. Khi đó:
> - `fallbackUsed = true`
> - `probabilityPercent = null` (Tuyệt đối không bịa đặt số xác suất giả 95/85/70/25%)
> - `insufficientData = true`
> - `baselineSource = "fallback"`
