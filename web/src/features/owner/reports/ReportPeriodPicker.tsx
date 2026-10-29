"use client";

import React from "react";

interface ReportPeriodPickerProps {
    mode: "day" | "month" | "year";
    dateStr: string;
    onModeChange: (mode: "day" | "month" | "year") => void;
    onDateChange: (dateStr: string) => void;
}

export function ReportPeriodPicker({
    mode,
    dateStr,
    onModeChange,
    onDateChange,
}: ReportPeriodPickerProps) {
    return (
        <div
            style={{
                display: "flex",
                alignItems: "center",
                gap: 12,
                flexWrap: "wrap",
            }}
        >
            {/* Pills: Ngày | Tháng | Năm */}
            <div
                style={{
                    display: "flex",
                    background: "#f3f6fb",
                    borderRadius: 999,
                    padding: 4,
                    border: "1px solid #e3e8f2",
                }}
            >
                {(["day", "month", "year"] as const).map((m) => {
                    const labels = {
                        day: "Theo ngày",
                        month: "Theo tháng",
                        year: "Theo năm",
                    };
                    const isActive = mode === m;
                    return (
                        <button
                            key={m}
                            type="button"
                            onClick={() => onModeChange(m)}
                            style={{
                                border: "none",
                                background: isActive
                                    ? "#2563eb"
                                    : "transparent",
                                color: isActive ? "#fff" : "#6a7491",
                                borderRadius: 999,
                                padding: "6px 14px",
                                fontSize: 12.5,
                                fontWeight: 600,
                                cursor: "pointer",
                                transition: "all 0.15s ease",
                            }}
                        >
                            {labels[m]}
                        </button>
                    );
                })}
            </div>

            {/* Date Picker Input */}
            {mode === "day" && (
                <input
                    type="date"
                    value={dateStr}
                    onChange={(e) => onDateChange(e.target.value)}
                    aria-label="Chọn ngày báo cáo"
                    style={{
                        border: "1px solid #e3e8f2",
                        background: "#fff",
                        borderRadius: 10,
                        padding: "7px 12px",
                        fontSize: 13,
                        color: "#0f1b3d",
                        outline: "none",
                    }}
                />
            )}

            {mode === "month" && (
                <input
                    type="month"
                    value={dateStr.substring(0, 7)}
                    onChange={(e) => onDateChange(`${e.target.value}-01`)}
                    aria-label="Chọn tháng báo cáo"
                    style={{
                        border: "1px solid #e3e8f2",
                        background: "#fff",
                        borderRadius: 10,
                        padding: "7px 12px",
                        fontSize: 13,
                        color: "#0f1b3d",
                        outline: "none",
                    }}
                />
            )}

            {mode === "year" && (
                <select
                    value={dateStr.substring(0, 4)}
                    onChange={(e) => onDateChange(`${e.target.value}-01-01`)}
                    aria-label="Chọn năm báo cáo"
                    style={{
                        border: "1px solid #e3e8f2",
                        background: "#fff",
                        borderRadius: 10,
                        padding: "7px 12px",
                        fontSize: 13,
                        color: "#0f1b3d",
                        outline: "none",
                        fontWeight: 600,
                    }}
                >
                    {["2026", "2025", "2024"].map((yr) => (
                        <option key={yr} value={yr}>
                            Năm {yr}
                        </option>
                    ))}
                </select>
            )}
        </div>
    );
}
