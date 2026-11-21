# BE-2 — 23-task acceptance matrix (21/11/2026)

**Historical Excel is not release acceptance.** 22 tasks/72 SP OJT; one LightGBM post-OJT/8 SP. No row below is marked Done without current evidence.

| ID | SP | Sprint | FR | Historical status | Owner now | Decision | Exit evidence |
|---|---:|---|---|---|---|---|---|
| TECH-02 | 3 | S1 | — | Xong | BE-2 / review | VERIFY_ONLY | Catalog fixture version, seed docs, test evidence |
| TECH-03 | 2 | S1 | — | Xong | BE-2 / review | VERIFY_ONLY | Adapter file + rate-limit/fallback tests + PR |
| US-SEARCH-001 | 2 | S1 | FR-PARK-001 | Xong | BE-2 / review | VERIFY_ONLY | HTTP guest test + current develop SHA |
| US-SEARCH-005 | 2 | S1 | FR-PARK-001 | Xong | BE-2 / review | VERIFY_ONLY | HTTP guest lot-detail test + current develop SHA |
| US-ADMIN-002 | 2 | S2 | FR-ADMIN-003 | Xong | BE-2 / review | VERIFY_ONLY | Admin API tests and role checks |
| US-OWNER-001 | 3 | S2 | FR-OWNER-001 | Xong | BE-2 / review | VERIFY_ONLY | Owner API tests and current file evidence |
| US-OWNER-002 | 2 | S2 | FR-OWNER-001 | Xong | BE-2 / review | VERIFY_ONLY | Opening hours API and date boundary tests |
| US-SEARCH-002 | 3 | S2 | FR-PARK-004 | Xong | BE-2 / Lộc UI | VERIFY_ONLY | Guest search behavior + consent evidence |
| US-SEARCH-003 | 3 | S2 | FR-MAP-001 | Xong | BE-2 / review | VERIFY_ONLY | Geocoding/mock tests and API evidence |
| US-SEARCH-004 | 1 | S2 | FR-PARK-007 | Xong | BE-2 / review | VERIFY_ONLY | Filter contract and search API tests |
| US-OWNER-007 | 5 | S2 | FR-OWNER-002 | Xong | BE-2 / Đình Anh UI | DELEGATED_VERIFY | Owner API test and UI owner confirmation |
| US-MAP-001 | 3 | S3 | FR-PARK-002 | Xong | BE-2 / Đình Anh UI | DELEGATED_VERIFY | Frontend render evidence |
| US-MAP-002 | 2 | S3 | FR-PARK-002 | Xong | BE-2 / Đình Anh UI | DELEGATED_VERIFY | Frontend render evidence |
| US-MAP-004 | 3 | S3 | FR-PARK-007 | Xong | BE-2 / Lộc slot PR | DELEGATED_VERIFY | Sensor-to-usage tests |
| US-MAP-006 | 3 | S3 | FR-AI-005 | Xong | BE-2 / Lộc slot PR | DELEGATED_VERIFY | Simulator tests and event replay evidence |
| US-SEARCH-006 | 5 | S4 | FR-MAP-002 | Chưa làm | BE-2 / Lộc #329 | DIFF_REVIEW | Split-ref blob+behavior diff and owner signoff |
| US-AI-002 | 5 | S3 | FR-AI-002 | Chờ review | BE-2 / Lộc #329 | DIFF_REVIEW | Integration test + quota evidence + owner signoff |
| US-AI-001 | 8 | S4 | FR-AI-001 | Chưa làm | Lộc #329 | OWNED_ELSEWHERE | Split PR accepted + AI QA |
| US-PRIV-001 | 1 | S5 | FR-PRIV-001 | Chưa làm | Lộc #329 | OWNED_ELSEWHERE | Browser consent/manual check and PR review |
| US-AI-004 | 8 | S5 | FR-AI-007 | Chưa làm | BE-2 independent baseline; Lộc owns OCR UI | ACTIVE_DATA_BLOCKED | Labeled holdout split + class/site/night breakdown + agreed accuracy/coverage + manual review |
| TECH-14 | 3 | S6 | — | Chưa làm | BE-2 evidence / Lộc QA | COORDINATE_NFR | NFR-02 realtime p95 <2s and NFR-04 AI p95 <1s measured under approved load |
| TECH-15 | 3 | S6 | — | Chưa làm | Lộc demo/E2E | OWNED_ELSEWHERE | MF-01..13 real journey trace, settlement and gates, no fake data |
| US-AI-003 | 8 | Sau OJT | FR-AI-006 | Chưa làm | BE-2 after OJT | POST_OJT | Held-out dataset and reproducible comparison |

## How to close a row

1. Verify the correct current `origin/develop` and PR branch SHA.
2. Verify owner/Lead acceptance; historical `Xong` in 2026-10 is not enough.
3. Attach one relevant contract/functional test and its commit SHA, not a screenshot of a skipped job.
4. Cross-service tasks require the owning service’s approval before code changes.
5. US-AI-004 needs labeled real camera holdout, per-group metrics and manual-review gate.
6. TECH-14 needs p95 under approved load on external QA; TECH-15 belongs to Lộc’s actual E2E.
7. BE-5 owns changes to progressive charging and wallet settlement; leave pricing formula untouched.
8. Do not merge old 52-file integration PR or any owner-controlled files from #329 wholesale.
