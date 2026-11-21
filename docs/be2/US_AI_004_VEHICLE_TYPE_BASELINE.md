# BE-2 US-AI-004 — independent offline vehicle-type baseline

Date: 2026-11-21. Scope: **vehicle type from a cropped image**, not plate OCR, staff UI, GateReadings, Recommendation, Pricing, or live camera deployment.

## Team ownership guard

The 20 November task division assigns the #329 split work in AI, Gate arrival, IoT/Pricing/GPS and customer dashboard to Lộc, along with E2E/demo. Do **not** change these sources or resubmit the 36 Git-exact files. This delivery adds only new files under `tools/be2-vehicle-type/` and this document. It has no migration or external API changes.

## Delivered source

- `vehicle_type.py`: local manifest validation, resized grayscale/gradient feature extraction, CPU-limited logistic regression training, validation-only confidence-cutoff selection, locked test-set scoring, manual-review fallback and numeric-only model export/inference.
- `test_vehicle_type.py`: tests for image path containment, input validation, duplicates/leakage, confidence gating, safe model loading, training, prediction and no model export when validation cannot meet the policy.
- `requirements.txt` + README: optional local training dependencies, data ownership/privacy safeguards, command examples. Auto Git Bash deployment never downloads dependencies, trains on camera images or starts Docker.

## Proof and limitations

- Coding environment: 12/12 Python tests passed; synthetic 40-image demonstration ran local training, validation, test and inference. Includes an intentionally impossible validation gate to confirm it blocks model export.
- Data splitting is *manifest driven*, and the code only detects **exact decoded-image duplicates**. Do not interpret it as prevention of near-duplicate video-frame or vehicle-identity leakage.
- No real camera footage or inference metrics were supplied. **No proof of field accuracy, real-time latency or vehicle-type acceptance.** No model is packaged in this PR.
- A model cannot be used for autonomous gate operation on this evidence. Only authenticated staff/manual review should resolve low-confidence cases in the product's existing flow.

## Next acceptance inputs (not code owned by Lộc)

1. Approved vehicle-type labels, permissioned/annotated images from different camera angles and lighting, with train/validation/test separated by vehicle and recording session.
2. Lead-approved confidence/coverage/accuracy criteria. The `0.90` in the README is an **example**, not an agreed requirement.
3. Run preflight/audit, train once, measure test + slice metrics, record sample counts and manual-review rate. No private source filenames or license plates in PR.
4. Any integration with Gate, AI service, or frontend must be assigned and reviewed with existing owners before editing their files.

## Existing BE-2 evidence

- Earlier Phase 1: `features/Implementation_Be2EvidenceAndAnprEvaluation` (FR traceability, exact Git dedup, ANPR text evaluation, lightweight NFR evidence).
- This new feature does not reintroduce the old 52-file BE-2 integration PR or the four PRs split from #329.

- `manifest-check.mjs` + `.test.mjs`: no-dependency Node preflight for anonymous IDs, path containment, class/split balance, file existence and exact encoded-image duplication; 12 additional Node tests. This does **not** replace decoded-pixel validation in Python.
