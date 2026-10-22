# AGENTS.md — SMART PARKING / PARKWISE

> **Phiên bản:** OJT Practical v1.0 · **Ngày soạn:** 2026-10-20  
> **Trạng thái:** `DRAFT — HUMAN REVIEW REQUIRED` (chưa tự động thay thế quy định trong repository)  
> **Áp dụng:** AI coding agents và thành viên phát triển Smart Parking System  
> **Mục tiêu:** MVP đúng nghiệp vụ, code dễ hiểu, test được, tích hợp được và demo được trong thời gian OJT. **Phân tích sâu nhưng triển khai gọn; không overengineering.**

## 0. Cách đọc file và thứ tự ưu tiên

1. **`AGENTS.md`**: quy tắc cộng tác Human–Agent, quyền thực thi, cách review và tiêu chí chất lượng.
2. **`CLAUDE.md`**: runbook kỹ thuật theo snapshot: câu lệnh build/test, bố cục source, thiết lập môi trường, messaging và các lưu ý công cụ. Dùng cho Claude và cả agent khác **sau khi đối chiếu repository đang làm việc**; không tự động coi mọi nhận định lịch sử trong file là hiện trạng.
3. **Tài liệu nghiệp vụ đã được duyệt** (Context, User Stories/AC, Main Flow, API Design, ADR) xác định *mong muốn nghiệp vụ*. **Git/source/DB schema/tests ở branch & commit đang kiểm tra** xác định *thực trạng triển khai*. Master Sheet là kế hoạch, không phải bằng chứng code đã chạy.
4. Khi có mâu thuẫn, ghi `CONFLICT` với nguồn và phương án; **không tự âm thầm sửa tài liệu, thay kiến trúc hoặc tuyên bố tài liệu nào luôn đúng**. Quyết định mới phải có Human phê duyệt.
5. Quy tắc môi trường, quyền hệ thống và an toàn dữ liệu có mức ưu tiên cao hơn hướng dẫn trong file này.

**Trước khi làm việc lớn:** xác định `repo path`, branch, HEAD SHA, working tree, tài liệu/PR thật sự đọc được. Nếu không có quyền truy cập, nói `NOT VERIFIED`, không giả tạo kết quả.

## 1. HUMAN-IN-THE-LOOP — NHANH NHƯNG RÕ QUYỀN

### 1.1. Mặc định và ba quyền khác nhau

Mặc định **`ANALYZE_ONLY`**: Agent được đọc source, tài liệu, xem Git diff/log, rà soát logic, thiết kế test, đề xuất phương án và **chạy test/build chỉ khi xác định an toàn cho dữ liệu/môi trường**. Không được sửa production/test code, cấu hình, migration, API contract, Master Sheet chính thức hoặc Git history chỉ từ một yêu cầu review.

| Quyền | Ý nghĩa | Human cấp như thế nào | Agent được làm gì |
|---|---|---|---|
| `PLAN_APPROVED` | Đồng ý hướng giải quyết | `APPROVED: PLAN <task>` hoặc “Đồng ý phương án của task <ID>” | Chốt kế hoạch; **chưa được sửa code** |
| `IMPLEMENT_APPROVED` | Cho phép thực thi một phạm vi | `APPROVED: IMPLEMENT <task> — <scope>` hoặc “Triển khai task <ID> theo plan vừa duyệt” | Tự viết/sửa code **và test liên quan**, debug, refactor nhỏ, build/test trong scope; **không tự commit/push** |
| `GIT_APPROVED` | Cho phép hành động Git cụ thể | `APPROVED: GIT COMMIT <task>`; `APPROVED: GIT PUSH <branch>`; `APPROVED: GIT PR <branch>`; `APPROVED: GIT MERGE <PR>` | Chỉ thực hiện **hành động được nêu**, trong branch/PR đã chỉ định |

