# TECH-03 · Mapbox feasibility spike (Sprint 1, Khoa / BE-2)

**Provider nhóm dùng: MAPBOX** (không dùng OSRM/OSM làm mặc định theo context cũ). Đây là spike kỹ thuật, **không đăng ký endpoint HTTP mới**. `US-SEARCH-003` (S2), `US-SEARCH-006` (S3) sẽ triển khai theo hợp đồng đã review riêng; các API FE/BE đề xuất chưa có trong code S1.

## Chạy mock không cần token / không phát sinh phí

```sh
python scripts/mapbox/mapbox_spike.py --output docs/implementation/evidence/mapbox-mock.json
python -m unittest discover -s tests/mapbox -v
```

Mock response bao gồm **BOX-01 Search Box `/suggest` trong vùng Mỹ được hỗ trợ**, với UUIDv4 `session_token`, và Geocoding/Directions/Matrix. Search Box **không được gọi live tại Việt Nam** do vùng công bố chưa bao gồm VN. Mock response là dữ liệu giả lập, không đo chất lượng tìm địa chỉ Việt Nam và không xác nhận Mapbox thật hoạt động. JSON ghi `mode=MOCK`.

## Chạy live có token của nhóm, sau khi duyệt hạn mức chi phí

```sh
# Inject Mapbox token securely from .env or secret manager, không đưa vào Git/shell history.
# Biến môi trường cần có tên MAPBOX_ACCESS_TOKEN.
python scripts/mapbox/mapbox_spike.py --live \
  --timeout 6 --output docs/implementation/evidence/mapbox-live.json
```

Live thực hiện **3 request** với tọa độ mẫu tĩnh: (1) Geocoding v6 forward cho địa chỉ `1 Le Duan, Ho Chi Minh City, Vietnam`, `country=vn`, `autocomplete=false`; (2) Directions v5 `mapbox/driving`; (3) Matrix v1 một điểm nguồn tới ba điểm đích, `sources=0`, `destinations=1;2;3`, trả `duration,distance`. Không ghi token/raw URL/raw provider JSON vào evidence.

| Test | Đầu ra | Tiêu chí bước đầu |
| --- | --- | --- |
| BOX-01 (chỉ mock) | `suggestionCount`, `requiresRetrieve` | Kiểm tra cú pháp/session UUID của Search Box (không chứng minh hỗ trợ Việt Nam) |
| GEO-01 | `featureCount`, `features[].longitude/latitude` | HTTP thành công; **review độ chính xác địa chỉ Việt Nam bằng tay** |
| DIR-01 | `reachable`, `durationSeconds`, `distanceMeters` | Tuyến có kết quả, hoặc xác nhận trường hợp không có tuyến |
| MAT-01 | `destinationCount`, `items[]` | Cấu trúc 1×3 đúng; kết quả theo thứ tự đầu vào; `null` có thể hợp lệ khi không có tuyến |

**Thủ công bắt buộc trước khi đóng TECH-03:** thử nhiều địa chỉ ở TP.HCM, Hà Nội, địa chỉ không tồn tại; kiểm tra giới hạn/rate/chi phí từ tài khoản Mapbox; test token sai, rate limit và timeout với stub/mock để không tốn phí; đính kèm live evidence sau khi duyệt bảo mật. Không chia sẻ private token.

## Các giới hạn quan trọng

- Mapbox yêu cầu tọa độ **longitude,latitude**; Parking DTO hiện trả `latitude` và `longitude` theo trường riêng. Script chuyển đúng thứ tự.
- Mapbox Matrix `mapbox/driving` tối đa 25 tọa độ, còn `mapbox/driving-traffic` tối đa 10 tổng tọa độ. Một nguồn + 10 bãi chỉ chạy được `driving` trong một request; profile traffic phải giảm còn 9 bãi hoặc phân lô (cần phê duyệt).
- Các giá trị trong Matrix tính theo **giây** và **mét**; `null` là không có tuyến. Matrix không trả geometry.
- Geocoding v6 mặc định temporary, **không cache hoặc lưu kết quả** trừ khi hợp đồng Mapbox cho phép permanent. Cũng cần kiểm tra giới hạn của Mapbox Search Box tại Việt Nam trước khi đưa autocomplete vào backlog.
- Token phải có scope phù hợp với API; URL restrictions của public browser token có thể khiến server-side calls bị `403`.
- Mock xử lý lỗi `401`,`403`,`422`,`429`, network/timeout và provider payload không hợp lệ. Không tự retry `429` để tránh tăng lưu lượng/chi phí.

## Links tham khảo chính thức

- [Geocoding v6](https://docs.mapbox.com/api/search/geocoding/)
- [Directions v5](https://docs.mapbox.com/api/navigation/directions/)
- [Matrix v1](https://docs.mapbox.com/api/navigation/matrix/)
- [Search Box](https://docs.mapbox.com/api/search/search-box/)

**Tiêu chí `Xong`:** 15+ tests offline pass, Mapbox live evidence hợp lệ, ghi nhận chính sách/giới hạn/tối thiểu 1 vùng Việt Nam, FE/BE thống nhất contract và reviewer chấp nhận. Mock pass không được tự đổi thành `Xong`.
