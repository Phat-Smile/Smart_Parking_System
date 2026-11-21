# US-AI-004: local vehicle-type image baseline

**Purpose:** Train, validate, and run a small **vehicle-type** classifier on locally labeled, *cropped vehicle images*. This is an **offline research baseline**, **not** license-plate OCR, real-time camera inference, a production gate decision, or proof of accuracy on real camera feeds. This directory does **not** edit Lộc's AI service, GateReadings, Recommendations, sensor/Pricing, or frontend.

## Dependencies (training/inference only)

Python 3.10+; training: `numpy>=1.24`, `Pillow>=10`, `scikit-learn>=1.3`; inference: `numpy`, `Pillow`. Dependency installation is deliberately **not** part of OneClick or automatic deployment. For a dedicated local environment, if desired: `python -m pip install -r tools/be2-vehicle-type/requirements.txt`.

CPU safety: `OMP_NUM_THREADS=1 OPENBLAS_NUM_THREADS=1 MKL_NUM_THREADS=1`. Training accepts at most 2,000 images; no Docker, GPU, HTTP, camera device, image uploads, external weights, or API calls.

## Prepare labeled data (private, outside Git repository)

Create a local directory with images that contain a **cropped vehicle**, e.g. `D:/private-vehicle-data/images`. Put `train.jsonl` **outside the repository**, with one row per image:

```json
{"id":"anon-0001","image":"car/0001.jpg","vehicle_type":"car","split":"train","group":"day"}
{"id":"anon-0002","image":"motorbike/0002.jpg","vehicle_type":"motorbike","split":"validation","group":"night"}
{"id":"anon-0003","image":"car/0003.jpg","vehicle_type":"car","split":"test","group":"day"}
```

`id` must be anonymous, **never a plate, email, customer or camera ID**. `image` is a path relative to `--dataset-root`. All classes must have at least two items in **each of train/validation/test** (this is only a minimum software sanity check, not sufficient sample size for credible evaluation). Use class labels that match the project's agreed vehicle taxonomy. The code does not guess taxonomy from license plate patterns.

Important: reserve `test` as a holdout, do not tune model or threshold on it; split by **vehicle/recording session**, not randomly by adjacent video frames. Code rejects identical decoded image pixels, but cannot catch near-duplicate frames or identity leakage. Provide day/night/rain/site groups when practical. Consent, retention, and data access are the data-owner's responsibilities. Do **not** commit `.jsonl` data, images, generated `.npz` weights, or private predictions.

## Commands, explicitly run only on a suitable machine

In the checked-out repository root:

```bash
# Lightweight code tests; optionally skip model training test with -k BasicChecks
OMP_NUM_THREADS=1 OPENBLAS_NUM_THREADS=1 MKL_NUM_THREADS=1 python -m unittest discover -s tools/be2-vehicle-type -p 'test_*.py'

# Validate local images and split leakage without training
python tools/be2-vehicle-type/vehicle_type.py audit \
  --dataset-root 'D:/private-vehicle-data/images' \
  --manifest 'D:/private-vehicle-data/annotations.jsonl' \
  --report 'D:/private-vehicle-data/audit.json'

# Calibrate confidence cutoff on validation, evaluate test once
python tools/be2-vehicle-type/vehicle_type.py train \
  --dataset-root 'D:/private-vehicle-data/images' \
  --manifest 'D:/private-vehicle-data/annotations.jsonl' \
  --model 'D:/private-vehicle-data/vehicle-type-baseline.npz' \
  --report 'D:/private-vehicle-data/vehicle-type-report.json' \
  --min-accepted-accuracy 0.90 --min-validation-accepted 10

# Local private predictions for downstream manual review, not for a barrier
python tools/be2-vehicle-type/vehicle_type.py predict \
  --dataset-root 'D:/private-vehicle-data/images' \
  --manifest 'D:/private-vehicle-data/predict-anonymous.jsonl' \
  --model 'D:/private-vehicle-data/vehicle-type-baseline.npz' \
  --output 'D:/private-vehicle-data/private-predictions.jsonl'
```

`predict` manifest rows have only `{ "id": "anon-...", "image": "..." }`. Predictions contain anonymous ID, vehicle type or `null`, confidence, and `requires_manual_review`. Nothing opens a barrier or creates a parking session.

The numeric `.npz` format is checked and loaded with `allow_pickle=False`; **only load local models you trust**. A missing safe cutoff returns code 3, writes an aggregate report, and **does not produce a deployable model**.

## What is verified and what is not

- 12 unit/integration tests passed in the coding environment, including actual local synthetic image training and inference. **This is a functional pipeline check only.**
- The synthetic holdout metrics do not represent real parking-camera accuracy, nor ANPR plate recognition.
- Genuine US-AI-004 acceptance still needs a labeled camera dataset, model evaluation by vehicle type and environment, Lead-approved minimum accuracy/coverage and evidence of manual review, ideally a test on field footage. No actual camera data/model artifact is provided in this repo.

All saved reports are aggregate-only and exclude filenames, paths, image content, personal plates and sample IDs. The `predict` command intentionally writes private per-case output **outside Git**, for local matching only.

## Lightweight Node manifest metadata preflight (no Python dependencies)

```bash
node --test --test-reporter=tap --test-concurrency=1 tools/be2-vehicle-type/manifest-check.test.mjs
node tools/be2-vehicle-type/manifest-check.mjs --input 'D:/private-vehicle-data/annotations.jsonl' --root 'D:/private-vehicle-data/images' --output 'D:/private-vehicle-data/metadata-report.json'
```

Node preflight checks encoded image duplicates and balanced classes/splits, but **does not decode images**; run the Python `audit` command before training to detect equal decoded pixels. The one-click source delivery runs **only 12 tiny Node tests**, not image model training.
