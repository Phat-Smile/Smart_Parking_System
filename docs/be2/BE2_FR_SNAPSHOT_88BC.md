# BE-2 FR candidate audit — original source 88bc3069 only

**This is an historical source ZIP, not current `develop` or CI evidence.**

|Task|SP|Status in Excel|FR|Candidate files in 88bc3069|
|---|---:|---|---|---|
|TECH-02|3|Xong|—|manual review|
|TECH-03|2|Xong|—|manual review|
|US-SEARCH-001|2|Xong|FR-PARK-001|manual review|
|US-SEARCH-005|2|Xong|FR-PARK-001|manual review|
|US-ADMIN-002|2|Xong|FR-ADMIN-003|manual review|
|US-OWNER-001|3|Xong|FR-OWNER-001|manual review|
|US-OWNER-002|2|Xong|FR-OWNER-001|manual review|
|US-SEARCH-002|3|Xong|FR-PARK-004|manual review|
|US-SEARCH-003|3|Xong|FR-MAP-001|manual review|
|US-SEARCH-004|1|Xong|FR-PARK-007|manual review|
|US-OWNER-007|5|Xong|FR-OWNER-002|manual review|
|US-MAP-001|3|Xong|FR-PARK-002|manual review|
|US-MAP-002|2|Xong|FR-PARK-002|manual review|
|US-MAP-004|3|Xong|FR-PARK-007|manual review|
|US-MAP-006|3|Xong|FR-AI-005|manual review|
|US-SEARCH-006|5|Chưa làm|FR-MAP-002|`src/Services/Parking/Parking.WebAPI/Routing/OsrmMatrixClient.cs`: present|
|US-AI-002|5|Chờ review|FR-AI-002|`ai/ai-service/app/api/arrival_availability.py`: present|
|US-AI-001|8|Chưa làm|FR-AI-001|`ai/ai-service/app/api/recommendations.py`: present|
|US-PRIV-001|1|Chưa làm|FR-PRIV-001|`web/src/features/customer/CustomerDashboardPage.tsx`: present|
|US-AI-004|8|Chưa làm|FR-AI-007|`web/src/features/staff/anpr/plateOcr.ts`: present; `web/src/features/staff/anpr/plateMatcher.ts`: present|
|TECH-14|3|Chưa làm|—|`tests/performance/be2-qa-perf.mjs`: not in this snapshot|
|TECH-15|3|Chưa làm|—|`tests/e2e/be2-business-qa.mjs`: not in this snapshot|
|US-AI-003|8|Chưa làm|FR-AI-006|`ai/ai-service/`: present|

A file listed as present is **not** evidence that its acceptance criteria have passed. For the current branch run `fr-audit.mjs` and check Jenkins / PR evidence manually.
