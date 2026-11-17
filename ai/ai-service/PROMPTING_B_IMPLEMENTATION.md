# ParkWise — AI Parking Recommendations: phương án B (Pure Prompting)

**Trạng thái (17/11/2026):** MVP Backend đã triển khai và kiểm thử chức năng trên QA. Branch `features/Implementation_AI_PromptingRecommendations`, base `72b270cb`. Chưa nghiệm thu NFR latency, chưa rollout production; không chỉnh Frontend, schema/migration DB, Owner Location hoặc Public Details.

## 1. Luồng nghiệp vụ và ranh giới trách nhiệm

`Customer → Parking WebAPI (.NET) → AI Service (Python, HTTP nội bộ) → LLM → AI validator → Parking validator/enrichment → Customer`.

- Parking đọc tối đa 10 ứng viên gần tọa độ khách theo bán kính và loại xe thông qua nearby reader hiện hữu; lấy policy giá, held quota và lịch sử availability. Từ chối ứng viên không có giá áp dụng. `PerEntry`: giá một lượt; `Block`: số block làm tròn lên × giá block. Không tự gán giá mặc định.
- AI Service ước lượng thời gian từ Haversine (không phải OSRM/ETA tuyến đường), dự báo availability theo baseline lịch sử. Nếu thiếu mẫu tin cậy hoặc `heldQuotaKnown=false`, xác suất là `null`, `insufficientData=true`. Lịch sử QA có dữ liệu mô phỏng; không phải cam kết chắc chắn còn chỗ.
- Với `AI_RECOMMENDATION_MODE=prompting`, LLM chọn và xếp thứ tự tối đa 3 bãi. `balanced` diễn giải định tính thời gian 40%, khả năng còn chỗ 40%, giá 20%; không tuyên bố tính điểm số chính xác. Các ưu tiên riêng: `cheapest`, `fastest`, `availability`. Không chạy thuật toán xếp hạng legacy trong mode prompting.
- Prompt version `parking-top3-b-v1`. LLM chỉ nhận dữ liệu cần cho lựa chọn (ID, tên bãi, giá, thời gian ước tính, xác suất nếu đủ dữ liệu, loại xe, thời lượng, ưu tiên, thông tin chất lượng); không gửi GPS khách, JWT, biển số, điện thoại hoặc service key trong payload tới provider. Tên bãi là dữ liệu không đáng tin, cần chính sách dữ liệu/retention trước production.
- LLM trả JSON `recommendations` với `lotId`, `rank`, `reasonCode`; ID phải thuộc candidate, không trùng, rank liên tiếp và đúng số lượng. Không để bãi có dự báo <30% đứng đầu khi có ứng viên dự báo tin cậy >=30%. Bãi rủi ro vẫn có thể đứng hạng sau và có cảnh báo. AI dựng lý do tiếng Việt từ mã lý do và dữ liệu nguồn, không lấy văn bản tự do từ model. Mã lý do: `short_travel`, `affordable`, `arrival_availability`, `balanced`, `limited_data`.
- Parking kiểm tra toàn bộ response (số lượng, ID, rank, tên, giá, miền giá trị, metadata và lý do); bổ sung tọa độ, địa chỉ, khoảng cách và dùng giá đã tính từ policy. Không tự sắp xếp lại kết quả LLM hợp lệ. Nếu thất bại, dùng fallback phi AI có nhãn rõ ràng.
- `AI_RECOMMENDATION_MODE=legacy` là mặc định của code để tương thích/rollback, không phải prompting. Cần cấu hình `prompting` khi bật tính năng. Mode sai không tự chuyển sang legacy. `fallbackUsed=false` tự thân không xác nhận mode prompting nếu deployment dùng legacy.

## 2. API contract bàn giao FE

Public: `GET /api/parking/lots/recommendations` (anonymous theo endpoint hiện tại). FE gọi Parking, không gọi trực tiếp AI Service và không giữ service key.

| Query | Kiểu / ràng buộc | Mặc định |
|---|---|---|
| latitude | decimal, -90..90, bắt buộc | — |
| longitude | decimal, -180..180, bắt buộc | — |
| vehicleType | Car / Motorbike | Car |
| arrivalAt | DateTimeOffset, ISO 8601 có offset | UTC now + 30 phút |
| durationMinutes | int, 15..1440 | 120 |
| radiusKm | double, >0 và <=50 | 10 |
| topK | int, 1..10 | 3 |
| priority | balanced / cheapest / fastest / availability | balanced |

