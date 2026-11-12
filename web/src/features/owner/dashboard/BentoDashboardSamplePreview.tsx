"use client";

import React, { useState } from "react";
import Image from "next/image";
import Link from "next/link";
import "@/styles/bento-dashboard.css";

interface MockLotItem {
    id: number;
    name: string;
    district: string;
    total: number;
    occupied: number;
    revenue: number;
    status: "active" | "pending";
    image: string;
}

const SAMPLE_LOTS: MockLotItem[] = [
    {
        id: 1,
        name: "Bãi Nguyễn Huệ ParkWise Central",
        district: "Quận 1",
        total: 250,
        occupied: 215,
        revenue: 8650000,
        status: "active",
        image: "/images/parkwise/dashboard-parking-garage.webp",
    },
    {
        id: 2,
        name: "Bãi Lê Lợi Premium Garage",
        district: "Quận 1",
        total: 180,
        occupied: 148,
        revenue: 5920000,
        status: "active",
        image: "/images/parkwise/dashboard-parking-structure.webp",
    },
    {
        id: 3,
        name: "Bãi Trần Hưng Đạo Smart Hub",
        district: "Quận 5",
        total: 150,
        occupied: 113,
        revenue: 3880000,
        status: "active",
        image: "/images/parkwise/dashboard-parking-entrance.webp",
    },
];

interface TrafficDataPoint {
    time: string;
    inCount: number;
    outCount: number;
    x: number;
    yIn: number;
    yOut: number;
}

const TRAFFIC_TODAY: TrafficDataPoint[] = [
    { time: "06:00", inCount: 25, outCount: 18, x: 50, yIn: 135, yOut: 142 },
    { time: "08:30", inCount: 56, outCount: 48, x: 135, yIn: 104, yOut: 112 },
    { time: "11:30", inCount: 62, outCount: 54, x: 220, yIn: 98, yOut: 106 },
    { time: "14:30", inCount: 42, outCount: 32, x: 305, yIn: 118, yOut: 128 },
    { time: "17:30", inCount: 88, outCount: 82, x: 390, yIn: 72, yOut: 78 },
    { time: "20:30", inCount: 64, outCount: 52, x: 475, yIn: 96, yOut: 108 },
    { time: "23:00", inCount: 46, outCount: 38, x: 560, yIn: 114, yOut: 122 },
];

const TRAFFIC_7DAYS: TrafficDataPoint[] = [
    { time: "T2", inCount: 420, outCount: 395, x: 50, yIn: 118, yOut: 122 },
    { time: "T3", inCount: 480, outCount: 460, x: 135, yIn: 112, yOut: 115 },
    { time: "T4", inCount: 510, outCount: 490, x: 220, yIn: 106, yOut: 109 },
    { time: "T5", inCount: 570, outCount: 540, x: 305, yIn: 96, yOut: 102 },
    { time: "T6", inCount: 620, outCount: 605, x: 390, yIn: 72, yOut: 75 },
    { time: "T7", inCount: 500, outCount: 485, x: 475, yIn: 108, yOut: 110 },
    { time: "CN", inCount: 470, outCount: 450, x: 560, yIn: 114, yOut: 116 },
];

const TRAFFIC_MONTH: TrafficDataPoint[] = [
    {
        time: "Tuần 1",
        inCount: 3120,
        outCount: 3010,
        x: 50,
        yIn: 115,
        yOut: 118,
    },
    {
        time: "Tuần 2",
        inCount: 3450,
        outCount: 3380,
        x: 220,
        yIn: 105,
        yOut: 108,
    },
    {
        time: "Tuần 3",
        inCount: 3890,
        outCount: 3750,
        x: 390,
        yIn: 75,
        yOut: 80,
    },
    {
        time: "Tuần 4",
        inCount: 3620,
        outCount: 3510,
        x: 560,
        yIn: 95,
        yOut: 100,
    },
];

