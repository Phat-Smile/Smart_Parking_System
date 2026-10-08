# Requirements

Official requirement documents of the Smart Parking System, versioned with the code so a PR can cite
an id (for example "MF-08, BR-03.c, US-PARK-003") and reviewers can open the exact text.

| File | Content | Ids defined |
|---|---|---|
| `context-smart-parking.docx` | Context / SRS: scope, actors, glossary, business rules, NFRs, architecture constraints and missing service contracts, requirement matrix, defaults, open decisions | `FR-<GROUP>-nnn`, `BR-nn.x`, `NFR-nn` |
| `user-stories-smart-parking.docx` | 17 epics, 94 stories with Given / When / Then acceptance criteria | `US-<EPIC>-nnn` |
| `main-flow-smart-parking.docx` | Main flows MF-01 … MF-15 (MF-09 split A/B), state tables, traceability | `MF-nn` |
| `phu-luc-context-smart-parking.docx` | Appendix: legal references, sprint plan, test strategy (reference only) | — |

## Rules

- Precedence when documents disagree: **Context → User Stories → Main Flow**. The appendix is not a source of requirements.
- Every document is **CANDIDATE — NOT BASELINE**. Requirement status goes Proposed → Approved → Baseline; only Baseline (mentor-approved, frozen after Sprint 2) is committed scope.
- Tags keep their meaning: `[ĐÃ CHỐT]` decided, `[OPEN DECISION]` waiting for team/mentor, `[MISSING SPECIFICATION]` not defined yet — code must not silently decide these.
- Change a rule in Context first, then update the stories and flows that cite it. Commit the new `.docx` with a message naming the ids that changed, e.g. `docs(requirements): close open decision 34 in context`.
- Story assignment, sprint and progress live in the team master sheet, not here.