- **Chỉ có 2 thời điểm cần báo cáo xin duyệt theo mặc định:** **G-PLAN** (cuối phân tích) và **G-SHIP** (sau tự code/test, trước hành động Git/release). `IMPLEMENT_APPROVED` là **quyền độc lập**, có thể được Human cấp **ngay tại G-PLAN bằng một câu rõ ràng**, không cần thêm một vòng hỏi.
- Phân biệt: “OK”, “đã xem”, “đồng ý ý tưởng” **không mặc nhiên cấp quyền code**. “OK, triển khai **task X theo plan vừa trình**” là quyền triển khai nếu task, phạm vi và kế hoạch xác định được.
- Một lần duyệt implementation cho phép Agent tự chủ sửa nhiều file **trong đúng task đã duyệt**, không hỏi từng file hoặc từng lần sửa test. Được tự sửa lỗi phát sinh từ phần thay đổi của mình và chạy lại test an toàn.
- Không tự mở rộng sang service/task khác, phá API consumer, đổi policy nghiệp vụ, thay quyền truy cập hoặc chuyển đổi công nghệ chỉ vì thuận tiện.
- `APPROVED: IMPLEMENT` **không** cấp quyền `git add/commit`, tạo nhánh/checkout, push/PR/merge, rebase/reset/cherry-pick/force-push, chạy DB update hoặc deploy. Nếu Human giao rõ riêng hành động nào, chỉ làm hành động đó.
- Giới hạn an toàn **không thể suy ra từ câu duyệt chung**: thao tác dữ liệu thật/production, migration phá dữ liệu, thanh toán thật, tiết lộ giấy tờ cá nhân/secrets, force-push hoặc thay đổi lịch sử nhánh dùng chung luôn cần xác nhận cụ thể; ưu tiên không thực hiện.

### 1.2. Cơ chế dừng, không làm phiền vụn vặt

Sau `IMPLEMENT_APPROVED`, Agent chủ động **Code → Test → Debug → Refactor nhỏ → Regression → Self-review**, không dừng hỏi ở mỗi phase. **Dừng và xin duyệt lại** khi:

- Yêu cầu mới mâu thuẫn AC/chính sách đã duyệt, hoặc đổi mô hình quyền `OWNER`, Auth, session, dữ liệu nhạy cảm hay tiền ngoài plan.
- Phát hiện breaking contract FE–BE/gRPC/event ảnh hưởng consumer, hoặc cần sửa service/task khác ngoài scope.
- Migration có nguy cơ mất dữ liệu; cần apply vào DB không phải môi trường test được cho phép; cần sửa seed/dữ liệu thật.
- Vướng lỗi test nền sẵn có, thiếu môi trường, rủi ro quá lớn hoặc không thể xác minh đúng đắn. **Báo blocker trung thực; không sửa assertion để ép xanh.**
- Sắp thực hiện Git action hay deploy chưa được cấp quyền cụ thể.

Với bug đơn giản và rõ scope: phân tích ngắn → đề xuất sửa/test → duyệt một lần → triển khai liền. Với business flow lớn: phân tích sâu, có test matrix và so sánh phương án. Không biến task 1 file thành thủ tục hành chính nhiều trang.

## 2. QUY TRÌNH PHÁT TRIỂN 7 PHA — 2 CỔNG HUMAN

```text
P1  KHẢO SÁT & BẰNG CHỨNG         (READ)
          ↓
P2  NGHIỆP VỤ, THIẾT KẾ & TRADE-OFFS  (READ)
          ↓
P3  TEST PLAN & CONTRACT CHECK    (READ)
          ↓
       [G-PLAN: HUMAN XEM/KHẲNG ĐỊNH PLAN]
          ↓ (Human cấp quyền IMPLEMENT rõ task/scope, có thể duyệt ngay tại G-PLAN)
P4  CODE & TDD KHI PHÙ HỢP        ┐
          ↓                      │ Agent tự chủ TRONG SCOPE
P5  BUILD / TEST / DEBUG         │
          ↓                      │ Không cần hỏi từng file
P6  SELF-REVIEW / GIT DIFF (READ)┘
          ↓
       [G-SHIP: HUMAN XEM DIFF/TEST/KẾT QUẢ]
          ↓ (Human cấp riêng quyền Git: COMMIT/PUSH/PR/MERGE)
P7  ĐÓNG GÓI & BÀN GIAO
```

**P1 — Khảo sát:** xem task/AC, source liên quan, branch/commit, schema, test, dependencies; phân loại `FACT`, `INFERENCE`, `PROPOSAL`, `NOT VERIFIED`.

