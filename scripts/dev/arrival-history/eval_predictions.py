#!/usr/bin/env python3
"""AI-D2 Prediction Evaluation Suite (Brier Score & MAE).

Evaluates the arrival-availability probability estimator against ground truth
parking session occupancy across time buckets (Peak Morning, Lunch, Peak Evening, Night, Off-peak).

Usage:
    python eval_predictions.py
    python eval_predictions.py --synthetic
    python eval_predictions.py --csv report.csv
"""

import argparse
import datetime
import math
import os
import re
import subprocess
import sys
from collections import defaultdict
from typing import Dict, List, NamedTuple, Tuple

# Vietnam timezone (UTC+7)
TZ_VN = datetime.timezone(datetime.timedelta(hours=7))

# Time buckets for evaluation
TIME_BUCKETS = {
    "Morning Peak (07:00 - 09:00)": (7, 9),
    "Lunch / Midday (11:00 - 13:00)": (11, 13),
    "Evening Peak (16:00 - 18:00)": (16, 18),
    "Night (19:00 - 23:00)": (19, 23),
    "Off-peak (00:00 - 06:00)": (0, 6),
}


class SessionRecord(NamedTuple):
    lot_id: str
    vehicle_type: str
    entry_time: datetime.datetime
    exit_time: datetime.datetime


class EvalResult(NamedTuple):
    bucket: str
    sample_count: int
    brier_score: float
    mae: float
    mean_predicted: float
    actual_availability_rate: float


def get_time_bucket(dt: datetime.datetime) -> str:
    hour = dt.astimezone(TZ_VN).hour
    for name, (start, end) in TIME_BUCKETS.items():
        if start <= hour <= end:
            return name
    return "Off-peak (Other)"


def fetch_sessions_from_postgres() -> List[SessionRecord]:
    """Fetch completed parking sessions from parking_demo PostgreSQL container."""
    cmd = [
        "docker", "exec", "parking-system-postgres-1",
        "psql", "-U", "parking_dev", "-d", "parking_demo", "-t", "-A", "-F", "|",
        "-c",
        "SELECT lot_id, vehicle_type, entry_time, exit_time FROM parking.parking_sessions WHERE status = 'Completed' AND exit_time IS NOT NULL;"
    ]
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, check=True)
    except Exception as e:
        print(f"[WARN] Cannot query Docker postgres directly: {e}. Falling back to synthetic simulation.", file=sys.stderr)
        return []

    records = []
    for line in proc.stdout.strip().splitlines():
        parts = line.split("|")
        if len(parts) >= 4:
            lot_id, vehicle_type, entry_str, exit_str = parts[0], parts[1], parts[2], parts[3]
            try:
                # Handle ISO timestamps with timezone offsets (+00 -> +00:00)
                entry_str_clean = re.sub(r"([+-]\d{2})$", r"\1:00", entry_str)
                exit_str_clean = re.sub(r"([+-]\d{2})$", r"\1:00", exit_str)
                entry_dt = datetime.datetime.fromisoformat(entry_str_clean)
                exit_dt = datetime.datetime.fromisoformat(exit_str_clean)
                records.append(SessionRecord(lot_id, vehicle_type, entry_dt, exit_dt))
            except Exception as e:
                continue
    return records


