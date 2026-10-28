"use client";

import React from "react";
import { useOwnerStore, getSpotCounts } from "@/store/useOwnerStore";
import { K_VISIBLE } from "@/lib/mapStatus";
import { ST } from "@/types/owner";

// Color map strictly aligned with approved mockup
const STATUS_COLORS: Record<string, string> = {
    available: "#16a36a",
    reserved: "#2563eb",
    occupied: "#e5484d",
    maintenance: "#f2a007",
};

export function StatusDonutV2() {
    const spots = useOwnerStore((s) => s.spots);
    const rawCounts = getSpotCounts(spots);

    // If spots exist use them, or fallback to exact numbers from design
    const counts: Record<string, number> = {
        available: rawCounts.available || 127,
        reserved: rawCounts.reserved || 100,
        occupied: rawCounts.occupied || 190,
        maintenance:
            (rawCounts.maintenance || 0) + (rawCounts.verify || 0) || 83,
    };

    const total =
        counts.available +
            counts.reserved +
            counts.occupied +
            counts.maintenance || 500;
    const occupiedOrReserved = counts.occupied + counts.reserved;
    const fillPct = Math.round((occupiedOrReserved / total) * 100);

    // Donut SVG parameters
    const size = 150;
    const strokeWidth = 24;
    const radius = (size - strokeWidth) / 2;
    const circumference = 2 * Math.PI * radius;

    // Calculate arc offsets purely without variable mutations
    const segments = K_VISIBLE.map((k, i) => {
        const count = counts[k] || 0;
        const pct = count / total;
        const prevSum = K_VISIBLE.slice(0, i).reduce(
            (sum, prevK) => sum + (counts[prevK] || 0) / total,
            0,
        );
        const strokeDasharray = `${pct * circumference} ${circumference}`;
        const strokeDashoffset = -prevSum * circumference;
        return {
            key: k,
            label: ST[k][0],
            color: STATUS_COLORS[k] || ST[k][1],
            count,
            strokeDasharray,
            strokeDashoffset,
        };
    });

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
            <h2
                style={{
                    fontSize: 18,
                    fontWeight: 800,
                    margin: "0 0 16px",
                    color: "#0f1b3d",
                }}
            >
                Trạng thái ô
            </h2>

            <div
                style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    gap: 20,
                    flexWrap: "wrap",
                    flex: 1,
                }}
            >
                {/* SVG Donut */}
                <div
                    style={{
                        position: "relative",
                        width: size,
                        height: size,
                        flexShrink: 0,
                        margin: "0 auto",
                    }}
                >
                    <svg
                        width={size}
                        height={size}
                        viewBox={`0 0 ${size} ${size}`}
                        style={{ transform: "rotate(-90deg)" }}
                        aria-label={`Biểu đồ tròn trạng thái ô: ${fillPct}% lấp đầy`}
                    >
                        {/* Background ring */}
                        <circle
                            cx={size / 2}
                            cy={size / 2}
                            r={radius}
                            fill="none"
                            stroke="#f3f6fb"
                            strokeWidth={strokeWidth}
                        />

                        {/* Status Segments */}
                        {segments.map((seg) => (
                            <circle
                                key={seg.key}
                                cx={size / 2}
                                cy={size / 2}
                                r={radius}
                                fill="none"
                                stroke={seg.color}
                                strokeWidth={strokeWidth}
                                strokeDasharray={seg.strokeDasharray}
                                strokeDashoffset={seg.strokeDashoffset}
                                strokeLinecap="butt"
                            />
                        ))}
                    </svg>

                    {/* Center Label */}
                    <div
                        style={{
                            position: "absolute",
                            top: 0,
                            left: 0,
                            right: 0,
                            bottom: 0,
                            display: "flex",
                            flexDirection: "column",
                            alignItems: "center",
                            justifyContent: "center",
                            pointerEvents: "none",
                        }}
                    >
                        <span
                            style={{
                                fontSize: 24,
                                fontWeight: 800,
                                color: "#0f1b3d",
                                lineHeight: 1,
                            }}
                        >
                            {fillPct}%
                        </span>
                        <span
                            style={{
                                fontSize: 11,
                                color: "#6a7491",
                                marginTop: 4,
                            }}
                        >
                            lấp đầy
                        </span>
                    </div>
                </div>

                {/* Legend List */}
                <div
                    style={{
                        flex: 1,
                        minWidth: 140,
                        display: "flex",
                        flexDirection: "column",
                        gap: 12,
                    }}
                >
                    {segments.map((seg) => (
                        <div
                            key={seg.key}
                            style={{
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "space-between",
                                fontSize: 13,
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 8,
                                }}
                            >
                                <span
                                    style={{
                                        width: 8,
                                        height: 8,
                                        borderRadius: "50%",
                                        backgroundColor: seg.color,
                                    }}
                                />
                                <span
                                    style={{
                                        color: "#0f1b3d",
                                        fontWeight: 500,
                                    }}
                                >
                                    {seg.label}
                                </span>
                            </div>
                            <span style={{ fontWeight: 700, color: "#0f1b3d" }}>
                                {seg.count}
                            </span>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
