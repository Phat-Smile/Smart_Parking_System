#!/usr/bin/env python3
"""Evaluate Arrival Availability Prediction Calibration into 5 Probability Bins.

Compares authentic develop baseline (`app.application.baseline_arrival`)
against AI-M1 3-window smoothing (`app.application.arrival_availability`)
on the exact same simulated snapshots and arrival points.
"""

import datetime
import os
import sys
from collections import defaultdict
from uuid import UUID

# Direct service paths
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
AI_SERVICE_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "..", "..", "ai", "ai-service"))
sys.path.insert(0, AI_SERVICE_DIR)
sys.path.insert(0, SCRIPT_DIR)

# Authentic develop baseline code (fixed snapshot from develop c451a6ca)
from baseline_arrival import (
    estimate_arrival_availability as estimate_arrival_baseline,
)

# AI-M1 3-window smoothing code
from app.application.arrival_availability import (
    AvailabilitySnapshot,
    estimate_arrival_availability as estimate_arrival_m1,
)

from generate_arrival_history import ArrivalHistoryGenerator

TZ_VN = datetime.timezone(datetime.timedelta(hours=7))

PROB_BINS = [
    ("00% - 20%", 0.00, 0.20),
    ("20% - 40%", 0.20, 0.40),
    ("40% - 60%", 0.40, 0.60),
    ("60% - 80%", 0.60, 0.80),
    ("80% - 100%", 0.80, 1.00001),
]


def get_prob_bin(p: float) -> str:
    for label, low, high in PROB_BINS:
        if low <= p < high:
            return label
    return "80% - 100%"