**P2 — Phân tích/thiết kế:** truy vết request → authz → validation → application → domain → persistence → event/integration → response/FE. Xác định invariant và rủi ro; `KEEP` nếu code đúng. Chỉ so sánh 2–3 phương án khi có trade-off thật sự.

**P3 — Test/contract plan:** xác nhận request/response/status/errors/consumer; tối thiểu happy path và các negative/boundary/security/concurrency case **có liên quan**. Với task nhỏ chỉ cần checklist ngắn; **không bắt buộc** tạo `IMPLEMENTATION_PLAN.md` và nhiều file riêng.

**P4 — Implementation:** chỉ sau quyền code. Chọn TDD (RED–GREEN–REFACTOR) cho nghiệp vụ nhạy cảm/khó; với thay đổi đơn giản có thể viết test cùng lúc. Giữ Clean Architecture/DDD theo cấu trúc **đang tồn tại**, không thêm pattern/service/abstraction chỉ để “đúng chuẩn”.

**P5 — Verification:** build, unit, architecture, integration/contract/E2E theo scope và môi trường. Cho phép tự fix lỗi mới do mình gây ra; nếu test fail vì môi trường/ngoài scope, báo rõ thay vì sửa bừa. Không tuyên bố `100% PASS` khi chưa chạy đủ.

**P6 — Self-review:** đọc `git diff`, kiểm tra nghiệp vụ, transaction, authz, secrets, API compatibility, query dư, tests, regression, task scope. **Không tự rebase để làm lịch sử tuyến tính.** Đề xuất commit message tiếng Việt.

**P7 — Delivery:** báo file đã thay đổi, test thực chạy, giới hạn còn lại, cách demo, Git plan. Chỉ commit/push/PR/merge khi có quyền riêng. Không tự ghi Master Sheet Done chỉ vì build xanh.

## 3. PROMPT COMPILER — CHUYỂN LỜI NÓI THÀNH YÊU CẦU CHUẨN

Khi Human nói “kiểm tra BE-1”, “code API Chủ bãi”, “review Master Sheet”, Agent tự chuẩn hóa yêu cầu **trước khi thực hiện**, nhưng **không in prompt dài nếu người dùng không yêu cầu**.

1. **Intent:** `AUDIT | DESIGN | PLAN | TEST | IMPLEMENT | REFACTOR | COMPARE | DOC | REPLAN`.
2. **Objective:** kết quả cần có và cách biết đã đúng.
3. **Scope:** task/owner/service/branch, in-scope & out-of-scope; không đoán BE-1 là ai khi chưa kiểm tra.
4. **Sources:** Context/US/AC/API, Git/source/DB/FE consumer/tests/Master Sheet mới nhất; tài liệu thiếu ghi `NOT VERIFIED`.
5. **Business rules:** actor, inputs, trạng thái, quyền, invariant, failure/edge paths và acceptance criteria.
6. **Technical trace:** entry point → handler/domain/data/messaging; dữ liệu đầu vào/đầu ra, compatibility, side effects.
7. **Verification:** test case (precondition/input/expected/level), môi trường và giới hạn, các bằng chứng có thể thu được.
8. **Decision/Authority:** câu hỏi quan trọng, phương án, rủi ro, quyền đang có, cổng phải dừng.
9. **Output:** báo cáo gọn bằng tiếng Việt, code/diff khi được duyệt, không lặp và không suy đoán PASS.

**Mẫu prompt khi Human yêu cầu “viết prompt chi tiết”:**

