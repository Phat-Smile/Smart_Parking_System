"use client";

import React from "react";
import Link from "next/link";

interface ActivityItemV2 {
    id: string;
    plate: string;
    method: string;
    timeAgo: string;
    status: "Đang đỗ" | "Đã ra";
}

const ACTIVITIES: ActivityItemV2[] = [];

export function RecentActivityV2() {
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
            {/* Header */}
            <div
                style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
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
                    Hoạt động gần đây
                </h2>
                <Link
                    href="/owner/sessions"
                    style={{
                        border: "1px solid #e3e8f2",
                        background: "#fff",
                        borderRadius: 8,
                        padding: "5px 12px",
                        fontSize: 12.5,
                        fontWeight: 600,
                        color: "#0f1b3d",
                        textDecoration: "none",
                    }}
                >
                    Xem tất cả
                </Link>
            </div>

            {/* List */}
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                {ACTIVITIES.length === 0 ? (
                    <p
                        style={{
                            textAlign: "center",
                            color: "#6a7491",
                            padding: "24px 0",
                            fontSize: 13.5,
                        }}
                    >
                        Chưa có hoạt động đỗ xe
                    </p>
                ) : (
                    ACTIVITIES.map((act) => {
                        const isParked = act.status === "Đang đỗ";
                        return (
                            <div
                                key={act.id}
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "space-between",
                                    padding: "8px 0",
                                    borderBottom: "1px solid #f5f7fa",
                                }}
                            >
                                {/* Left: Car Icon + Plate + Method */}
                                <div
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 12,
                                    }}
                                >
                                    <div
                                        style={{
                                            width: 36,
                                            height: 36,
                                            borderRadius: 8,
                                            background: "#f3f6fb",
                                            display: "flex",
                                            alignItems: "center",
                                            justifyContent: "center",
                                            color: "#6a7491",
                                        }}
                                    >
                                        <svg
                                            width="18"
                                            height="18"
                                            viewBox="0 0 24 24"
                                            fill="none"
                                            stroke="currentColor"
                                            strokeWidth="2"
                                        >
                                            <path d="M19 17h2c.6 0 1-.4 1-1v-3c0-.9-.7-1.7-1.5-1.9C18.7 10.6 16 10 16 10s-1.3-1.4-2.2-2.3c-.5-.4-1.1-.7-1.8-.7H5c-.6 0-1.1.4-1.4.9l-1.4 2.9A3.7 3.7 0 0 0 2 12v4c0 .6.4 1 1 1h2" />
                                            <circle cx="7" cy="17" r="2" />
                                            <path d="M9 17h6" />
                                            <circle cx="17" cy="17" r="2" />
                                        </svg>
                                    </div>
                                    <div>
                                        <div
                                            style={{
                                                fontSize: 13.5,
                                                fontWeight: 700,
                                                color: "#0f1b3d",
                                            }}
                                        >
                                            {act.plate}
                                        </div>
                                        <div
                                            style={{
                                                fontSize: 11.5,
                                                color: "#6a7491",
                                            }}
                                        >
                                            {act.method}
                                        </div>
                                    </div>
                                </div>

                                {/* Right: Time ago + Status Badge */}
                                <div
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 12,
                                    }}
                                >
                                    <span
                                        style={{ fontSize: 12, color: "#6a7491" }}
                                    >
                                        {act.timeAgo}
                                    </span>
                                    <span
                                        style={{
                                            background: isParked
                                                ? "#fde8e8"
                                                : "#def7ec",
                                            color: isParked ? "#e5484d" : "#0e9f6e",
                                            borderRadius: 999,
                                            padding: "3px 10px",
                                            fontSize: 11.5,
                                            fontWeight: 700,
                                            minWidth: 64,
                                            textAlign: "center",
                                        }}
                                    >
                                        {act.status}
                                    </span>
                                </div>
                            </div>
                        );
                    })
                )}
            </div>
        </div>
    );
}
