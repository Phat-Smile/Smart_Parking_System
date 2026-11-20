"use client";

import React, { useState, useMemo } from "react";
import {
    getTrafficData,
    TrafficData,
} from "@/features/owner/dashboard/trafficAdapter";

export function TrafficChart() {
    const [period, setPeriod] = useState<"day" | "week" | "month">("week");
    const data: TrafficData = useMemo(() => getTrafficData(period), [period]);

    // SVG Chart Dimensions
    const width = 600;
    const height = 260;
    const padLeft = 45;
    const padRight = 20;
    const padTop = 30;
    const padBottom = 35;

    const chartW = width - padLeft - padRight;
    const chartH = height - padTop - padBottom;

    const maxVal = useMemo(() => {
        let max = 0;
        data.points.forEach((p) => {
            if (p.inCount > max) max = p.inCount;
            if (p.outCount > max) max = p.outCount;
        });
        // Round up nicely
        if (period === "month") return 40000;
        if (period === "day") return 150;
        return 2000;
    }, [data, period]);

    // Coordinates calculation
    const coords = useMemo(() => {
        const n = data.points.length;
        return data.points.map((p, i) => {
            const x = padLeft + (i / Math.max(n - 1, 1)) * chartW;
            const yIn =
                padTop + chartH - Math.max(0, (p.inCount / maxVal) * chartH);
            const yOut =
                padTop + chartH - Math.max(0, (p.outCount / maxVal) * chartH);
            return { x, yIn, yOut, point: p };
        });
    }, [data, chartW, chartH, maxVal]);

    // Build smooth path
    const makeSmoothPath = (pts: { x: number; y: number }[]) => {
        if (pts.length === 0) return "";
        if (pts.length === 1) return `M ${pts[0].x} ${pts[0].y}`;
        let d = `M ${pts[0].x} ${pts[0].y}`;
        for (let i = 0; i < pts.length - 1; i++) {
            const p0 = pts[i];
            const p1 = pts[i + 1];
            const cx = (p0.x + p1.x) / 2;
            d += ` C ${cx} ${p0.y}, ${cx} ${p1.y}, ${p1.x} ${p1.y}`;
        }
        return d;
    };

    const inLinePath = useMemo(
        () => makeSmoothPath(coords.map((c) => ({ x: c.x, y: c.yIn }))),
        [coords],
    );
    const outLinePath = useMemo(
        () => makeSmoothPath(coords.map((c) => ({ x: c.x, y: c.yOut }))),
        [coords],
    );

    const inAreaPath = useMemo(() => {
        if (coords.length === 0) return "";
        const bottom = padTop + chartH;
        return `${inLinePath} L ${coords[coords.length - 1].x} ${bottom} L ${coords[0].x} ${bottom} Z`;
    }, [inLinePath, coords, chartH]);

    const outAreaPath = useMemo(() => {
        if (coords.length === 0) return "";
        const bottom = padTop + chartH;
        return `${outLinePath} L ${coords[coords.length - 1].x} ${bottom} L ${coords[0].x} ${bottom} Z`;
    }, [outLinePath, coords, chartH]);

    // Peak zone index
    const peakCoord = coords.find((c) => c.point.isPeak);

    return (
        <div
            className="ov2-card"
            style={{
                background: "#fff",
                borderRadius: 18,
                border: "1px solid #e3e8f2",
                padding: 24,
                display: "flex",
                flexDirection: "column",
            }}
        >
            {/* Top Header & Period Switcher */}
            <div
                style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    flexWrap: "wrap",
                    gap: 12,
                    marginBottom: 16,
                }}
            >
                <h2
                    style={{
                        fontSize: 18,
                        fontWeight: 800,
                        margin: 0,
                        color: "#0f1b3d",
                    }}
                >
                    Lưu lượng xe
                </h2>

                <div
                    style={{
                        display: "flex",
                        background: "#f3f6fb",
                        borderRadius: 999,
                        padding: 4,
                        gap: 2,
                    }}
                >
                    {(["day", "week", "month"] as const).map((p) => {
                        const labels = {
                            day: "Ngày",
                            week: "Tuần",
                            month: "Tháng",
                        };
                        const isActive = period === p;
                        return (
                            <button
                                key={p}
                                type="button"
                                onClick={() => setPeriod(p)}
                                style={{
                                    border: "none",
                                    background: isActive
                                        ? "#a3e635"
                                        : "transparent",
                                    color: isActive ? "#143806" : "#6a7491",
                                    fontWeight: isActive ? 700 : 500,
                                    fontSize: 13,
                                    padding: "6px 14px",
                                    borderRadius: 999,
                                    cursor: "pointer",
                                    transition: "all 0.15s ease",
                                }}
                            >
                                {labels[p]}
                            </button>
                        );
                    })}
                </div>
            </div>

            {/* Legend & Stats */}
            <div
                style={{
                    display: "flex",
                    flexWrap: "wrap",
                    alignItems: "center",
                    gap: 16,
                    marginBottom: 14,
                    fontSize: 13,
                }}
            >
                <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <span
                        style={{
                            width: 10,
                            height: 10,
                            borderRadius: "50%",
                            backgroundColor: "#2563eb",
                        }}
                    />
                    <span style={{ fontWeight: 700, color: "#0f1b3d" }}>
                        Xe vào
                    </span>
                    <span style={{ color: "#6a7491", fontSize: 12 }}>
                        {data.totalIn.toLocaleString("vi-VN")} lượt ·{" "}
                        {data.dateRangeText}
                    </span>
                </div>

                <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <span
                        style={{
                            width: 10,
                            height: 10,
                            borderRadius: "50%",
                            backgroundColor: "#e5484d",
                        }}
                    />
                    <span style={{ fontWeight: 700, color: "#0f1b3d" }}>
                        Xe ra
                    </span>
                    <span style={{ color: "#6a7491", fontSize: 12 }}>
                        {data.totalOut.toLocaleString("vi-VN")} lượt
                    </span>
                </div>
            </div>

            {/* Quick Pills */}
            <div
                style={{
                    display: "flex",
                    flexWrap: "wrap",
                    gap: 8,
                    marginBottom: 16,
                }}
            >
                <span
                    style={{
                        background: "#f3f6fb",
                        borderRadius: 8,
                        padding: "4px 10px",
                        fontSize: 12,
                        color: "#6a7491",
                    }}
                >
                    Ngày đông nhất:{" "}
                    <strong style={{ color: "#2563eb" }}>
                        {data.peakDayLabel}
                    </strong>
                </span>
                <span
                    style={{
                        background: "#f3f6fb",
                        borderRadius: 8,
                        padding: "4px 10px",
                        fontSize: 12,
                        color: "#6a7491",
                    }}
                >
                    Vào nhiều nhất:{" "}
                    <strong style={{ color: "#2563eb" }}>
                        {data.maxInLabel}
                    </strong>
                </span>
                <span
                    style={{
                        background: "#f3f6fb",
                        borderRadius: 8,
                        padding: "4px 10px",
                        fontSize: 12,
                        color: "#6a7491",
                    }}
                >
                    Ra nhiều nhất:{" "}
                    <strong style={{ color: "#2563eb" }}>
                        {data.maxOutLabel}
                    </strong>
                </span>
            </div>

            {/* SVG Chart */}
            <div
                style={{
                    position: "relative",
                    width: "100%",
                    overflowX: "auto",
                }}
            >
                {data.totalIn === 0 && data.totalOut === 0 && (
                    <div
                        style={{
                            position: "absolute",
                            inset: 0,
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            background: "rgba(255, 255, 255, 0.75)",
                            borderRadius: 12,
                            color: "#6a7491",
                            fontWeight: 600,
                            fontSize: 14,
                            zIndex: 2,
                        }}
                    >
                        Chưa có dữ liệu lưu lượng
                    </div>
                )}
                <svg
                    viewBox={`0 0 ${width} ${height}`}
                    style={{
                        width: "100%",
                        minWidth: 480,
                        height: "auto",
                        display: "block",
                    }}
                    aria-label={`Biểu đồ lưu lượng xe theo ${period}`}
                >
                    <defs>
                        <linearGradient id="inGrad" x1="0" y1="0" x2="0" y2="1">
                            <stop
                                offset="0%"
                                stopColor="#2563eb"
                                stopOpacity="0.22"
                            />
                            <stop
                                offset="100%"
                                stopColor="#2563eb"
                                stopOpacity="0.0"
                            />
                        </linearGradient>
                        <linearGradient
                            id="outGrad"
                            x1="0"
                            y1="0"
                            x2="0"
                            y2="1"
                        >
                            <stop
                                offset="0%"
                                stopColor="#e5484d"
                                stopOpacity="0.18"
                            />
                            <stop
                                offset="100%"
                                stopColor="#e5484d"
                                stopOpacity="0.0"
                            />
                        </linearGradient>
                    </defs>

                    {/* Grid lines (horizontal) */}
                    {[0, 0.25, 0.5, 0.75, 1].map((pct, idx) => {
                        const y = padTop + chartH * (1 - pct);
                        const val = Math.round(maxVal * pct);
                        return (
                            <g key={idx}>
                                <line
                                    x1={padLeft}
                                    y1={y}
                                    x2={width - padRight}
                                    y2={y}
                                    stroke="#f0f2f5"
                                    strokeDasharray="4 4"
                                />
                                <text
                                    x={padLeft - 8}
                                    y={y + 4}
                                    textAnchor="end"
                                    fontSize="11"
                                    fill="#9aa2b4"
                                >
                                    {val >= 1000
                                        ? (val / 1000).toFixed(
                                              val % 1000 === 0 ? 0 : 1,
                                          ) + ".000"
                                        : val}
                                </text>
                            </g>
                        );
                    })}

                    {/* Peak zone vertical highlight */}
                    {peakCoord && (
                        <g>
                            <rect
                                x={peakCoord.x - 28}
                                y={padTop}
                                width={56}
                                height={chartH}
                                fill="#f0f4ff"
                                rx={8}
                                opacity={0.8}
                            />
                            <rect
                                x={peakCoord.x - 26}
                                y={padTop - 18}
                                width={52}
                                height={18}
                                rx={9}
                                fill="#2563eb"
                            />
                            <text
                                x={peakCoord.x}
                                y={padTop - 6}
                                textAnchor="middle"
                                fontSize="10"
                                fontWeight="700"
                                fill="#fff"
                            >
                                Cao điểm
                            </text>
                        </g>
                    )}

                    {/* Filled Areas */}
                    <path d={inAreaPath} fill="url(#inGrad)" />
                    <path d={outAreaPath} fill="url(#outGrad)" />

                    {/* Lines */}
                    <path
                        d={inLinePath}
                        fill="none"
                        stroke="#2563eb"
                        strokeWidth="2.5"
                        strokeLinecap="round"
                    />
                    <path
                        d={outLinePath}
                        fill="none"
                        stroke="#e5484d"
                        strokeWidth="2.5"
                        strokeLinecap="round"
                    />

                    {/* Dots on line points */}
                    {coords.map((c, i) => {
                        // For day view, don't show every dot to avoid crowding
                        if (period === "day" && i % 3 !== 0 && !c.point.isPeak)
                            return null;
                        return (
                            <g key={i}>
                                <circle
                                    cx={c.x}
                                    cy={c.yIn}
                                    r="4"
                                    fill="#fff"
                                    stroke="#2563eb"
                                    strokeWidth="2"
                                />
                                <circle
                                    cx={c.x}
                                    cy={c.yOut}
                                    r="3.5"
                                    fill="#fff"
                                    stroke="#e5484d"
                                    strokeWidth="2"
                                />
                            </g>
                        );
                    })}

                    {/* X Axis Labels */}
                    {coords.map((c, i) => {
                        // In day view show every 2 or 3 hours
                        if (
                            period === "day" &&
                            i % 2 !== 0 &&
                            i !== coords.length - 1
                        )
                            return null;
                        return (
                            <text
                                key={i}
                                x={c.x}
                                y={height - 12}
                                textAnchor="middle"
                                fontSize="11"
                                fill={c.point.isPeak ? "#0f1b3d" : "#6a7491"}
                                fontWeight={c.point.isPeak ? "700" : "500"}
                            >
                                {c.point.label}
                            </text>
                        );
                    })}
                </svg>
            </div>
        </div>
    );
}
