# BE-2 complete audit tools — offline and CPU-bounded

The owner-specific roadmap is in `docs/be2/BE2_CLOSEOUT_ACTION_REGISTER.md`.

- `audit.mjs`: load 23-task/80-SP catalog, recheck 16 unsettled files via **local Git read-only**, write `BE2_CLOSEOUT.md` and `.json`. It does NOT fetch, edit source, assume Jenkins passed or promote historical `Xong` statuses.
- `dataset-audit.mjs`: local anonymous training-manifest split guard for US-AI-004. It checks exact and near-duplicate fingerprints and prevents a vehicle or recording session from crossing train/validation/test. No image file is read or sent; no private hashes/IDs/paths are included in reports.
- Node tests: `node --test --test-reporter=tap --test-concurrency=1 tools/be2-closeout/*.test.mjs`.

Run read-only Git audit: `node tools/be2-closeout/audit.mjs --repo 'D:/OJT26/Smart_Parking_System' --out 'D:/private/be2-audit'`.

For real camera data split audit, prepare an **external private** JSONL manifest (not in Git) with one row per labeled cropped image:

```json
{
  "id": "anon-001",
  "image": "car/001.jpg",
  "vehicle_type": "car",
  "split": "train",
  "vehicle_identity_hash": "1234567890abcdef",
  "capture_session_hash": "abcdef1234567890",
  "image_sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
  "perceptual_hash": "0123456789abcdef"
}
```

Hashes above are syntax-only examples, NOT real fingerprints. `vehicle_identity_hash` and `capture_session_hash` must come from privacy-safe stable group pseudonyms produced **locally** by the data custodian, and each must stay in only one split. `image_sha256` should be the raw file hash; `perceptual_hash` must be a true 64-bit perceptual hash computed by a trusted local image tool, not a random number. The dataset audit validates provided hashes **but cannot establish their authenticity**. It never reads images; the previous Python baseline `vehicle_type.py audit` remains responsible for decoded-pixel duplicate checks.

```bash
node tools/be2-closeout/dataset-audit.mjs --input 'D:/private/vehicle-manifest.jsonl' --output 'D:/private/split-report.json'
```

**Do not include** plates, customer IDs, private data, image files or per-sample prediction logs in Git or in the BE2_UPLOAD_THIS.zip; one-click does not scan private camera data by default. This is a precondition for model evaluation, not proof of accuracy. A `PASS_SPLIT_GUARD_ONLY` never means the model is good enough for gate automation.
