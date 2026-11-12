"use client";

import React, { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { useStaffData } from "./staffData";

interface NotificationItem {
    id: string;
    type: "ocr" | "session" | "barrier" | "system";
    titleVi: string;
    titleEn: string;
    descVi: string;
    descEn: string;
    timeVi: string;
    timeEn: string;
    href: string;
    read: boolean;
}

const INITIAL_NOTIFICATIONS: NotificationItem[] = [
    {
        id: "staff-notif-1",
        type: "ocr",
        titleVi: "Cần xác thực biển số xe OCR",
        titleEn: "OCR plate verification needed",
        descVi: "Biển số xe 51K-892.41 cần xác nhận thủ công tại Cổng Vào 01.",
        descEn: "Vehicle plate 51K-892.41 requires manual verification at Gate In 01.",
        timeVi: "5 phút trước",
        timeEn: "5m ago",
        href: "/staff/verification",
        read: false,
    },
    {
        id: "staff-notif-2",
        type: "ocr",
        titleVi: "Cảnh báo biển số xe bị mờ",
        titleEn: "Blurry plate warning",
        descVi: "Camera ANPR Cổng Vào phát hiện biển số mờ xe 30G-991.12.",
        descEn: "Gate In ANPR camera detected blurry plate for 30G-991.12.",
        timeVi: "12 phút trước",
        timeEn: "12m ago",
        href: "/staff/verification",
        read: false,
    },
    {
        id: "staff-notif-3",
        type: "session",
        titleVi: "Phương tiện đỗ quá hạn 24 giờ",
        titleEn: "Vehicle parked over 24 hours",
        descVi: "Xe ô tô 50E-124.99 đã gửi vượt quá 24h tại vị trí B1-A12.",
        descEn: "Vehicle 50E-124.99 has exceeded 24 hours at slot B1-A12.",
        timeVi: "45 phút trước",
        timeEn: "45m ago",
        href: "/staff/sessions",
        read: false,
    },
    {
        id: "staff-notif-4",
        type: "barrier",
        titleVi: "Hệ thống Barrier tự động sẵn sàng",
        titleEn: "Automated barrier system online",
        descVi: "Cổng Vào 01 và Cổng Ra 02 đã đồng bộ thời gian thực.",
        descEn: "Gate In 01 and Gate Out 02 are in sync in real time.",
        timeVi: "2 giờ trước",
        timeEn: "2h ago",
        href: "/staff/dashboard",
        read: true,
    },
];

export function StaffNotifications() {
    const { language } = useLanguage();
    const isVi = language === "vi";
    const [isOpen, setIsOpen] = useState(false);
    const [items, setItems] = useState<NotificationItem[]>(
        INITIAL_NOTIFICATIONS,
    );
    const dropdownRef = useRef<HTMLDivElement>(null);

    const { issues } = useStaffData();
    const pendingIssuesCount = issues.length || 2;

    const unreadCount = items.filter((n) => !n.read).length;

    // Close on click outside
    useEffect(() => {
        function handleClickOutside(event: MouseEvent) {
            if (
                dropdownRef.current &&
                !dropdownRef.current.contains(event.target as Node)
            ) {
                setIsOpen(false);
            }
        }
        if (isOpen) {
            document.addEventListener("mousedown", handleClickOutside);
        }
        return () => {
            document.removeEventListener("mousedown", handleClickOutside);
        };
    }, [isOpen]);

    function markAllAsRead() {
        setItems((prev) => prev.map((item) => ({ ...item, read: true })));
    }

    function markAsRead(id: string) {
        setItems((prev) =>
            prev.map((item) =>
                item.id === id ? { ...item, read: true } : item,
            ),
        );
        setIsOpen(false);
    }

    return (
        <div
            ref={dropdownRef}
            className="staff-notifications-container"
            style={{ position: "relative", display: "inline-block" }}
        >
            <button
                type="button"
                className="staff-notifications-btn"
                onClick={() => setIsOpen(!isOpen)}
                aria-label={isVi ? "Thông báo" : "Notifications"}
                aria-expanded={isOpen}
                style={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: "6px",
                    background: "#ffffff",
                    border: "1px solid #e2e8f0",
                    padding: "7px 12px",
                    borderRadius: "10px",
                    color: "#1e293b",
                    fontSize: "13px",
                    fontWeight: 600,
                    cursor: "pointer",
                    boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)",
                    transition: "all 0.15s ease",
                }}
            >
                <span style={{ position: "relative", display: "inline-flex" }}>
                    <svg
                        width={17}
                        height={17}
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.8"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        aria-hidden="true"
                    >
                        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                        <path d="M13.73 21a2 2 0 0 1-3.46 0" />
                    </svg>
                    {unreadCount > 0 && (
                        <span
                            style={{
                                position: "absolute",
                                top: "-6px",
                                right: "-7px",
                                background: "#ef4444",
                                color: "#ffffff",
                                fontSize: "10px",
                                fontWeight: 800,
                                minWidth: "16px",
                                height: "16px",
                                borderRadius: "999px",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                padding: "0 3px",
                                border: "1.5px solid #ffffff",
                            }}
                        >
                            {unreadCount}
                        </span>
                    )}
                </span>
                <span>{isVi ? "Thông báo" : "Notifications"}</span>
            </button>

            {/* Notification Dropdown */}
            {isOpen && (
                <div
                    className="staff-notifications-dropdown"
                    style={{
                        position: "absolute",
                        top: "calc(100% + 8px)",
                        right: 0,
                        width: "360px",
                        maxWidth: "90vw",
                        background: "#ffffff",
                        borderRadius: "12px",
                        boxShadow:
                            "0 10px 30px rgba(0, 0, 0, 0.12), 0 1px 4px rgba(0, 0, 0, 0.06)",
                        border: "1px solid #e2e8f0",
                        zIndex: 9999,
                        overflow: "hidden",
                        textAlign: "left",
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                            padding: "12px 16px",
                            borderBottom: "1px solid #f1f5f9",
                            background: "#fafafa",
                        }}
                    >
                        <strong style={{ fontSize: "14px", color: "#0f172a" }}>
                            🔔{" "}
                            {isVi
                                ? "Thông báo trạm trực"
                                : "Station Notifications"}
                        </strong>
                        {unreadCount > 0 && (
                            <button
                                type="button"
                                onClick={markAllAsRead}
                                style={{
                                    background: "none",
                                    border: "none",
                                    color: "#2563eb",
                                    fontSize: "12px",
                                    fontWeight: 600,
                                    cursor: "pointer",
                                    padding: "2px 4px",
                                }}
                            >
                                {isVi ? "Đã đọc tất cả" : "Mark all as read"}
                            </button>
                        )}
                    </div>

                    <div style={{ maxHeight: "340px", overflowY: "auto" }}>
                        {items.length === 0 ? (
                            <p
                                style={{
                                    padding: "24px",
                                    textAlign: "center",
                                    color: "#94a3b8",
                                    fontSize: "13px",
                                    margin: 0,
                                }}
                            >
                                {isVi
                                    ? "Không có thông báo mới"
                                    : "No new notifications"}
                            </p>
                        ) : (
                            items.map((item) => (
                                <Link
                                    key={item.id}
                                    href={item.href}
                                    onClick={() => markAsRead(item.id)}
                                    style={{
                                        display: "block",
                                        padding: "12px 16px",
                                        borderBottom: "1px solid #f1f5f9",
                                        background: item.read
                                            ? "#ffffff"
                                            : "#f8fafc",
                                        textDecoration: "none",
                                        transition: "background 0.15s ease",
                                    }}
                                >
                                    <div
                                        style={{
                                            display: "flex",
                                            justifyContent: "space-between",
                                            alignItems: "baseline",
                                            marginBottom: "3px",
                                        }}
                                    >
                                        <span
                                            style={{
                                                fontSize: "13px",
                                                fontWeight: item.read
                                                    ? 600
                                                    : 700,
                                                color: item.read
                                                    ? "#334155"
                                                    : "#0f172a",
                                            }}
                                        >
                                            {item.type === "ocr"
                                                ? "⚠️ "
                                                : item.type === "session"
                                                  ? "🚗 "
                                                  : item.type === "barrier"
                                                    ? "⚡ "
                                                    : "🔔 "}
                                            {isVi ? item.titleVi : item.titleEn}
                                        </span>
                                        <small
                                            style={{
                                                fontSize: "11px",
                                                color: "#94a3b8",
                                            }}
                                        >
                                            {isVi ? item.timeVi : item.timeEn}
                                        </small>
                                    </div>
                                    <p
                                        style={{
                                            margin: 0,
                                            fontSize: "12px",
                                            color: "#64748b",
                                            lineHeight: "1.4",
                                        }}
                                    >
                                        {isVi ? item.descVi : item.descEn}
                                    </p>
                                </Link>
                            ))
                        )}
                    </div>

                    <div
                        style={{
                            padding: "10px 16px",
                            background: "#fafafa",
                            borderTop: "1px solid #f1f5f9",
                            display: "flex",
                            justifyContent: "space-between",
                            alignItems: "center",
                            fontSize: "12px",
                        }}
                    >
                        <Link
                            href="/staff/verification"
                            onClick={() => setIsOpen(false)}
                            style={{
                                color: "#0f172a",
                                fontWeight: 600,
                                textDecoration: "none",
                            }}
                        >
                            {isVi
                                ? `Xử lý sự cố OCR (${pendingIssuesCount}) →`
                                : `Review OCR issues (${pendingIssuesCount}) →`}
                        </Link>
                    </div>
                </div>
            )}
        </div>
    );
}
