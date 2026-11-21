# BE-2 closeout without duplicated ownership — 21 November 2026

## Sources and truth boundaries

1. `BE_V4 (1).xlsx`, sheet `Backlog`, assigned BE-2: **23 tasks / 80 SP**, including **one 8-SP LightGBM task after OJT**. During OJT: **22 tasks / 72 SP**. The original Excel says 15 Done, one Waiting for review, and seven Not started; these are **historical records**, not current Jenkins acceptance.
2. `context.docx` and `SRS.docx`: task/FR acceptance framework. Task catalog extracted to `tools/be2-evidence/be2-backlog-20261120.json`; it records original row number and an excerpt of Context for the user stories where available.
3. `phan-cong-tranh-trung.md` and `tong-ket-tuan-16-20-11.html`, 20 November team report: **ownership rules are authoritative for avoiding duplicate work** until the lead changes them.
4. BE-2 integrated branch `features/BE2_integrated_20261120`: previous push/merge `16bb7bcc`, 52 changed files vs historical `b40c6830`; a read-only scope audit found 36 owned, 15 shared, 1 unassigned document. This is a **past log**, not proof of today’s GitHub PR state.

**Do not squash-merge or cherry-pick the old 52-file integration PR wholesale.** Do not force-push it or rewrite a shared branch.

## Work kept with existing owners — no duplicate implementation

| Work                                                                                                                                     | Owner / context              | Our action                                                                                                                                                                                         |
| ---------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 4 split PRs from #329: AI insufficient-data ranking, gate arrival override, slot sensor + pricing + GPS no-cache, customer GPS dashboard | Lộc, using Khoa’s prior code | Do not touch `ai/ai-service/`, GateReadings, ProcessSlotSensorEvent, Recommendations, pricing/privacy endpoints, or customer GPS page. Compare exact Git blobs before suggesting any missing part. |
| #332 design tokens, Vietnamese UI, layout                                                                                                | Lộc                          | No changes to shared layout/tokens/icons.                                                                                                                                                          |
| #333 public QR fees, emails, staff listing                                                                                               | Lộc                          | No `contracts/`, Payment mail/fee, Notification, or Parking ListLotAssignments changes.                                                                                                            |
| #334 reminders, countdown ticket, reserved slot visibility                                                                               | Lộc                          | No Booking/Notification migration or StaffEntry/CustomerSessions/BookingPass pages.                                                                                                                |
| Refund approval/wallet credit                                                                                                            | Bách                         | No Payment RefundRequests or Payment migrations.                                                                                                                                                   |
| Owner staff and zone editor                                                                                                              | Đình Anh                     | No owner staff/editor frontend paths.                                                                                                                                                              |
| Customer search split, AI panels, voice search, demo stack and Business E2E                                                              | Lộc                          | No customer search/panel/voice changes; no duplicate business E2E or shared QA scripts.                                                                                                            |

The **AI/OSRM/IoT/GPS/Gate/Pricing source from prior BE-2 batches remains available as evidence**, but is not another task to code or merge until Lộc confirms exactly what his split PRs include.

## Work BE-2 can take now without editing owner files

| Track                                                                        | Specific outcome                                                                                         | Implementation in this delivery                                                                               | Acceptance before DoD                                                                                                                                                                       |
| ---------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **FR traceability** (no named owner in team report)                          | Match BE-2 23 tasks to `Context`/Excel, candidate files, Jenkins evidence and current split branches     | Backlog manifest + offline FR report generator; no production source edits                                    | Verify each row against current `develop`, attach PR/test evidence, close gaps only when owner approves. Merely finding a file cannot mark Done.                                            |
| **PR dedup verification** (coordination)                                     | Compare original integrated 52-file PR against 4 split PRs at **actual Git blob level**, not paths alone | Read-only `reconcile.mjs`; classifies exact/changed/unavailable refs and feature-only merge commits           | Lộc reviews mismatches; only truly missing code can be proposed in a separate clean PR. Missing branch refs must be BLOCKED, not treated as non-overlap.                                    |
| **US-AI-004: ANPR/vehicle-type evaluation** (not claimed in 20/11 task list) | Repeatable offline accuracy/coverage/abstention report with no raw identifiers                           | `anpr-evaluate.mjs` + synthetic fixtures and regression tests; under isolated `tools/be2-evidence/` directory | Need real labeled camera dataset, model inference output, lighting/site/vehicle breakdown, agreed thresholds and human-review criteria. No model training or camera acceptance claimed now. |
| **TECH-14 supporting evidence** (shared, coordinate with Lộc)                | Tiny local QA read-only latency sample; no load on user machine by default                               | `qa-perf.mjs`, one request at a time, QA ports only, max 8 samples                                            | Real NFR-02 realtime <2s and NFR-04 AI <1s require agreed workload, concurrency and Jenkins/runtime environment. Tiny sample never qualifies as NFR PASS.                                   |

## Additional open tasks in the weekly report — **request owner approval before coding**

1. Staff view: minutes late for scheduled arrival and whether reservation was canceled for no-show. This crosses Booking and StaffEntry, which are in #334; **do not independently implement before Lộc/BE-3 approve the boundary**.
2. Bind exit-barrier command to the actual camera reading at exit. Crosses Parking Gate/StaffExit and BE-4 domain; **do not modify gate code without BE-4 and Lộc agreement**.
3. Context FR matrix across all services: use the read-only audit to identify candidate gaps; escalate to owner rather than changing Payment/Identity/Booking directly.

## Completion plan and merge gates

**Phase A (this delivery):** Create a clean branch from `origin/develop` with _only new, previously unowned QA/evidence files_. Run built-in Node tests sequentially (no .NET, containers, migrations). Generate FR report, compare four split refs if locally fetched, run synthetic ANPR evaluation, write ZIP logs. English commit and PR title: `test(be2): add independent requirement and anpr evaluation tools`.

**Phase B (coordination):** Send exact Git comparison report to Lộc; ask which #329 pieces are already in his four split PRs, and assign owner for the two cross-service gaps. Keep integrated PR closed/held; do not blindly rebase/squash it.

**Phase C (acceptance):** After owner confirmation and real camera predictions, run offline ANPR evaluation on labeled data. Apply agreed minimum coverage and accuracy gates. Ask Lộc to run Business E2E; collect Jenkins green and TECH-14 actual load evidence. Reject statuses based on Supabase Preview skipping.

**Phase D (merge):** Only independent, non-overlapping PRs on `features/Implementation_<Name>` branches. After new develop commits: `git fetch` and carefully `git rebase origin/develop` on dedicated branch (no merge commits; never force-update teammate branches). PR requires Jenkins green, review, squash merge. Do not handwrite EF migrations.

## CPU / safety

- Node test concurrency **1** and no Docker, DB, .NET or AI inference in this delivery.
- Do not write raw camera images, license plates, passwords or access tokens to any committed report.
- New branch/worktree only; never touch Gate/old integrated branch or local user's uncommitted files.
- The scope audit is conservative: **unknown ownership stays review-only**.

## Stop/go for today's delivery

- **Ready to ship a new tool PR:** 31 Node unit tests green locally, all new files under `tools/be2-evidence/` or `docs/be2/`, no source overlap.
- **Not ready for full BE-2 Done:** actual GitHub split PRs and Jenkins not read here, vehicle classifier/model evaluation dataset absent, TECH-14 load and Lộc's E2E still pending.