const SAMPLE_SESSIONS = [
    {
        id: "SES-9821",
        plate: "51K-892.41",
        type: "Ô tô (4-7 chỗ)",
        slot: "A-04",
        time: "14:15",
        duration: "1h 18p",
        fee: "60.000 đ",
        status: "active",
    },
    {
        id: "SES-9820",
        plate: "59T2-481.90",
        type: "Xe máy",
        slot: "M-08",
        time: "13:50",
        duration: "1h 43p",
        fee: "10.000 đ",
        status: "active",
    },
    {
        id: "SES-9819",
        plate: "60A-712.33",
        type: "Ô tô (4-7 chỗ)",
        slot: "B-12",
        time: "12:10",
        duration: "3h 23p",
        fee: "150.000 đ",
        status: "overstay",
    },
    {
        id: "SES-9818",
        plate: "51H-391.02",
        type: "Ô tô (4-7 chỗ)",
        slot: "A-01",
        time: "11:30",
        duration: "2h 10p",
        fee: "90.000 đ",
        status: "completed",
    },
    {
        id: "SES-9817",
        plate: "59P1-902.11",
        type: "Xe máy",
        slot: "M-14",
        time: "11:00",
        duration: "2h 45p",
        fee: "15.000 đ",
        status: "completed",
    },
];

