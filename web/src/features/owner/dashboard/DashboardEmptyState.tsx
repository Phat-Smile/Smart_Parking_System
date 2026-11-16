"use client";

import React from "react";
import Link from "next/link";

interface DashboardEmptyStateProps {
    title?: string;
    description?: string;
    actionHref?: string;
    actionLabel?: string;
}

export function DashboardEmptyState({
    title = "Chưa phát sinh lượt đỗ xe trong ngày",
    description = "Hệ thống bốt kiểm soát & Camera OCR sẽ tự động hiển thị phiên gửi xe và cập nhật doanh thu thời gian thực ngay khi có phương tiện check-in.",
    actionHref = "/owner/parking-lots",
    actionLabel = "Quản lý cơ sở bãi xe →",
}: DashboardEmptyStateProps) {
    return (
        <div
            style={{
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                padding: "36px 20px",
                textAlign: "center",
                background: "#FAFBF9",
                borderRadius: 14,
                border: "1.5px dashed #D6DFD3",
                margin: "8px 0",
            }}
        >
            <div
                style={{
                    width: 44,
                    height: 44,
                    borderRadius: "50%",
                    background: "#EAF7EE",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    color: "#19301D",
                    marginBottom: 12,
                }}
            >
                <svg
                    width="22"
                    height="22"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                >
                    <rect x="2" y="5" width="20" height="14" rx="2" />
                    <circle cx="7" cy="15" r="2" />
                    <circle cx="17" cy="15" r="2" />
                </svg>
            </div>
            <h4
                style={{
                    fontSize: 14.5,
                    fontWeight: 750,
                    color: "#172018",
                    margin: "0 0 6px",
                }}
            >
                {title}
            </h4>
            <p
                style={{
                    fontSize: 12.5,
                    color: "#718071",
                    maxWidth: 420,
                    lineHeight: 1.5,
                    margin: "0 0 14px",
                }}
            >
                {description}
            </p>
            {actionHref && actionLabel && (
                <Link
                    href={actionHref}
                    style={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 6,
                        background: "#19301D",
                        color: "#B8F34A",
                        fontSize: 12,
                        fontWeight: 750,
                        padding: "6px 14px",
                        borderRadius: 8,
                        textDecoration: "none",
                        transition: "opacity 0.2s ease",
                    }}
                >
                    {actionLabel}
                </Link>
            )}
        </div>
    );
}