```text
ROLE: Senior Engineer + Business Logic Reviewer + QA, hiểu Smart Parking.
MODE: ANALYZE_ONLY (trừ khi đã được cấp quyền IMPLEMENT rõ ràng).
OBJECTIVE: <mục tiêu, tiêu chí đo kết quả>.
SCOPE: <task, service, branch, role/actor, in/out scope>.
SOURCES: <docs, AC, API Design, Git/PR/HEAD, source, schema, FE, test>.
AS-IS: <điều đã chứng minh; điều NOT VERIFIED>.
BUSINESS RULES: <preconditions, state machine, invariants, RBAC, failure cases>.
METHOD: Inventory → Trace → Check contract → Challenge assumptions → Verify → Options.
IMPLEMENTATION PLAN: <các file/layer dự kiến, migration/contract impact nếu có>.
TESTS: <test ID, tình huống, input, expected, mức test, môi trường an toàn>.
RISKS / OPEN DECISIONS: <ảnh hưởng FE, data, security, dependencies>.
DELIVERABLES: <file:line, vấn đề P0/P1, KEEP/fix, kế hoạch, mức tin cậy>.
HITL: G-PLAN trước code; G-SHIP trước Git; không vượt phạm vi đã cấp.
LANGUAGE: Tiếng Việt rõ ràng, không dài dòng, message commit đơn giản.
```

Khi chỉ thiếu một thông tin then chốt: hỏi **một câu trực diện**. Nếu vẫn phân tích có điều kiện được thì nêu giả định, tiếp tục phần không bị chặn.

## 4. SKILL ROUTER — CHỌN ĐÚNG KỸ NĂNG, KHÔNG KÍCH HOẠT TẤT CẢ

| Skill | Dùng khi | Bằng chứng/đầu ra chính |
|---|---|---|
| `SK-01` Requirements & Business Rules | Form, API mới, trạng thái, hồ sơ OWNER | AC, state machine, decision points |
| `SK-02` Execution Flow Trace | Soi logic endpoint/handler | Source `file:line`, luồng data và fail paths |
| `SK-03` Contract Drift | FE–BE, API, gRPC, event mismatch | API Design vs code vs consumer, compatibility |
| `SK-04` DB & Concurrency | Booking slot cuối, token, wallet, trạng thái duyệt | Transaction/constraint/locks, race test |
| `SK-05` Authz/Security/Privacy | JWT, role/ownership, hồ sơ giấy tờ, tiền | Threat scenarios, policy/data-flow review |
| `SK-06` Test & Runtime Verification | Sửa code, tuyên bố Done, E2E | Test cases, PASS/FAIL/NOT RUN, limits |
| `SK-07` Git/PR Forensics | BE-1/branches/merge/revert | Task ↔ branch ↔ PR ↔ commit ↔ test |
| `SK-08` Optimization & Alternatives | Code dài, query chậm, refactor | KEEP/MINIMAL FIX/ALTERNATIVE, lợi ích có chứng cứ |
| `SK-09` Master Sheet & Agile | Sprint/backlog/owner/points/dependency | Audit workbook, Before/After không ghi đè |
| `SK-10` Mapbox & AI | Search/map/routing/Top 3 AI | Contract, timeout/cache/fallback, rate/cost |
| `SK-11` Owner Document Onboarding | Xin quyền OWNER, hồ sơ pháp lý, Admin review | Checklist tài liệu có điều kiện, RBAC, audit, tests |
| `SK-12` Booking/Payment E2E | Hold/payment/session/refund/IoT | Idempotency, outbox/inbox, failure matrix |

**Vòng tự phản biện áp dụng với issue quan trọng:**

`Yêu cầu/invariant → Bằng chứng → Giả thuyết lỗi → Tìm cách bác bỏ (middleware/DB constraint/worker/test khác) → Phản ví dụ → Kiểm chứng an toàn → Giải pháp → Độ tin cậy`.

Luôn tự hỏi: **Logic đã đúng chưa? Quyền có đúng resource không? Hai request đồng thời ra sao? Retry gây nhân đôi không? Có query/abstraction dư không? Có cách đơn giản hơn không?** Không báo lỗi giả, không ép mọi thay đổi thành “phải refactor”.

## 5. BUSINESS RULES SMART PARKING — NHỮNG ĐIỂM CẦN GIỮ

### 5.1. Owner onboarding: **có hồ sơ giấy tờ trước khi cấp OWNER**

**Quyết định yêu cầu hiện tại từ Human:** Một người **chỉ được cấp quyền Chủ bãi sau khi đã nộp đủ hồ sơ/giấy tờ áp dụng và được Admin xét duyệt**. Bản AGENTS cũ có mô hình Owner `Pending/Approved/Rejected` không yêu cầu upload; Agent phải coi đó là **hiện trạng cần kiểm tra**, không lén xem đó là đủ cho yêu cầu mới.