Prompting trả tối đa `min(3, số ứng viên hợp lệ, topK)`. Fallback có thể trả nhiều hơn 3 nếu yêu cầu `topK>3`; FE nên gửi `topK=3` cho UI Top 3. Không giả định luôn có ba kết quả.

Response envelope: `recommendedLots`, `fallbackUsed`, `fallbackReason`. Mỗi item gồm `lotId`, `name`, `address`, `latitude`, `longitude`, `rank`, `driveTimeMinutes`, `probabilityPercent`, `price` (VND cho thời lượng), `warningHighFullRisk`, `reasons`, `distanceKm`, `sampleCount`, `insufficientData`, `baselineSource`. `driveTimeMinutes`, `probabilityPercent` và `baselineSource` có thể null theo contract. FE cần hiển thị trạng thái chưa đủ dữ liệu, không đổi null thành 0%; không hiển thị fallback như kết quả AI. Không coi thời gian ước tính là ETA định tuyến. Một danh sách rỗng với `fallbackUsed=false` có thể đơn giản là không có ứng viên gần đó.

| Public fallbackReason | Ý nghĩa |
|---|---|
| `ai_service_timeout_or_unavailable` | HTTP client không có response đã deserialize (bao gồm lỗi HTTP, kết nối, timeout hoặc cấu hình; không khẳng định chỉ timeout) |
| `ai_recommendation_unavailable` | AI trả envelope fallback với danh sách rỗng |
| `ai_response_invalid` | Response đã deserialize nhưng không đạt contract |
| `no_applicable_pricing_policy` | Không có ứng viên có giá áp dụng |

Internal: `POST /internal/ai/recommendations`, header `X-Arrival-Service-Key`; `ARRIVAL_API_KEY` ở AI phải trùng `ArrivalIntegration__Key` ở Parking. Không đưa endpoint/key nội bộ cho browser. AI vẫn trả `ai_recommendation_unavailable` khi provider/validator lỗi; log server phân biệt `stage=provider` / `stage=validation`, `code` allowlisted, `upstream_status` nếu có. Không log nội dung prompt, response provider hoặc key ở log chẩn đoán mới. Không retry tự động.

## 3. Cấu hình QA đã kiểm thử (không chứa secret)

AI Service (Python 3.12, dependencies từ lock; Windows dùng bản lock tạm ngoài repo bỏ uvloop nếu cần):

```dotenv
AI_RECOMMENDATION_MODE=prompting
AI_LLM_MODEL=openai/gpt-oss-120b
AI_LLM_CHAT_COMPLETIONS_URL=https://api.groq.com/openai/v1/chat/completions
AI_LLM_TIMEOUT_SECONDS=12
AI_LLM_MAX_OUTPUT_TOKENS=1200
AI_LLM_USER_AGENT=curl/8.0.1
AI_GRPC_PORT=5603
```

`AI_LLM_API_KEY` và `ARRIVAL_API_KEY` được cung cấp riêng qua biến môi trường/secret manager. Không commit `.env` chứa secret. `AI_LLM_USER_AGENT` override là workaround QA đã xác nhận; mặc định code dùng `ParkWise-AI/1.0`. Cần xác nhận client/gateway được provider hỗ trợ chính thức trước production; không coi override là cam kết tương thích lâu dài. Provider mặc định URL OpenAI khi không cấu hình; bắt buộc HTTPS, key và model do operator thiết lập, JSON object mode. Không cần thêm gRPC business server cho luồng HTTP này.

Parking QA:

```dotenv
AiService__BaseUrl=http://127.0.0.1:8001
AiService__RecommendationTimeoutMs=15000
Seeding__Enabled=false
```

Client mặc định 5000 ms, clamp 100..15000 ms. QA đã chạy với ENV=20000 nhưng giá trị hiệu lực là 15000 ms. Nạp riêng `ArrivalIntegration__Key`, connection string PostgreSQL QA và RabbitMQ/JWT. Local AI HTTP 8001, Parking HTTP 5502, Parking gRPC 5602. Chỉ dùng DB QA, không chạy seed hoặc migration trên production. Khởi động AI: `python -m uvicorn app.main:app --host 127.0.0.1 --port 8001` từ `ai/ai-service`. Health `/health/ready` không chứng minh LLM thực sự hoạt động.