export function BentoDashboardSamplePreview() {
    const [chartTimeframe, setChartTimeframe] = useState<
        "today" | "week" | "month"
    >("today");
    const [surgeAutoEnabled, setSurgeAutoEnabled] = useState(true);
    const [activePointIndex, setActivePointIndex] = useState<number>(4);

    const trafficData =
        chartTimeframe === "today"
            ? TRAFFIC_TODAY
            : chartTimeframe === "week"
              ? TRAFFIC_7DAYS
              : TRAFFIC_MONTH;

    const currentPoint =
        trafficData[Math.min(activePointIndex, trafficData.length - 1)];

    const inPath = trafficData.reduce(
        (acc, p, i) =>
            i === 0 ? `M ${p.x} ${p.yIn}` : `${acc} L ${p.x} ${p.yIn}`,
        "",
    );
    const inAreaPath = `${inPath} L ${trafficData[trafficData.length - 1].x} 160 L ${trafficData[0].x} 160 Z`;

    const outPath = trafficData.reduce(
        (acc, p, i) =>
            i === 0 ? `M ${p.x} ${p.yOut}` : `${acc} L ${p.x} ${p.yOut}`,
        "",
    );

    return (
        <div className="bento-dashboard-wrap" style={{ marginTop: 12 }}>
            {/* ── ROW 1: 3 BENTO COLUMNS ── */}
            <div className="bento-row-top">
                {/* CỘT 1: DOANH THU HÔM NAY + 3 THẺ PHÂN BỔ NHANH */}
                <div
                    className="bento-card"
                    style={{
                        display: "flex",
                        flexDirection: "column",
                        justifyContent: "space-between",
                    }}
                >
                    <div>
                        <div
                            className="bento-card-header"
                            style={{ marginBottom: 8 }}
                        >
                            <div className="bento-card-title-wrap">
                                <div
                                    style={{
                                        width: 22,
                                        height: 22,
                                        borderRadius: "50%",
                                        background: "#EAF7EE",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        color: "#19301D",
                                    }}
                                >
                                    <svg
                                        width="13"
                                        height="13"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="currentColor"
                                        strokeWidth="2.5"
                                    >
                                        <line x1="12" y1="1" x2="12" y2="23" />
                                        <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                                    </svg>
                                </div>
                                <h3 className="bento-card-title">
                                    Doanh thu hôm nay
                                </h3>
                            </div>
                            <span className="bento-badge-success">
                                ↑ +14.2% so với hôm qua
                            </span>
                        </div>

                        <div className="bento-rev-number">
                            18.450.000 <span className="bento-rev-unit">đ</span>
                        </div>
                        <div className="bento-rev-sub">
                            Phí thực thu đã trừ cọc: 16.210.000 đ
                        </div>

                        {/* Sparkline wave */}
                        <div
                            style={{
                                width: "100%",
                                height: 48,
                                margin: "10px 0",
                            }}
                        >
                            <svg
                                viewBox="0 0 340 48"
                                width="100%"
                                height="100%"
                                preserveAspectRatio="none"
                            >
                                <defs>
                                    <linearGradient
                                        id="simWaveGrad"
                                        x1="0"
                                        y1="0"
                                        x2="0"
                                        y2="1"
                                    >
                                        <stop
                                            offset="0%"
                                            stopColor="#269B62"
                                            stopOpacity="0.32"
                                        />
                                        <stop
                                            offset="100%"
                                            stopColor="#269B62"
                                            stopOpacity="0.0"
                                        />
                                    </linearGradient>
                                </defs>
                                <path
                                    d="M 0 38 Q 45 42, 90 28 T 180 32 T 260 16 T 340 10 L 340 48 L 0 48 Z"
                                    fill="url(#simWaveGrad)"
                                />
                                <path
                                    d="M 0 38 Q 45 42, 90 28 T 180 32 T 260 16 T 340 10"
                                    fill="none"
                                    stroke="#269B62"
                                    strokeWidth="2.4"
                                    strokeLinecap="round"
                                />
                                <circle
                                    cx="340"
                                    cy="10"
                                    r="3.5"
                                    fill="#269B62"
                                    stroke="#FFFFFF"
                                    strokeWidth="2"
                                />
                            </svg>
                        </div>
                    </div>

                    <div>
                        <div
                            style={{
                                fontSize: 12,
                                fontWeight: 700,
                                color: "#172018",
                                margin: "10px 0 6px",
                            }}
                        >
                            Chỉ số phân bổ nhanh
                        </div>
                        <div className="bento-mini-kpis">
                            <div className="bento-mini-kpi-card highlight">
                                <div className="bento-mini-kpi-label">
                                    <svg
                                        width="11"
                                        height="11"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="currentColor"
                                        strokeWidth="2.5"
                                    >
                                        <rect
                                            x="2"
                                            y="5"
                                            width="20"
                                            height="14"
                                            rx="2"
                                        />
                                        <circle cx="7" cy="15" r="2" />
                                        <circle cx="17" cy="15" r="2" />
                                    </svg>
                                    Bãi đông nhất
                                </div>
                                <div className="bento-mini-kpi-val">86%</div>
                                <div className="bento-mini-kpi-desc">
                                    Nguyễn Huệ
                                </div>
                            </div>

                            <div className="bento-mini-kpi-card">
                                <div className="bento-mini-kpi-label">
                                    <svg
                                        width="11"
                                        height="11"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="#269B62"
                                        strokeWidth="2.5"
                                    >
                                        <path d="M16 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                                        <circle cx="8.5" cy="7" r="4" />
                                    </svg>
                                    Lượt gửi xe
                                </div>
                                <div className="bento-mini-kpi-val">476</div>
                                <div
                                    className="bento-mini-kpi-desc"
                                    style={{
                                        color: "#269B62",
                                        fontWeight: 700,
                                    }}
                                >
                                    ↑ +12% so với hôm qua
                                </div>
                            </div>

                            <div className="bento-mini-kpi-card">
                                <div className="bento-mini-kpi-label">
                                    <svg
                                        width="11"
                                        height="11"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="#718071"
                                        strokeWidth="2.5"
                                    >
                                        <rect
                                            x="3"
                                            y="4"
                                            width="18"
                                            height="18"
                                            rx="2"
                                        />
                                        <line x1="16" y1="2" x2="16" y2="6" />
                                        <line x1="8" y1="2" x2="8" y2="6" />
                                    </svg>
                                    Đặt giữ chỗ
                                </div>
                                <div className="bento-mini-kpi-val">24</div>
                                <div
                                    className="bento-mini-kpi-desc"
                                    style={{
                                        color: "#269B62",
                                        fontWeight: 700,
                                    }}
                                >
                                    ↑ +8%
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                {/* CỘT 2: TÌNH TRẠNG SỨC CHỨA CƠ SỞ BÃI XE */}
                <div className="bento-card">
                    <div className="bento-card-header">
                        <div className="bento-card-title-wrap">
                            <div
                                style={{
                                    width: 22,
                                    height: 22,
                                    borderRadius: "50%",
                                    background: "#EAF7EE",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    color: "#19301D",
                                }}
                            >
                                <svg
                                    width="13"
                                    height="13"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="currentColor"
                                    strokeWidth="2.5"
                                >
                                    <rect
                                        x="2"
                                        y="5"
                                        width="20"
                                        height="14"
                                        rx="2"
                                    />
                                    <circle cx="7" cy="15" r="2" />
                                    <circle cx="17" cy="15" r="2" />
                                </svg>
                            </div>
                            <h3 className="bento-card-title">
                                Tình trạng sức chứa cơ sở bãi xe
                            </h3>
                        </div>
                        <span
                            style={{
                                fontSize: 12,
                                fontWeight: 700,
                                color: "#718071",
                            }}
                        >
                            3 cơ sở mẫu
                        </span>
                    </div>

                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 10,
                        }}
                    >
                        {SAMPLE_LOTS.map((lot) => {
                            const pct = Math.round(
                                (lot.occupied / lot.total) * 100,
                            );
                            return (
                                <div key={lot.id} className="bento-lot-item">
                                    <Image
                                        src={lot.image}
                                        alt={lot.name}
                                        width={48}
                                        height={48}
                                        className="bento-lot-thumb"
                                    />
                                    <div className="bento-lot-info">
                                        <div className="bento-lot-name-row">
                                            <h4 className="bento-lot-name">
                                                {lot.name}
                                            </h4>
                                            <span className="bento-lot-tag">
                                                {lot.district}
                                            </span>
                                        </div>
                                        <div className="bento-progress-wrap">
                                            <div className="bento-progress-bar">
                                                <div
                                                    className={`bento-progress-fill ${pct >= 85 ? "danger" : pct >= 75 ? "warning" : ""}`}
                                                    style={{ width: `${pct}%` }}
                                                />
                                            </div>
                                            <span className="bento-lot-spots-text">
                                                {lot.occupied}/{lot.total} chỗ (
                                                {pct}%)
                                            </span>
                                        </div>
                                    </div>
                                    <div className="bento-lot-side">
                                        <div className="bento-lot-rev">
                                            {lot.revenue.toLocaleString(
                                                "vi-VN",
                                            )}{" "}
                                            đ
                                        </div>
                                        <div className="bento-lot-status">
                                            <span
                                                style={{
                                                    width: 6,
                                                    height: 6,
                                                    borderRadius: "50%",
                                                    background: "#269B62",
                                                }}
                                            />
                                            Đang đón xe
                                        </div>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>

                {/* CỘT 3: LƯU LƯỢNG 7 NGÀY & VẬN HÀNH THÔNG MINH */}
                <div className="bento-right-col">
                    <div
                        className="bento-card"
                        style={{ padding: "16px 18px" }}
                    >
                        <div
                            className="bento-card-header"
                            style={{ marginBottom: 6 }}
                        >
                            <div className="bento-card-title-wrap">
                                <svg
                                    width="14"
                                    height="14"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="#269B62"
                                    strokeWidth="2.5"
                                >
                                    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
                                </svg>
                                <h3
                                    className="bento-card-title"
                                    style={{ fontSize: 13.5 }}
                                >
                                    Lưu lượng 7 ngày qua
                                </h3>
                            </div>
                            <span
                                style={{
                                    fontSize: 11,
                                    fontWeight: 700,
                                    color: "#269B62",
                                }}
                            >
                                ↑ +8.5%
                            </span>
                        </div>

                        <div className="bento-bars-grid">
                            {[
                                { day: "T2", count: 420, height: 48 },
                                { day: "T3", count: 480, height: 56 },
                                { day: "T4", count: 510, height: 60 },
                                { day: "T5", count: 570, height: 68 },
                                {
                                    day: "T6",
                                    count: 620,
                                    height: 78,
                                    isPeak: true,
                                },
                                { day: "T7", count: 500, height: 58 },
                                { day: "CN", count: 470, height: 54 },
                            ].map((col) => (
                                <div
                                    key={col.day}
                                    className={`bento-bar-col ${col.isPeak ? "highlight" : ""}`}
                                >
                                    {col.isPeak && (
                                        <div className="bento-bar-tag">
                                            620 lượt
                                        </div>
                                    )}
                                    <div
                                        className="bento-bar-rect"
                                        style={{ height: col.height }}
                                    />
                                    <div className="bento-bar-label">
                                        {col.day}
                                    </div>
                                    <div className="bento-bar-sub">
                                        {col.count}
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>

                    <div className="bento-smart-card">
                        <div
                            style={{
                                position: "relative",
                                zIndex: 2,
                                maxWidth: "62%",
                            }}
                        >
                            <div
                                style={{
                                    fontSize: 9.5,
                                    fontWeight: 800,
                                    letterSpacing: 0.8,
                                    color: "#B8F34A",
                                    marginBottom: 3,
                                    textTransform: "uppercase",
                                }}
                            >
                                VẬN HÀNH THÔNG MINH
                            </div>
                            <div
                                style={{
                                    fontSize: 13,
                                    fontWeight: 800,
                                    color: "#FFFFFF",
                                    lineHeight: 1.3,
                                    marginBottom: 4,
                                }}
                            >
                                Bật tự động giá cao điểm chiều nay?
                            </div>
                            <div
                                style={{
                                    fontSize: 10.5,
                                    color: "#A8BFA9",
                                    lineHeight: 1.3,
                                    marginBottom: 10,
                                }}
                            >
                                Tự động tăng +20% biểu phí từ 17:00 - 19:00 tại
                                2 bãi Quận 1.
                            </div>
                            <span
                                style={{
                                    display: "inline-block",
                                    background: "#B8F34A",
                                    color: "#19301D",
                                    fontSize: 11,
                                    fontWeight: 800,
                                    padding: "5px 12px",
                                    borderRadius: 6,
                                    cursor: "pointer",
                                    boxShadow: "0 2px 6px rgba(0,0,0,0.2)",
                                }}
                            >
                                Thử nghiệm cấu hình →
                            </span>
                        </div>

                        <div
                            style={{
                                position: "relative",
                                zIndex: 2,
                                display: "flex",
                                flexDirection: "column",
                                alignItems: "flex-end",
                                gap: 12,
                            }}
                        >
                            <button
                                type="button"
                                onClick={() =>
                                    setSurgeAutoEnabled(!surgeAutoEnabled)
                                }
                                style={{
                                    width: 44,
                                    height: 24,
                                    borderRadius: 999,
                                    background: surgeAutoEnabled
                                        ? "#B8F34A"
                                        : "#324A36",
                                    border: "none",
                                    cursor: "pointer",
                                    padding: 2,
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: surgeAutoEnabled
                                        ? "flex-end"
                                        : "flex-start",
                                    transition: "all 0.2s ease",
                                }}
                            >
                                <span
                                    style={{
                                        width: 20,
                                        height: 20,
                                        borderRadius: "50%",
                                        background: surgeAutoEnabled
                                            ? "#19301D"
                                            : "#A8BFA9",
                                    }}
                                />
                            </button>
                        </div>

                        <Image
                            src="/images/parkwise/vehicle-sedan-cutout.png"
                            alt="Xe sedan ParkWise"
                            width={145}
                            height={75}
                            className="bento-smart-card-bg-car"
                            style={{ objectFit: "contain" }}
                        />
                    </div>
                </div>
            </div>

            {/* ── ROW 2: LINE CHART + PAYMENT BREAKDOWN ── */}
            <div className="bento-row-middle">
                <div className="bento-card">
                    <div className="bento-card-header">
                        <div className="bento-card-title-wrap">
                            <div
                                style={{
                                    width: 22,
                                    height: 22,
                                    borderRadius: "50%",
                                    background: "#EAF7EE",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    color: "#19301D",
                                }}
                            >
                                <svg
                                    width="13"
                                    height="13"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="currentColor"
                                    strokeWidth="2.5"
                                >
                                    <line x1="18" y1="20" x2="18" y2="10" />
                                    <line x1="12" y1="20" x2="12" y2="4" />
                                    <line x1="6" y1="20" x2="6" y2="14" />
                                </svg>
                            </div>
                            <h3 className="bento-card-title">
                                Biến động lưu lượng xe vào/ra theo khung giờ
                                (06:00 - 23:00)
                            </h3>
                        </div>

                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 16,
                                flexWrap: "wrap",
                            }}
                        >
                            <div className="bento-timeframe-pills">
                                <button
                                    type="button"
                                    className={`bento-timeframe-btn ${chartTimeframe === "today" ? "active" : ""}`}
                                    onClick={() => {
                                        setChartTimeframe("today");
                                        setActivePointIndex(4);
                                    }}
                                >
                                    Hôm nay
                                </button>
                                <button
                                    type="button"
                                    className={`bento-timeframe-btn ${chartTimeframe === "week" ? "active" : ""}`}
                                    onClick={() => {
                                        setChartTimeframe("week");
                                        setActivePointIndex(4);
                                    }}
                                >
                                    7 ngày qua
                                </button>
                                <button
                                    type="button"
                                    className={`bento-timeframe-btn ${chartTimeframe === "month" ? "active" : ""}`}
                                    onClick={() => {
                                        setChartTimeframe("month");
                                        setActivePointIndex(2);
                                    }}
                                >
                                    Tháng này
                                </button>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 12,
                                    fontSize: 12,
                                    fontWeight: 700,
                                }}
                            >
                                <div
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 5,
                                    }}
                                >
                                    <span
                                        style={{
                                            width: 8,
                                            height: 8,
                                            borderRadius: "50%",
                                            background: "#19301D",
                                        }}
                                    />
                                    <span style={{ color: "#172018" }}>
                                        Vào
                                    </span>
                                </div>
                                <div
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 5,
                                    }}
                                >
                                    <span
                                        style={{
                                            width: 8,
                                            height: 8,
                                            borderRadius: "50%",
                                            background: "#A7E34A",
                                        }}
                                    />
                                    <span style={{ color: "#718071" }}>Ra</span>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div
                        className="bento-chart-container"
                        style={{ height: 180 }}
                    >
                        <svg
                            viewBox="0 0 600 180"
                            width="100%"
                            height="100%"
                            preserveAspectRatio="none"
                        >
                            <defs>
                                <linearGradient
                                    id="simAreaGreenFill"
                                    x1="0"
                                    y1="0"
                                    x2="0"
                                    y2="1"
                                >
                                    <stop
                                        offset="0%"
                                        stopColor="#A7E34A"
                                        stopOpacity="0.30"
                                    />
                                    <stop
                                        offset="100%"
                                        stopColor="#A7E34A"
                                        stopOpacity="0.0"
                                    />
                                </linearGradient>
                            </defs>

                            {[0, 20, 40, 60, 80, 100].map((val, idx) => {
                                const y = 160 - (val / 100) * 110;
                                return (
                                    <g key={idx}>
                                        <line
                                            x1="40"
                                            y1={y}
                                            x2="580"
                                            y2={y}
                                            stroke="#EEF2EB"
                                            strokeWidth="1"
                                            strokeDasharray="3 3"
                                        />
                                        <text
                                            x="32"
                                            y={y + 3}
                                            textAnchor="end"
                                            fontSize="10"
                                            fill="#718071"
                                            fontWeight="600"
                                        >
                                            {val}
                                        </text>
                                    </g>
                                );
                            })}

                            <path
                                d={inAreaPath}
                                fill="url(#simAreaGreenFill)"
                            />
                            <path
                                d={inPath}
                                fill="none"
                                stroke="#19301D"
                                strokeWidth="2.6"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />
                            <path
                                d={outPath}
                                fill="none"
                                stroke="#A7E34A"
                                strokeWidth="2.2"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />

                            {trafficData.map((pt, idx) => (
                                <g
                                    key={idx}
                                    style={{ cursor: "pointer" }}
                                    onClick={() => setActivePointIndex(idx)}
                                >
                                    <circle
                                        cx={pt.x}
                                        cy={pt.yOut}
                                        r="3.5"
                                        fill="#A7E34A"
                                        stroke="#FFFFFF"
                                        strokeWidth="1.5"
                                    />
                                    <circle
                                        cx={pt.x}
                                        cy={pt.yIn}
                                        r={
                                            activePointIndex === idx
                                                ? "5.5"
                                                : "3.5"
                                        }
                                        fill="#19301D"
                                        stroke={
                                            activePointIndex === idx
                                                ? "#B8F34A"
                                                : "#FFFFFF"
                                        }
                                        strokeWidth={
                                            activePointIndex === idx
                                                ? "2.5"
                                                : "1.5"
                                        }
                                    />
                                    <text
                                        x={pt.x}
                                        y="174"
                                        textAnchor="middle"
                                        fontSize="10.5"
                                        fill={
                                            activePointIndex === idx
                                                ? "#172018"
                                                : "#718071"
                                        }
                                        fontWeight={
                                            activePointIndex === idx
                                                ? "800"
                                                : "500"
                                        }
                                    >
                                        {pt.time}
                                    </text>
                                </g>
                            ))}
                        </svg>

                        {currentPoint && (
                            <div
                                className="bento-tooltip-box"
                                style={{
                                    left: `${(currentPoint.x / 600) * 100}%`,
                                    top: `${(currentPoint.yIn / 180) * 100}%`,
                                }}
                            >
                                <span
                                    style={{
                                        color: "#19301D",
                                        fontWeight: 800,
                                    }}
                                >
                                    {currentPoint.time}
                                </span>
                                <span
                                    style={{
                                        margin: "0 6px",
                                        color: "#E1E7DE",
                                    }}
                                >
                                    |
                                </span>
                                <span style={{ color: "#19301D" }}>
                                    Vào:{" "}
                                    <strong>{currentPoint.inCount} xe</strong>
                                </span>
                                <span
                                    style={{
                                        margin: "0 6px",
                                        color: "#E1E7DE",
                                    }}
                                >
                                    |
                                </span>
                                <span style={{ color: "#718071" }}>
                                    Ra:{" "}
                                    <strong>{currentPoint.outCount} xe</strong>
                                </span>
                            </div>
                        )}
                    </div>
                </div>

                <div
                    className="bento-card"
                    style={{
                        display: "flex",
                        flexDirection: "column",
                        justifyContent: "space-between",
                    }}
                >
                    <div>
                        <div className="bento-card-header">
                            <div className="bento-card-title-wrap">
                                <div
                                    style={{
                                        width: 22,
                                        height: 22,
                                        borderRadius: "50%",
                                        background: "#EAF7EE",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        color: "#19301D",
                                    }}
                                >
                                    <svg
                                        width="13"
                                        height="13"
                                        viewBox="0 0 24 24"
                                        fill="none"
                                        stroke="currentColor"
                                        strokeWidth="2.5"
                                    >
                                        <circle cx="12" cy="12" r="10" />
                                        <path d="M12 2a10 10 0 0 1 10 10" />
                                    </svg>
                                </div>
                                <h3 className="bento-card-title">
                                    Cơ cấu doanh thu theo phương thức
                                </h3>
                            </div>
                            <span
                                style={{
                                    fontSize: 11.5,
                                    fontWeight: 700,
                                    color: "#269B62",
                                    background: "#EAF7EE",
                                    padding: "2px 8px",
                                    borderRadius: 999,
                                }}
                            >
                                Online 88%
                            </span>
                        </div>

                        <div className="bento-pay-item">
                            <div className="bento-pay-header">
                                <span>Ví ParkWise / VNPay (QR)</span>
                                <span>
                                    16.230.000 đ{" "}
                                    <span className="bento-pay-sub">(88%)</span>
                                </span>
                            </div>
                            <div className="bento-pay-bar-wrap">
                                <div
                                    className="bento-pay-bar-fill"
                                    style={{ width: "88%" }}
                                />
                            </div>
                        </div>

                        <div
                            className="bento-pay-item"
                            style={{ marginBottom: 16 }}
                        >
                            <div className="bento-pay-header">
                                <span style={{ color: "#718071" }}>
                                    Tiền mặt tại bốt trực
                                </span>
                                <span style={{ color: "#718071" }}>
                                    2.220.000 đ{" "}
                                    <span className="bento-pay-sub">(12%)</span>
                                </span>
                            </div>
                            <div className="bento-pay-bar-wrap">
                                <div
                                    className="bento-pay-bar-fill cash"
                                    style={{ width: "12%" }}
                                />
                            </div>
                        </div>
                    </div>

                    <div className="bento-highlight-banner">
                        <div>
                            <div
                                style={{
                                    fontSize: 11,
                                    color: "#718071",
                                    fontWeight: 600,
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 4,
                                    marginBottom: 2,
                                }}
                            >
                                <span style={{ color: "#269B62" }}>↗</span> Tỷ
                                lệ thanh toán thành công
                            </div>
                            <div
                                style={{
                                    fontSize: 16,
                                    fontWeight: 850,
                                    color: "#172018",
                                }}
                            >
                                98.6%{" "}
                                <span
                                    style={{
                                        fontSize: 12,
                                        fontWeight: 600,
                                        color: "#718071",
                                    }}
                                >
                                    không ùn tắc
                                </span>
                            </div>
                        </div>

                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 12,
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    alignItems: "flex-end",
                                    gap: 3,
                                    height: 24,
                                }}
                            >
                                <span
                                    style={{
                                        width: 4,
                                        height: 10,
                                        background: "#C6E8AF",
                                        borderRadius: 1,
                                    }}
                                />
                                <span
                                    style={{
                                        width: 4,
                                        height: 14,
                                        background: "#A7E34A",
                                        borderRadius: 1,
                                    }}
                                />
                                <span
                                    style={{
                                        width: 4,
                                        height: 18,
                                        background: "#64B82A",
                                        borderRadius: 1,
                                    }}
                                />
                                <span
                                    style={{
                                        width: 4,
                                        height: 22,
                                        background: "#269B62",
                                        borderRadius: 1,
                                    }}
                                />
                            </div>

                            <span
                                style={{
                                    fontSize: 11.5,
                                    fontWeight: 750,
                                    color: "#172018",
                                    padding: "5px 10px",
                                    background: "#FFFFFF",
                                    border: "1px solid #E1E7DE",
                                    borderRadius: 6,
                                }}
                            >
                                Mẫu báo cáo →
                            </span>
                        </div>
                    </div>
                </div>
            </div>

            {/* ── ROW 3: BẢNG PHIÊN XE RA/VÀO TRỰC TIẾP TẠI BỐT KIỂM SOÁT ── */}
            <div
                className="bento-card"
                style={{ padding: 0, overflow: "hidden" }}
            >
                <div
                    style={{
                        padding: "16px 20px",
                        borderBottom: "1px solid #E1E7DE",
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        flexWrap: "wrap",
                        gap: 12,
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                        }}
                    >
                        <div
                            style={{
                                width: 22,
                                height: 22,
                                borderRadius: "50%",
                                background: "#EAF7EE",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                color: "#19301D",
                            }}
                        >
                            <svg
                                width="13"
                                height="13"
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2.5"
                            >
                                <rect
                                    x="3"
                                    y="11"
                                    width="18"
                                    height="11"
                                    rx="2"
                                />
                                <path d="M7 11V7a5 5 0 0 1 10 0v4" />
                            </svg>
                        </div>
                        <h3 className="bento-card-title">
                            Phiên xe ra/vào trực tiếp tại bốt kiểm soát
                        </h3>
                        <span
                            style={{
                                fontSize: 11.5,
                                fontWeight: 700,
                                color: "#269B62",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 5,
                            }}
                        >
                            <span
                                style={{
                                    width: 6,
                                    height: 6,
                                    borderRadius: "50%",
                                    background: "#269B62",
                                }}
                            />
                            Đang hoạt động
                        </span>
                    </div>

                    <span
                        style={{
                            fontSize: 12.5,
                            fontWeight: 700,
                            color: "#718071",
                        }}
                    >
                        Xem tất cả 476 phiên (Mẫu)
                    </span>
                </div>

                <div className="bento-table-wrap">
                    <table className="bento-table">
                        <thead>
                            <tr>
                                <th>MÃ PHIÊN</th>
                                <th>BIỂN SỐ XE</th>
                                <th>LOẠI XE</th>
                                <th>VỊ TRÍ Ô</th>
                                <th>GIỜ VÀO</th>
                                <th>THỜI LƯỢNG</th>
                                <th>PHÍ TẠM TÍNH</th>
                                <th>TRẠNG THÁI</th>
                            </tr>
                        </thead>
                        <tbody>
                            {SAMPLE_SESSIONS.map((row) => (
                                <tr key={row.id}>
                                    <td
                                        style={{
                                            fontWeight: 800,
                                            color: "#19301D",
                                        }}
                                    >
                                        {row.id}
                                    </td>
                                    <td>
                                        <span
                                            style={{
                                                padding: "3px 8px",
                                                background: "#F6F8F4",
                                                border: "1px solid #E1E7DE",
                                                borderRadius: 6,
                                                fontWeight: 750,
                                            }}
                                        >
                                            {row.plate}
                                        </span>
                                    </td>
                                    <td
                                        style={{
                                            color: "#718071",
                                            display: "flex",
                                            alignItems: "center",
                                            gap: 6,
                                        }}
                                    >
                                        <svg
                                            width="13"
                                            height="13"
                                            viewBox="0 0 24 24"
                                            fill="none"
                                            stroke="#718071"
                                            strokeWidth="2"
                                        >
                                            <rect
                                                x="2"
                                                y="5"
                                                width="20"
                                                height="14"
                                                rx="2"
                                            />
                                            <circle cx="7" cy="15" r="2" />
                                            <circle cx="17" cy="15" r="2" />
                                        </svg>
                                        <span>{row.type}</span>
                                    </td>
                                    <td
                                        style={{
                                            fontWeight: 800,
                                            color: "#19301D",
                                        }}
                                    >
                                        {row.slot}
                                    </td>
                                    <td style={{ color: "#718071" }}>
                                        {row.time}
                                    </td>
                                    <td>{row.duration}</td>
                                    <td
                                        style={{
                                            fontWeight: 800,
                                            color: "#172018",
                                        }}
                                    >
                                        {row.fee}
                                    </td>
                                    <td>
                                        {row.status === "active" && (
                                            <span
                                                style={{
                                                    background: "#EAF7EE",
                                                    color: "#269B62",
                                                    padding: "3px 9px",
                                                    borderRadius: 99,
                                                    fontSize: 11.5,
                                                    fontWeight: 750,
                                                }}
                                            >
                                                Đang đỗ
                                            </span>
                                        )}
                                        {row.status === "overstay" && (
                                            <span
                                                style={{
                                                    background: "#FDEEEE",
                                                    color: "#DC4545",
                                                    padding: "3px 9px",
                                                    borderRadius: 99,
                                                    fontSize: 11.5,
                                                    fontWeight: 750,
                                                }}
                                            >
                                                Quá giờ
                                            </span>
                                        )}
                                        {row.status === "completed" && (
                                            <span
                                                style={{
                                                    background: "#F1F5F9",
                                                    color: "#64748B",
                                                    padding: "3px 9px",
                                                    borderRadius: 99,
                                                    fontSize: 11.5,
                                                    fontWeight: 750,
                                                }}
                                            >
                                                Đã ra bãi
                                            </span>
                                        )}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            </div>
        </div>
    );
}