def run_evaluation(seed: int = 42, days: int = 35, eval_days: int = 14):
    print(f"[1] Sinh dữ liệu lịch sử với ArrivalHistoryGenerator (seed={seed}, days={days})...")
    gen = ArrivalHistoryGenerator(seed=seed, days=days)
    raw_sessions, _ = gen.generate_all()

    capacities = {
        "10000000-0000-0000-0000-000000000001": 2,   # Lot A: 2 ô tô
        "10000000-0000-0000-0000-000000000002": 20,  # Lot B: 20 xe máy
        "10000000-0000-0000-0000-000000000003": 1,   # Lot C: 1 ô tô
    }

    min_time = min(s["entry_time"] for s in raw_sessions)
    max_time = max(s["exit_time"] for s in raw_sessions)

    by_lot = defaultdict(list)
    for s in raw_sessions:
        by_lot[s["lot_id"]].append(s)

    # Sinh danh sách AvailabilitySnapshot lịch sử mỗi 15 phút
    print("[2] Sinh các snapshot lịch sử 15 phút từ các phiên đỗ...")
    all_snapshots = []
    t = min_time.replace(minute=0, second=0, microsecond=0)
    while t < max_time:
        for lot_id_str, cap in capacities.items():
            lot_u = UUID(lot_id_str)
            vtype = "Motorbike" if "0002" in lot_id_str else "Car"
            occupied = sum(1 for s in by_lot[lot_id_str] if s["entry_time"] <= t < s["exit_time"])
            avail = max(0, cap - occupied)
            all_snapshots.append(AvailabilitySnapshot(lot_u, vtype, t, avail))
        t += datetime.timedelta(minutes=15)

    print(f"    Tổng số snapshots đã tạo: {len(all_snapshots)}")

    # Đánh giá trên eval_days ngày cuối, lấy mẫu mỗi 1 giờ
    eval_start = max_time - datetime.timedelta(days=eval_days)
    print(f"[3] Đánh giá trên {eval_days} ngày cuối từ {eval_start} đến {max_time}...")

    baseline_bins = defaultdict(list)
    m1_bins = defaultdict(list)

    total_evals = 0
    eval_t = eval_start.replace(minute=0, second=0, microsecond=0)

    while eval_t < max_time:
        for lot_id_str, cap in capacities.items():
            lot_u = UUID(lot_id_str)
            vtype = "Motorbike" if "0002" in lot_id_str else "Car"

            # Giá trị thực tế tại thời điểm eval_t
            occupied_now = sum(1 for s in by_lot[lot_id_str] if s["entry_time"] <= eval_t < s["exit_time"])
            actual_avail = 1 if (cap - occupied_now) > 0 else 0

            # Lịch sử khả dụng trước eval_t (tối đa 28 ngày)
            past_snaps = [
                s for s in all_snapshots
                if s.lot_id == lot_u and s.captured_at < eval_t and s.captured_at >= (eval_t - datetime.timedelta(days=28))
            ]

            # 1. Gọi trực tiếp hàm dịch vụ gốc develop (Baseline thật)
            res_base = estimate_arrival_baseline(lot_u, vtype, eval_t, 0, past_snaps)
            p_base = res_base.probability_percent / 100.0
            bin_base = get_prob_bin(p_base)
            baseline_bins[bin_base].append((p_base, actual_avail))

            # 2. Gọi trực tiếp hàm dịch vụ sản xuất AI-M1 (3 cửa sổ làm mịn có trọng số)
            res_m1 = estimate_arrival_m1(lot_u, vtype, eval_t, 0, past_snaps)
            p_m1 = res_m1.probability_percent / 100.0
            bin_m1 = get_prob_bin(p_m1)
            m1_bins[bin_m1].append((p_m1, actual_avail))

            total_evals += 1

        eval_t += datetime.timedelta(hours=1)

    print(f"    Tổng số lần đánh giá: {total_evals}")

    # In Bảng hiệu chuẩn 5 nhóm
    print("\n" + "=" * 115)
    print("BẢNG HIỆU CHUẨN XÁC SUẤT 5 NHÓM (PROBABILITY CALIBRATION TABLE)")
    print("SO SÁNH MÃ GỐC DEVELOP THẬT (BASELINE) VS MÃ AI-M1 LÀM MỊN 3 CỬA SỔ")
    print(f"Tham số: seed={seed}, days={days}, eval_days={eval_days}, N_points={total_evals}")
    print("=" * 115)
    print(f"{'Nhóm xác suất':<15} | {'Mô hình':<10} | {'Số mẫu (N)':<10} | {'Dự báo TB (Mean P)':<20} | {'Thực tế còn chỗ (Obs Y)':<24} | {'Độ lệch hiệu chuẩn (|P - Y|)':<25}")
    print("-" * 115)

    all_bin_labels = [b[0] for b in PROB_BINS]

    for b in all_bin_labels:
        # Baseline thật
        pairs_b = baseline_bins[b]
        nb = len(pairs_b)
        if nb > 0:
            mean_pb = sum(p for p, _ in pairs_b) / nb
            mean_yb = sum(y for _, y in pairs_b) / nb
            diff_b = abs(mean_pb - mean_yb)
            str_pb = f"{mean_pb * 100:.2f}%"
            str_yb = f"{mean_yb * 100:.2f}%"
            str_diffb = f"{diff_b * 100:.2f}%"
        else:
            str_pb, str_yb, str_diffb = "N/A", "N/A", "N/A"

        # AI-M1
        pairs_m1 = m1_bins[b]
        nm1 = len(pairs_m1)
        if nm1 > 0:
            mean_pm1 = sum(p for p, _ in pairs_m1) / nm1
            mean_ym1 = sum(y for _, y in pairs_m1) / nm1
            diff_m1 = abs(mean_pm1 - mean_ym1)
            str_pm1 = f"{mean_pm1 * 100:.2f}%"
            str_ym1 = f"{mean_ym1 * 100:.2f}%"
            str_diffm1 = f"{diff_m1 * 100:.2f}%"
        else:
            str_pm1, str_ym1, str_diffm1 = "N/A", "N/A", "N/A"

        print(f"{b:<15} | {'Baseline':<10} | {nb:<10} | {str_pb:<20} | {str_yb:<24} | {str_diffb:<25}")
        print(f"{'':<15} | {'AI-M1':<10} | {nm1:<10} | {str_pm1:<20} | {str_ym1:<24} | {str_diffm1:<25}")
        print("-" * 115)

    # Thống kê tổng hợp toàn bộ tập dữ liệu
    all_pairs_b = [pair for sublist in baseline_bins.values() for pair in sublist]
    all_pairs_m1 = [pair for sublist in m1_bins.values() for pair in sublist]

    brier_b = sum((p - y) ** 2 for p, y in all_pairs_b) / len(all_pairs_b)
    brier_m1 = sum((p - y) ** 2 for p, y in all_pairs_m1) / len(all_pairs_m1)
    mae_b = sum(abs(p - y) for p, y in all_pairs_b) / len(all_pairs_b)
    mae_m1 = sum(abs(p - y) for p, y in all_pairs_m1) / len(all_pairs_m1)

    print(f"TỔNG HỢP TOÀN BỘ ({len(all_pairs_b)} mẫu):")
    print(f"  * Baseline Brier Score: {brier_b:.4f}  |  AI-M1 Brier Score: {brier_m1:.4f}  (Mức giảm Brier: {((brier_b - brier_m1)/brier_b)*100:+.2f}%)")
    print(f"  * Baseline MAE        : {mae_b:.4f}  |  AI-M1 MAE        : {mae_m1:.4f}  (Mức giảm MAE  : {mae_b - mae_m1:+.4f})")
    print("=" * 115)


if __name__ == "__main__":
    run_evaluation()