def evaluate_dataset(sessions: List[SessionRecord]) -> List[EvalResult]:
    """Compute Brier Score and MAE stratified by time bucket."""
    if not sessions:
        return []

    # Capacities for known demo lots
    capacities = {
        "10000000-0000-0000-0000-000000000001": 2,   # Lot A: 2 Car spots
        "10000000-0000-0000-0000-000000000002": 20,  # Lot B: 20 Motorbike spots
        "10000000-0000-0000-0000-000000000003": 1,   # Lot C: 1 Car spot
    }

    # Group sessions by lot
    by_lot = defaultdict(list)
    for s in sessions:
        by_lot[s.lot_id].append(s)

    # We evaluate 15-minute grid points across the last 14 days of data
    max_time = max(s.exit_time for s in sessions)
    min_eval_time = max_time - datetime.timedelta(days=14)

    bucket_stats: Dict[str, List[Tuple[float, int]]] = defaultdict(list)

    for lot_id, lot_sessions in by_lot.items():
        cap = capacities.get(lot_id, 2)
        # Sample every 1 hour for evaluation across the 14 days
        eval_dt = min_eval_time.replace(minute=0, second=0, microsecond=0)
        while eval_dt < max_time:
            local_dt = eval_dt.astimezone(TZ_VN)
            bucket_name = get_time_bucket(local_dt)
            weekday = local_dt.weekday()
            hour = local_dt.hour
            minute_quarter = (local_dt.minute // 15) * 15

            # Ground truth: is available_slots > 0 at eval_dt?
            active_now = sum(1 for s in lot_sessions if s.entry_time <= eval_dt < s.exit_time)
            ground_truth = 1 if (cap - active_now) > 0 else 0

            # Model prediction: Historical 28-day window prior to eval_dt
            window_start = eval_dt - datetime.timedelta(days=28)
            # Find matching historical snapshots on the same weekday and 15-min quarter
            past_days = [
                eval_dt - datetime.timedelta(days=7 * k)
                for k in range(1, 5)
                if (eval_dt - datetime.timedelta(days=7 * k)) >= window_start
            ]

            historical_availabilities = []
            for past_snap in past_days:
                occupied_past = sum(1 for s in lot_sessions if s.entry_time <= past_snap < s.exit_time)
                avail_past = max(0, cap - occupied_past)
                historical_availabilities.append(avail_past > 0)

            # Laplace smoothing: (k + 1) / (n + 2)
            n_samples = len(historical_availabilities)
            if n_samples > 0:
                k_success = sum(1 for a in historical_availabilities if a)
                pred_prob = (k_success + 1) / (n_samples + 2)
            else:
                pred_prob = 0.5

            bucket_stats[bucket_name].append((pred_prob, ground_truth))
            eval_dt += datetime.timedelta(hours=1)

    results = []
    for bucket_name, pairs in bucket_stats.items():
        if not pairs:
            continue
        n = len(pairs)
        brier = sum((p - y) ** 2 for p, y in pairs) / n
        mae = sum(abs(p - y) for p, y in pairs) / n
        mean_p = sum(p for p, _ in pairs) / n
        mean_y = sum(y for _, y in pairs) / n
        results.append(EvalResult(bucket_name, n, round(brier, 4), round(mae, 4), round(mean_p * 100, 2), round(mean_y * 100, 2)))

    results.sort(key=lambda r: r.bucket)
    return results


def print_results(results: List[EvalResult], csv_path: str | None = None):
    print("=" * 95)
    print("AI-D2 ARRIVAL PREDICTION EVALUATION REPORT (BRIER SCORE & MAE)")
    print("=" * 95)
    header = f"{'Khung thời gian (Time Bucket)':<34} | {'Mẫu (N)':<8} | {'Brier Score':<11} | {'MAE':<8} | {'Dự báo TB (%)':<14} | {'Thực tế còn (%)':<16}"
    print(header)
    print("-" * 95)
    for r in results:
        line = f"{r.bucket:<34} | {r.sample_count:<8} | {r.brier_score:<11.4f} | {r.mae:<8.4f} | {r.mean_predicted:<14.2f} | {r.actual_availability_rate:<16.2f}"
        print(line)
    print("-" * 95)
    if results:
        overall_brier = sum(r.brier_score * r.sample_count for r in results) / sum(r.sample_count for r in results)
        overall_mae = sum(r.mae * r.sample_count for r in results) / sum(r.sample_count for r in results)
        print(f"{'TỔNG THỂ TOÀN BỘ CÁC KHUNG':<34} | {sum(r.sample_count for r in results):<8} | {overall_brier:<11.4f} | {overall_mae:<8.4f} | {'-':<14} | {'-':<16}")
    print("=" * 95)

    if csv_path:
        with open(csv_path, "w", encoding="utf-8") as f:
            f.write("TimeBucket,SampleCount,BrierScore,MAE,MeanPredictedPercent,ActualAvailabilityPercent\n")
            for r in results:
                f.write(f'"{r.bucket}",{r.sample_count},{r.brier_score},{r.mae},{r.mean_predicted},{r.actual_availability_rate}\n')
        print(f"[INFO] Báo cáo CSV đã được lưu tại: {csv_path}")


def main():
    parser = argparse.ArgumentParser(description="Evaluate AI arrival availability predictions")
    parser.add_argument("--synthetic", action="store_true", help="Force synthetic generator if database is offline")
    parser.add_argument("--csv", help="Optional output CSV path")
    args = parser.parse_args()

    sessions = []
    if not args.synthetic:
        sessions = fetch_sessions_from_postgres()

    if not sessions:
        print("[INFO] Đang chạy trên mô phỏng dữ liệu lịch sử...")
        from generate_arrival_history import ArrivalHistoryGenerator
        gen = ArrivalHistoryGenerator(seed=42, days=35)
        raw_sessions, _ = gen.generate_all()
        for s in raw_sessions:
            sessions.append(SessionRecord(s["lot_id"], s["vehicle_type"], s["entry_time"], s["exit_time"]))

    results = evaluate_dataset(sessions)
    print_results(results, args.csv)


if __name__ == "__main__":
    main()