**Tách hai bước độc lập:**

1. **Identity / OwnerApplication:** đăng ký account & OTP → khai báo loại chủ thể (cá nhân/hộ kinh doanh/doanh nghiệp nếu được chấp thuận) → điền người liên hệ/đại diện → tải giấy tờ theo checklist → gửi duyệt → Admin xem/xác minh/yêu cầu bổ sung/từ chối/duyệt → **mới cấp quyền OWNER**. Không cho client tự gán `role` hay `owner_status`.
2. **Parking / ParkingLot:** sau khi có quyền OWNER → khai báo từng bãi (tên, địa chỉ, tọa độ, loại bãi, giờ hoạt động, zone/slot/capacity, pricing, booking policy nếu áp dụng) → nộp bằng chứng quyền khai thác địa điểm và giấy tờ vận hành theo trường hợp → Admin duyệt **từng bãi** → bãi mới được public/search/book.

**Phạm vi OJT:** Không bắt eKYC tự động, AI OCR/đối soát cơ sở dữ liệu nhà nước hay thanh toán xác minh giấy tờ thật; **Admin duyệt thủ công và tài liệu test giả lập là đủ để demo**, nhưng logic upload/đủ hồ sơ/không cấp role sớm phải có nếu đã đưa vào MVP scope. `Upload` không đồng nghĩa `Verified`.

**Danh sách giấy tờ:** checklist **có điều kiện** theo chủ thể, loại bãi, địa phương và quy định áp dụng. Giấy tờ danh tính/đăng ký kinh doanh/ủy quyền liên quan chủ thể; bằng chứng quyền khai thác, an toàn/PCCC và vận hành liên quan **từng bãi** khi áp dụng. Không tuyên bố một loại giấy tờ bắt buộc cho mọi trường hợp nếu chưa được BA/PO chốt chính sách pháp lý. Cần chốt chính sách trước thiết kế DTO/validation.

**Bảo vệ dữ liệu:** lưu file private; authz trên từng document; giới hạn file size/type và xác thực nội dung; quét/tách khu lưu trữ khi phù hợp; masked PII, không đưa CCCD/GPLX vào Git, log, public assets hay Excel; audit Admin decisions, reviewer, thời gian/lý do; dùng giấy tờ giả để test. Không tự expose đường dẫn bucket/file công khai.

**State machine:** xác minh enum/state hiện có trước; tối thiểu phân biệt `DRAFT`, `SUBMITTED/PENDING`, `NEEDS_CHANGES`, `APPROVED`, `REJECTED` về **nghiệp vụ**. Nếu code hiện chỉ có `Pending/Approved/Rejected`, đề xuất backward-compatible mapping/migration; **không tự đổi enum/API**. Phải kiểm tra duplicate/concurrent submit/review và refresh/revoke JWT claim sau approve.

**Test trọng yếu:** thiếu hồ sơ → không cho submit/approve; chưa duyệt → không có OWNER; thiếu quyền → 403; file người khác → cấm truy cập; trùng submit/admin concurrent; reject/resubmit; Owner được duyệt nhưng bãi chưa duyệt → không public/booking; document storage failure; FE status/contract. Không dùng tài liệu định danh thật.

### 5.2. Invariants/flow cần đối chiếu code & docs

- Guest không phải synthetic DB role; OTP lifecycle thuộc Identity.
- Parking session/SessionQR gắn đúng phiên; checkout cần đúng điều kiện nhận dạng, QR, trạng thái thanh toán theo policy đã duyệt.
- Booking giữ chỗ cuối cần concurrency; Payment/VNPay sandbox phải kiểm soát idempotency, callback/IPN, transaction/ledger; không chuyển tiền thật.
- Parking: ô tô có slot riêng, xe máy theo capacity zone (nếu được xác nhận trong thiết kế hiện hành); chủ bãi chỉ sửa lot thuộc quyền sở hữu; giá cần snapshot đúng thời điểm.
- Auth FE: đối chiếu `sp_refresh` HttpOnly cookie, `GET /me`, CSRF/CORS/SameSite/rotation/logout với implementation & FE; **HttpOnly không ngăn mọi XSS/CSRF**.
- Mapbox: đối chiếu ADR/API/FE; xác minh từng dịch vụ maps/geocoding/directions/matrix đã thật sự chốt hay chưa, không suy ra “mọi thứ đã chuyển xong”.

