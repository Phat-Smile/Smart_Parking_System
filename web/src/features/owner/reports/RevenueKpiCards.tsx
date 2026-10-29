"use client";

import React from "react";
import { RevenueKpi } from "./revenueAdapter";

interface RevenueKpiCardsProps {
    kpis: RevenueKpi;
}

export function RevenueKpiCards({ kpis }: RevenueKpiCardsProps) {
    const cards = [
        {
            label: "Tổng doanh thu",
            value: kpis.totalRevenueText,
            change: "15,2% ↑",
            changeColor: "#16a36a",
            icon: (
                <svg
                    width="22"
                    height="22"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                >
                    <line x1="12" y1="1" x2="12" y2="23" />
                    <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                </svg>
            ),
        },
        {
            label: "Doanh thu trung bình",
            value: kpis.avgDailyText,
            change: "8,4% ↑",
            changeColor: "#16a36a",
            icon: (
                <svg
                    width="22"
                    height="22"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                >
                    <polyline points="22 12 18 12 15 21 9 3 6 12 2 12" />
                </svg>
            ),
        },
        {
            label: "Tổng lượt giao dịch",
            value: kpis.totalTransactions.toLocaleString("vi-VN"),
            change: "11,0% ↑",
            changeColor: "#16a36a",
            icon: (
                <svg
                    width="22"
                    height="22"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                >
                    <rect x="2" y="5" width="20" height="14" rx="2" />
                    <line x1="2" y1="10" x2="22" y2="10" />
                </svg>
            ),
        },
        {
            label: "Tỷ lệ thanh toán online",
            value: `${kpis.onlineRatePct}%`,
            change: "95% mục tiêu",
            changeColor: "#2563eb",
            icon: (
                <svg
                    width="22"
                    height="22"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                >
                    <path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />
                </svg>
            ),
        },
    ];

    return (
        <div
            style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
                gap: 16,
                marginBottom: 20,
            }}
        >
            {cards.map((c, i) => (
                <div
                    key={i}
                    className="ov2-card"
                    style={{
                        background: "#fff",
                        borderRadius: 18,
                        border: "1px solid #e3e8f2",
                        padding: 22,
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "flex-start",
                        }}
                    >
                        <div
                            style={{
                                width: 44,
                                height: 44,
                                borderRadius: 12,
                                background: "#eefdf5",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                color: "#16a36a",
                            }}
                        >
                            {c.icon}
                        </div>
                    </div>

                    <div style={{ marginTop: 18 }}>
                        <div
                            style={{
                                fontSize: 30,
                                fontWeight: 800,
                                color: "#0f1b3d",
                                lineHeight: 1.1,
                            }}
                        >
                            {c.value}
                        </div>
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                                marginTop: 6,
                            }}
                        >
                            <span style={{ fontSize: 13, color: "#6a7491" }}>
                                {c.label}
                            </span>
                            <span
                                style={{
                                    fontSize: 13,
                                    fontWeight: 700,
                                    color: c.changeColor,
                                }}
                            >
                                {c.change}
                            </span>
                        </div>
                    </div>
                </div>
            ))}
        </div>
    );
}
