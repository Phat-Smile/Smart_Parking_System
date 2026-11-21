#!/usr/bin/env python3
"""CPU-bounded, offline vehicle-type image baseline for ParkWise US-AI-004.

No network. No data transfer. No raw image/path/sample ID emitted in aggregate reports.
Training requires Pillow, numpy and scikit-learn; inference only Pillow + numpy.
Model .npz is numeric-only (allow_pickle=False), never loads arbitrary Python objects.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import sys

MAX_MANIFEST_BYTES = 2 * 1024 * 1024
MAX_SAMPLES = 2000
MAX_IMAGE_BYTES = 8 * 1024 * 1024
MAX_IMAGE_PIXELS = 12_000_000
LABEL_RE = re.compile(r"^[a-z][a-z0-9_-]{1,31}$")
SAFE_ID_RE = re.compile(r"^[A-Za-z0-9_-]{1,80}$")
SPLITS = ("train", "validation", "test")
FEATURE_DIM = 640  # 16x16 greyscale + 6x6x(8+2) color/edge histogram bins (see feature_vector)
MODEL_VERSION = 1


class DatasetError(ValueError):
    pass


def safe_location(root: Path, value: str) -> Path:
    """Resolve only locally named images beneath root; reject symlink escapes."""
    if not isinstance(value, str) or not value or "\x00" in value or "\\" in value:
        raise DatasetError("image must be a nonempty, forward-slash relative path")
    candidate = Path(value)
    if candidate.is_absolute() or ".." in candidate.parts or any(x in ("", ".") for x in candidate.parts):
        raise DatasetError("image path must be strictly relative")
    base = root.resolve(strict=True)
    full = (base / candidate).resolve(strict=True)
    try:
        full.relative_to(base)
    except ValueError as exc:
        raise DatasetError("image path escapes dataset root") from exc
    if full.suffix.lower() not in (".jpg", ".jpeg", ".png", ".webp"):
        raise DatasetError("unsupported image extension")
    if not full.is_file() or full.stat().st_size > MAX_IMAGE_BYTES:
        raise DatasetError("image missing or exceeds 8 MiB")
    return full


def read_manifest(file: Path, root: Path, require_truth=True):
    if not file.is_file() or file.stat().st_size > MAX_MANIFEST_BYTES:
        raise DatasetError("manifest missing or exceeds 2 MiB")
    rows, seen_ids = [], set()
    with file.open("r", encoding="utf-8-sig") as handle:
        for line in handle:
            if not line.strip():
                continue
            if len(rows) >= MAX_SAMPLES:
                raise DatasetError("manifest exceeds 2000 images (CPU safety limit)")
            try:
                item = json.loads(line)
            except ValueError as exc:
                raise DatasetError("manifest contains invalid JSONL") from exc
            if not isinstance(item, dict) or not SAFE_ID_RE.fullmatch(str(item.get("id", ""))):
                raise DatasetError("id is required and must be anonymous ASCII")
            if item["id"] in seen_ids:
                raise DatasetError("duplicate sample id")
            seen_ids.add(item["id"])
            image = safe_location(root, item.get("image"))
            row = {"id": item["id"], "image": image}
            if require_truth:
                label = item.get("vehicle_type")
                if not isinstance(label, str) or not LABEL_RE.fullmatch(label):
                    raise DatasetError("vehicle_type must be a normalized category label")
                split = item.get("split")
                if split not in SPLITS:
                    raise DatasetError("split must be train, validation or test")
                group = item.get("group", "unspecified")
                if not isinstance(group, str) or not LABEL_RE.fullmatch(group):
                    raise DatasetError("group must be a normalized category label")
                row.update(vehicle_type=label, split=split, group=group)
            rows.append(row)
    if not rows:
        raise DatasetError("empty manifest")
    return rows


def feature_vector(img):
    """Extract small, deterministic shape + luminance features without OCR/plate inference.

    16x16 flattened grey pixels, 8-bin orientation histograms from 6x6 coarse regions,
    2 mean grey stats per region => 256 + 288 + 72 = 616 values; pad to 640.
    Pure NumPy + Pillow, with no pretrained model dependency.
    """
    import numpy as np
    from PIL import ImageOps

    img = ImageOps.exif_transpose(img).convert("L").resize((96, 96))
    pixels = np.asarray(img, dtype=np.float32) / 255.0
    from PIL import Image
    small = np.asarray(Image.fromarray((pixels * 255).astype('uint8'), 'L').resize((16, 16)), dtype=np.float32).ravel()/255.0
    gx = np.zeros_like(pixels)
    gy = np.zeros_like(pixels)
    gx[:, 1:-1] = pixels[:, 2:] - pixels[:, :-2]
    gy[1:-1, :] = pixels[2:, :] - pixels[:-2, :]
    mags = np.hypot(gx, gy)
    angles = (np.arctan2(gy, gx) + np.pi) % np.pi
    hists = []
    stats = []
    for iy in range(6):
        for ix in range(6):
            y, x = iy * 16, ix * 16
            m = mags[y:y+16, x:x+16]
            a = angles[y:y+16, x:x+16]
            hist = np.histogram(a, bins=8, range=(0, np.pi), weights=m)[0]
            hist = hist/(hist.sum()+1e-6)
            hists.extend(hist)
            block = pixels[y:y+16, x:x+16]
            stats.extend((block.mean(), block.std()))
    values = np.concatenate((small, np.asarray(hists, dtype=np.float32), np.asarray(stats, dtype=np.float32)))
    if values.size != 616:
        raise RuntimeError('feature length mismatch')
    return np.pad(values, (0, FEATURE_DIM - len(values))).astype(np.float32)


def extract_images(rows):
    import numpy as np
    from PIL import Image, ImageOps, UnidentifiedImageError
    from PIL import ImageFile
    Image.MAX_IMAGE_PIXELS = MAX_IMAGE_PIXELS
    ImageFile.LOAD_TRUNCATED_IMAGES = False
    vectors, fingerprints = [], set()
    for item in rows:
        try:
            with Image.open(item['image']) as image:
                if image.width * image.height > MAX_IMAGE_PIXELS:
                    raise DatasetError('image exceeds 12 megapixels')
                rgb = ImageOps.exif_transpose(image).convert('RGB')
                fingerprint = hashlib.sha256(rgb.tobytes()).digest()
                if fingerprint in fingerprints:
                    raise DatasetError('duplicate image pixels detected; split leakage or reused samples')
                fingerprints.add(fingerprint)
                vectors.append(feature_vector(image))
        except (OSError, UnidentifiedImageError) as exc:
            raise DatasetError('invalid image data in manifest') from exc
    return np.stack(vectors)


def check_training_distribution(rows, min_per_split=2):
    labels = sorted({x['vehicle_type'] for x in rows})
    if len(labels) < 2 or len(labels) > 5:
        raise DatasetError('require 2 to 5 vehicle categories')
    if not 2 <= min_per_split <= 30:
        raise DatasetError('min_per_split must be 2..30')
    for label in labels:
        for split in SPLITS:
            n = sum(x['vehicle_type'] == label and x['split'] == split for x in rows)
            if n < min_per_split:
                raise DatasetError(f'not enough {split} samples for class {label}: require {min_per_split}')
    return labels


def softmax(logits):
    import numpy as np
    logits = np.asarray(logits, dtype=np.float64)
    if logits.ndim != 2:
        raise ValueError('logits must be matrix')
    if logits.shape[1] == 1:
        positive = 1.0/(1.0+np.exp(-np.clip(logits[:, 0], -40, 40)))
        return np.stack((1.0-positive, positive), axis=1)
    stable = logits - logits.max(axis=1, keepdims=True)
    probs = np.exp(stable)
    return probs/probs.sum(axis=1, keepdims=True)


def predict_probabilities(x, coef, intercept, mean, scale):
    import numpy as np
    x = np.asarray(x, dtype=np.float32)
    coef = np.asarray(coef, dtype=np.float32)
    if x.ndim != 2 or x.shape[1] != FEATURE_DIM or coef.ndim != 2 or coef.shape[1] != FEATURE_DIM:
        raise DatasetError('incompatible input/model feature dimensions')
    centered = (x - mean)/scale
    logits = centered @ coef.T + intercept
    return softmax(logits)


def metrics(truth, predicted, confidences, threshold, groups, min_group=5):
    import numpy as np
    if not (len(truth) == len(predicted) == len(confidences) == len(groups)) or not len(truth):
        raise DatasetError('invalid evaluation arrays')
    if not 0 <= threshold <= 1:
        raise DatasetError('threshold must be in [0,1]')
    ok = np.asarray(confidences) >= threshold
    correct = np.asarray(truth) == np.asarray(predicted)
    accepted = int(ok.sum())
    total = len(truth)
    pack = {'samples':total,'accepted':accepted,'abstained':total-accepted,
            'coverage':round(accepted/total,4),
            'accuracy_accepted':round(float(correct[ok].mean()),4) if accepted else None,
            'accuracy_all_including_abstentions':round(float(np.logical_and(correct,ok).mean()),4)}
    slices = {}
    for label in sorted(set(groups)):
        indices = np.asarray([g == label for g in groups])
        if indices.sum() < min_group:
            continue
        n = int(np.logical_and(indices,ok).sum())
        slices[label] = {'samples':int(indices.sum()),
                         'coverage':round(n/int(indices.sum()),4),
                         'accuracy_accepted':round(float(correct[np.logical_and(indices,ok)].mean()),4) if n else None}
    pack['groups_min_5_samples'] = slices
    return pack


def choose_threshold(truth, predicted, confidence, target_accuracy, min_accepted):
    if not 0 <= target_accuracy <= 1:
        raise DatasetError('minimum accuracy must be between 0 and 1')
    if min_accepted < 2:
        raise DatasetError('minimum accepted validation count must be >=2')
    # Locked policy: thresholds calibrated exclusively on validation, never test.
    candidates = [round(x/100,2) for x in range(50, 100, 5)]
    for t in candidates:
        stat = metrics(truth,predicted,confidence,t,['validation']*len(truth))
        if stat['accepted'] >= min_accepted and stat['accuracy_accepted'] >= target_accuracy:
            return t
    return None


def load_model(filename):
    import numpy as np
    import zipfile
    if filename.stat().st_size > 4 * 1024 * 1024:
        raise DatasetError('model file exceeds 4 MiB')
    with zipfile.ZipFile(filename) as packed:
        if len(packed.infolist()) > 7 or any(e.file_size > 1024 * 1024 for e in packed.infolist()):
            raise DatasetError('unexpected model archive entries or size')
    with np.load(filename, allow_pickle=False) as data:
        required = {'version','labels','coef','intercept','mean','scale','threshold'}
        if set(data.files) != required:
            raise DatasetError('incomplete/unsupported numeric model')
        if int(data['version']) != MODEL_VERSION:
            raise DatasetError('model version mismatch')
        labels = [str(s) for s in data['labels']]
        coef = data['coef']; intercept=data['intercept']; mean=data['mean']; scale=data['scale']; threshold=float(data['threshold'])
    if len(labels) < 2 or len(labels)>5 or len(set(labels))!=len(labels) or any(not LABEL_RE.fullmatch(s) for s in labels):
        raise DatasetError('invalid vehicle labels')
    if coef.shape not in [(1, FEATURE_DIM), (len(labels), FEATURE_DIM)] or intercept.shape != (coef.shape[0],):
        raise DatasetError('model coefficients invalid')
    if mean.shape!=(FEATURE_DIM,) or scale.shape!=(FEATURE_DIM,) or not 0 <= threshold <= 1:
        raise DatasetError('model normalizer invalid')
    if not all(np.isfinite(a).all() for a in (coef,intercept,mean,scale)) or np.any(scale<=0):
        raise DatasetError('model contains nonfinite or invalid values')
    return labels,coef,intercept,mean,scale,threshold


def atomic_json(target, obj):
    target.parent.mkdir(parents=True, exist_ok=True)
    temp = target.with_name(target.name+'.tmp')
    try:
        with temp.open('w',encoding='utf-8') as out:
            json.dump(obj,out,ensure_ascii=False,indent=2)
            out.write('\n')
        os.replace(temp,target)
    finally:
        if temp.exists(): temp.unlink()


def run_train(args):
    import numpy as np
    try:
        from sklearn.linear_model import LogisticRegression
        from sklearn.preprocessing import StandardScaler
    except ImportError as exc:
        raise DatasetError('training requires numpy, Pillow and scikit-learn; no automatic installation') from exc
    rows = read_manifest(Path(args.manifest),Path(args.dataset_root))
    labels = check_training_distribution(rows,args.min_per_split)
    x = extract_images(rows)
    partitions={name:np.asarray([i for i,r in enumerate(rows) if r['split']==name]) for name in SPLITS}
    train, val, test = (partitions[n] for n in SPLITS)
    y=np.asarray([labels.index(row['vehicle_type']) for row in rows],dtype=np.int32)
    scaler=StandardScaler().fit(x[train])
    model=LogisticRegression(max_iter=150,C=0.25,solver='lbfgs',random_state=42).fit(scaler.transform(x[train]),y[train])
    coef=model.coef_.astype(np.float32); intercept=model.intercept_.astype(np.float32)
    mean=scaler.mean_.astype(np.float32); scale=scaler.scale_.astype(np.float32)
    def subset(index):
        probs=predict_probabilities(x[index],coef,intercept,mean,scale)
        pred=probs.argmax(axis=1)
        return y[index],pred,probs.max(axis=1),[rows[i]['group'] for i in index]
    vy,vp,vc,vg=subset(val)
    chosen = choose_threshold(vy,vp,vc,args.min_accepted_accuracy,args.min_validation_accepted)
    threshold=chosen if chosen is not None else 1.0
    ty,tp,tc,tg=subset(test)
    report={'schema_version':1,'status':'OFFLINE_CANDIDATE_ONLY',
            'warning':'No live camera acceptance. Camera labels, sources and images remain local; no plate OCR is performed.',
            'method':'fixed grayscale+gradient features, LogisticRegression, calibration only on validation',
            'classes':labels,'counts':{s:len(partitions[s]) for s in SPLITS},
            'validation_target_accuracy':args.min_accepted_accuracy,
            'validation_threshold_found':chosen is not None,'threshold':threshold,
            'validation':metrics(vy,vp,vc,threshold,vg),
            'test':metrics(ty,tp,tc,threshold,tg)}
    if chosen is None:
        report['warning']+=' Validation could not meet the requested acceptance policy; model is not exported.'
        atomic_json(Path(args.report),report)
        print(json.dumps({'status':'VALIDATION_NOT_MET','training_samples':len(train),'test_samples':len(test),'model_exported':False}))
        return 3
    model_path=Path(args.model)
    model_path.parent.mkdir(parents=True,exist_ok=True)
    temp = model_path.with_name(model_path.name+'.tmp.npz')
    try:
        np.savez_compressed(temp,version=np.int32(MODEL_VERSION),labels=np.asarray(labels),coef=coef,intercept=intercept,
                            mean=mean,scale=scale,threshold=np.float32(threshold))
        os.replace(temp,model_path)
    finally:
        if temp.exists():temp.unlink()
    atomic_json(Path(args.report),report)
    print(json.dumps({'status':report['status'],'training_samples':len(train),'test_samples':len(test),
                      'validation_threshold_found':chosen is not None,'accepted_holdout':report['test']['accepted']}))
    return 0


def run_predict(args):
    import numpy as np
    from PIL import Image
    labels,coef,intercept,mean,scale,threshold=load_model(Path(args.model))
    rows=read_manifest(Path(args.manifest),Path(args.dataset_root),require_truth=False)
    x=extract_images(rows)
    probs=predict_probabilities(x,coef,intercept,mean,scale)
    target=Path(args.output)
    target.parent.mkdir(parents=True,exist_ok=True)
    temp=target.with_name(target.name+'.tmp')
    try:
        with temp.open('w',encoding='utf8') as out:
            for row,p in zip(rows,probs):
                idx=int(np.argmax(p))
                confidence=float(p[idx])
                accepted=confidence>=threshold
                # Confidential local predictions: NO image path / plate; anonymous id for caller correlation.
                out.write(json.dumps({'id':row['id'],'vehicle_type':labels[idx] if accepted else None,
                    'confidence':round(confidence,4),'requires_manual_review':not accepted})+'\n')
        os.replace(temp,target)
    finally:
        if temp.exists():temp.unlink()
    print(json.dumps({'status':'LOCAL_PREDICTIONS_ONLY','samples':len(rows),'manual_review':sum(float(p.max())<threshold for p in probs)}))
    return 0


def run_audit(args):
    rows = read_manifest(Path(args.manifest), Path(args.dataset_root))
    classes = check_training_distribution(rows, args.min_per_split)
    # Validate every image is readable, and reject identical pixels across any split.
    extract_images(rows)
    counts = {name: sum(row['split'] == name for row in rows) for name in SPLITS}
    result = {'schema_version': 1, 'status': 'DATASET_STRUCTURE_VERIFIED_NOT_MODEL_ACCEPTANCE',
        'warning': 'No raw images or labels leave this device; dataset/model quality is not proven by preflight.',
        'classes': classes, 'split_counts': counts, 'samples': len(rows),
        'image_duplicates': 0, 'image_read_errors': 0}
    atomic_json(Path(args.report),result)
    print(json.dumps({'status':result['status'],'samples':len(rows),'splits':counts}))
    return 0

def main(argv=None):
    parser=argparse.ArgumentParser(description='Offline, CPU-bounded vehicle-type baseline. No network or model downloads.')
    sub=parser.add_subparsers(dest='command',required=True)
    audit=sub.add_parser('audit')
    audit.add_argument('--manifest',required=True)
    audit.add_argument('--dataset-root',required=True)
    audit.add_argument('--report',required=True)
    audit.add_argument('--min-per-split',type=int,default=2)
    train=sub.add_parser('train')
    train.add_argument('--manifest',required=True)
    train.add_argument('--dataset-root',required=True)
    train.add_argument('--model',required=True)
    train.add_argument('--report',required=True)
    train.add_argument('--min-per-split',type=int,default=2)
    train.add_argument('--min-accepted-accuracy',type=float,default=0.9)
    train.add_argument('--min-validation-accepted',type=int,default=5)
    predict=sub.add_parser('predict')
    predict.add_argument('--manifest',required=True)
    predict.add_argument('--dataset-root',required=True)
    predict.add_argument('--model',required=True)
    predict.add_argument('--output',required=True)
    args=parser.parse_args(argv)
    try:
        return run_audit(args) if args.command=='audit' else run_train(args) if args.command=='train' else run_predict(args)
    except (DatasetError,OSError,ValueError) as err:
        print('BE2_VEHICLE_TYPE_BLOCKED: '+str(err),file=sys.stderr)
        return 2


if __name__=='__main__':
    sys.exit(main())
