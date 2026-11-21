# BE-2 independent evidence / evaluation tools

These files are **non-production tooling**. They do not change AI, Gate, Booking, Payment, MQTT or database code.

## Run locally (Node 20+; no packages to install)

```bash
node --test --test-concurrency=1 tools/be2-evidence/*.test.mjs
node tools/be2-evidence/fr-audit.mjs . BE2_OUTPUT/evidence
node tools/be2-evidence/reconcile.mjs . BE2_OUTPUT/evidence
node tools/be2-evidence/anpr-evaluate.mjs --input tools/be2-evidence/sample.synthetic.jsonl --output BE2_OUTPUT/evidence/synthetic-anpr.json
```

`reconcile.mjs` needs local remote-tracking refs for origin/develop, origin/features/BE2_integrated_20261120 and the four #329 split branches. It does **not** fetch, edit files or declare unrepresented branches absent; a missing ref blocks an exact comparison.

To evaluate real camera results, produce **local-only JSONL** (not committed) with one line per labeled case:

```json
{"id":"case-001","ground_truth":{"plate":"TEST001","vehicle_type":"car"},"prediction":{"plate":"TEST001","vehicle_type":"car","confidence":0.95},"group":"day"}
```

Example identifiers are fake. Do not commit plate labels or images. Vehicle types are compared as exact case-insensitive labels; no automatic vehicle-type guesses or license plate substitutions. Empty predictions are abstentions, reducing coverage. Groups with <5 samples are withheld to avoid small-group disclosures. Output contains *only aggregates*, not raw plates/IDs.

If the lead gives real thresholds, supply all three at once:

```bash
node tools/be2-evidence/anpr-evaluate.mjs --input local-private.jsonl --output BE2_OUTPUT/anpr-metrics.json --min-samples 100 --min-coverage 0.8 --min-joint-accuracy 0.9
```

The shown thresholds are illustrative **not official requirements**. Without all three, status is `EVIDENCE_ONLY`, not `PASS`.

Optionally sample isolated QA endpoints (sequential 4 requests; no authentication, no body collected):

```bash
node tools/be2-evidence/qa-perf.mjs --url http://127.0.0.1:25002/health/ready --output BE2_OUTPUT/qa-perf.json
```

This is **not** a load test nor proof that NFR p95 thresholds were met. The script intentionally allows only known isolated QA ports and read-only paths. It does not start any service.