## 6. CODE QUALITY / TEST / ARCHITECTURE CHO OJT

- **Đúng nghiệp vụ > safety/data > tích hợp > test > bảo trì > performance vi mô > style.** Ưu tiên sửa P0/P1, tránh design pattern không mang giá trị.
- .NET microservices theo Clean Architecture/DDD/CQRS **chỉ trong mức hiện có**: Domain độc lập, Application business use cases, Persistence EF Core, Infrastructure messaging/security, WebAPI composition; không tạo cross-service reference/cross-schema writes nếu ranh giới thật xác nhận như vậy.
- EF Core/PostgreSQL: kiểm tra indexes/query, `AsNoTracking` khi đọc thuần, transaction, unique constraint, concurrency token **phù hợp PostgreSQL/EF (không mặc định bắt `RowVersion`)**; EF DbContext không thread-safe. Các thao tác có side effect cần rollback/idempotency phù hợp.
- Messaging: nếu repo đang dùng **custom transactional Outbox/Inbox với MassTransit RabbitMQ**, giữ tương thích. Không tự bật MassTransit EF Outbox/Inbox chỉ vì thấy package có sẵn; đọc `CLAUDE.md`, source và tests rồi quyết định. At-least-once delivery yêu cầu consumer idempotent.
- Async: không `.Result`/`.Wait()` ở luồng async; truyền CancellationToken thích hợp; không đòi async/parallel vô nghĩa.
- **Kiểm tra repo trước khi khẳng định gateway là YARP hay Ocelot, C# là 13 hay 14, SDK cụ thể, endpoint đã có hay chưa**, vì `AGENTS.md` và `CLAUDE.md` cung cấp snapshot khác nhau.
- **Lệnh test tham khảo (xác minh trước khi chạy):** `dotnet restore ParkingSystem.slnx`; `dotnet build ParkingSystem.slnx`; dự án dùng xUnit v3 + Microsoft.Testing.Platform thì có thể cần `dotnet test --project <test.csproj>`; integration Testcontainers cần Docker. Không coi số test đã ghi trong AGENTS cũ là kết quả hiện tại.
- **Không tự chạy `docker compose down -v`, `dotnet ef database update`, truncate/reset DB, migration destructive** nếu chưa biết môi trường và chưa được cấp quyền dữ liệu. Testcontainers chỉ dùng môi trường cô lập.
- **Tiêu chí test:** `STATIC REVIEW`, `UNIT VERIFIED`, `INTEGRATION VERIFIED`, `CONTRACT VERIFIED`, `E2E VERIFIED`, `NOT VERIFIED`; ghi rõ command, branch/SHA, PASS/FAIL/NOT RUN, Docker/env caveats. Không “100% PASS” nếu chưa có log; không đổi test để che bug.
- **Optimization:** ưu tiên đo hoặc phân tích hotspot cụ thể; nếu code hợp lý, `KEEP`. Khi cần chỉ đề xuất sửa tối thiểu trong phạm vi task.

## 7. GIT / MASTER SHEET / COMMIT TIẾNG VIỆT

- Audit an toàn: `git status --short`, `git branch -a -vv`, `git log --all --oneline --decorate`, `git diff`, `git show` và PR đã kiểm chứng. Không tự checkout/clean/reset/rebase/squash/cherry-pick hay sửa branch dùng chung.
- Sau code, **chỉ đề xuất** thứ tự commit và message; commit là quyền riêng, push/PR/merge là quyền riêng. Không ép linear history bằng rebase. Một commit nên gom **một thay đổi có ý nghĩa dễ review**, không bắt buộc tách mọi layer.
- Message bằng tiếng Việt đơn giản và prefix rõ scope, ví dụ:
  - `feat(identity): thêm đăng ký hồ sơ chủ bãi`
  - `test(identity): kiểm tra thiếu giấy tờ bắt buộc`
  - `fix(parking): chặn hiển thị bãi chưa duyệt`
  - `refactor(booking): đơn giản hóa kiểm tra sức chứa`
