"use client";

import React from "react";
import { PeakTimeItem } from "./revenueAdapter";

interface PeakTimeListProps {
    items: PeakTimeItem[];
}

export function PeakTimeList({ items }: PeakTimeListProps) {
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
                Khung giờ cao điểm
            </h2>

            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
                {items.map((pt, idx) => (
                    <div
                        key={idx}
                        style={{
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                            padding: "10px 14px",
                            background: "#f8faff",
                            borderRadius: 12,
                            border: "1px solid #eef2fa",
                        }}
                    >
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 10,
                            }}
                        >
                            <span
                                style={{
                                    width: 24,
                                    height: 24,
                                    borderRadius: "50%",
                                    backgroundColor:
                                        idx === 0 ? "#2563eb" : "#e3e8f2",
                                    color: idx === 0 ? "#fff" : "#6a7491",
                                    fontSize: 12,
                                    fontWeight: 700,
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                }}
                            >
                                {idx + 1}
                            </span>
                            <div>
                                <div
                                    style={{
                                        fontSize: 13.5,
                                        fontWeight: 700,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    {pt.timeRange}
                                </div>
                                <div
                                    style={{ fontSize: 11.5, color: "#6a7491" }}
                                >
                                    {pt.sessions} lượt xe vào
                                </div>
                            </div>
                        </div>

                        <div
                            style={{
                                fontSize: 14,
                                fontWeight: 800,
                                color: "#16a36a",
                            }}
                        >
                            {pt.revenueText}
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
}