## 4. Kết quả QA trước commit

Các kết quả dưới đây là log do người triển khai cung cấp sau Fallback Diagnostics V1, không phải số liệu production.

| Suite | Passed / total | Failed |
|---|---:|---:|
| AI Python | 49/49 | 0 |
| Parking UnitTests | 597/597 | 0 |
| Parking IntegrationTests | 301/301 | 0 |
| Tổng automated | 947/947 | 0 |
| Live stability smoke | 8/8 | 0 |

Build UnitTests có 2 warning xUnit1051 tại ReservationAvailabilityPolicyCommandHandlerTests; IntegrationTests build thành công không warning/error trong log. AI suite gồm 100 tình huống contract giả lập trong một test, không cộng thêm 100 vào tổng. Hai vòng smoke sau patch đều HTTP 200, `fallbackUsed=false`:

| Scenario | Round 1 (ms) | Round 2 (ms) |
|---|---:|---:|
| cheapest | 3993 | 2791 |
| fastest | 1738 | 2257 |
| availability | 1700 | 2106 |
| Parking → AI (DB QA) | 1503 | 2143 |

Min 1503 ms, mean 2278.875 ms, max 3993 ms cho 8 request hỗn hợp. Round 2 cheapest chọn bãi 16.000 VND với xác suất 16,67% ở hạng 2 và có cảnh báo, khác Round 1 nhưng hợp lệ theo risk guard. Trước patch diagnostics từng có một fallback cheapest và lượt chạy lại thành công; nguyên nhân cụ thể chưa được xác định. Hai E2E QA có một bãi đủ điều kiện, giá 30.000 VND, xác suất null; Top 3 có bốn candidate được kiểm tra qua AI HTTP bằng dữ liệu giả lập. Chưa có live balanced riêng, chưa có E2E ba bãi thực, chưa có đánh giá 100 scenario × nhiều lượt trên LLM thật hoặc load test.

Kiểm thử từ repo (dừng app trước build trên Windows; dùng `&&` để không chạy DLL cũ khi build fail):

```bash
(cd ai/ai-service && python -m unittest discover -s tests -v)
dotnet build tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj -m:1 --disable-build-servers && dotnet test --project tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj --no-build --no-restore --minimum-expected-tests 597
dotnet build tests/Parking/Parking.IntegrationTests/Parking.IntegrationTests.csproj -m:1 --disable-build-servers && dotnet test --project tests/Parking/Parking.IntegrationTests/Parking.IntegrationTests.csproj --no-build --no-restore --minimum-expected-tests 301
```

Live QA dùng script `ParkWise_LLM_LiveSmoke.py --all --parking` ngoài repo (cần shared QA key, hai service healthy). Tuân thủ quota provider; không đưa secret vào log. Script live không nằm trong 16 file PR này.

## 5. Giới hạn và điều kiện rollout

- NFR <1 giây chưa đạt trên mẫu nói trên; 8 request không đủ ước lượng p95/SLA, chưa đo concurrency hoặc quota dài hạn. Lead Tech cần thống nhất tiêu chí hiệu năng, provider/model, ngân sách và rollout.
- Không có training/fine-tuning; output LLM không tất định dù temperature=0. Không nâng claim về độ chính xác dự báo còn chỗ từ dữ liệu QA mô phỏng.
- Chưa thay Haversine thành route ETA, chưa chứng minh lọc giờ mở cửa tại thời điểm đến/đặt chỗ được trên mọi tình huống. Recommendation không bảo đảm bãi còn chỗ hoặc giữ chỗ; nghiệp vụ booking cần xác minh độc lập.
- Chưa triển khai retry/circuit breaker, cache, rate limiting/budget riêng cho LLM, cost/usage metrics, production secrets/retention review hoặc FE integration. Endpoint public cần rà soát chống abuse trước khi bật provider thật cho production.
- FE/Lead Tech cần review `fallbackReason` mới, metadata, null và số lượng Top 3. Không tuyên bố production ready hoặc hoàn thành tất cả NFR. PR này tập trung implementation Backend và chứng cứ QA hiện có.