- Master Sheet: đọc cả hidden rows/filters, không làm mất Task ID/Role/Owner/Points. Không tự gán BE-1 hoặc Booking/Payment owner, không tự đổi Sprint/Points; đề xuất `Before → After` và chờ duyệt. `Done` chỉ có ý nghĩa tương ứng mức bằng chứng, không dựa vào Excel/PR merged đơn lẻ.
- Khi phát hiện một task nghẽn, phải trả lời được: **chờ ai, chờ cái gì, có thể làm song song phần nào, và bước unblock cụ thể**.

## 8. MẪU BÁO CÁO NGẮN & DEFINITION OF DONE

Với task thông thường, báo theo mẫu gọn; bỏ những mục không áp dụng:

```text
TASK / SCOPE / BRANCH / SHA: ...
MODE & AUTHORIZATION: ANALYZE_ONLY | PLAN_APPROVED | IMPLEMENT_APPROVED | GIT_APPROVED [action]
ĐÃ KIỂM TRA: tài liệu/đường chạy/file:line/test command thật sự sử dụng
KẾT LUẬN: CONFIRMED | LIKELY | NOT VERIFIED (tách BUG/RISK/PROPOSAL)
TOP ISSUES: vấn đề P0/P1, expected-vs-actual, ảnh hưởng người dùng/FE/BE
PHƯƠNG ÁN: KEEP | SỬA TỐI THIỂU | PHƯƠNG ÁN KHÁC nếu có lợi ích
TEST: PASS / FAIL / NOT RUN + mức test; không suy đoán E2E
CÒN NGHẼN: chờ ai/cái gì; có thể làm tiếp phần nào
THAY ĐỔI: file/diff; tác động contract/DB/FE nếu có
QUYẾT ĐỊNH CẦN HUMAN: plan/code/Git action cụ thể, nếu cần
BƯỚC TIẾP: 1–3 hành động quan trọng nhất
```

`DONE VERIFIED` chỉ khi AC trong scope đạt, code đã kiểm tra, test cần thiết chạy và kết quả được ghi, contract/consumer quan trọng không lệch, thay đổi được tích hợp ở branch/môi trường cần nghiệm thu (nếu task yêu cầu). Nếu thiếu integration/E2E, ghi `DONE WITH GAPS` hoặc `NOT VERIFIED`, không tô xanh giả.

**Human chỉ cần 2 lần xem xét cho một task lớn thông thường:** duyệt plan + code scope tại **G-PLAN**, rồi duyệt kết quả/Git tại **G-SHIP**. Agent thực thi chủ động giữa hai lần đó, nhưng **không tự cấp quyền cho chính mình**.

## 9. NHỮNG ĐIỂM CẦN XÁC NHẬN KHI ĐƯA VÀO REPO

- [ ] Human duyệt `AGENTS.md` này và đồng ý hai cổng G-PLAN/G-SHIP cùng ba quyền PLAN/IMPLEMENT/GIT.
- [ ] Đối chiếu `CLAUDE.md` theo HEAD mới nhất; xác minh gateway YARP/Ocelot, .NET/C#, xUnit command, consumer/outbox và docs đã cập nhật.
- [ ] Chốt chính sách hồ sơ Chủ bãi: những loại chủ thể cho phép, checklist từng trường hợp và Admin verification; cập nhật User Stories, API Design, backlog sau khi duyệt.
- [ ] Kiểm tra API/FE đã có gì; không tạo duplicate endpoint/migration/task.
- [ ] Không copy các câu “APPROVED”, số test PASS hoặc trạng thái Phase 1 từ file hướng dẫn cũ sang báo cáo hiện tại mà chưa kiểm chứng code/test tại commit đang review.

**Thông điệp cho Agent:** *Đọc kỹ, tìm chứng cứ, tự phản biện, đưa plan rõ, xin duyệt một lần đúng phạm vi, tự code & test gọn và tốt, báo kết quả trung thực, chờ quyền Git riêng. Mục tiêu là Smart Parking OJT chạy đúng luồng — không phải tạo thêm thủ tục.*
