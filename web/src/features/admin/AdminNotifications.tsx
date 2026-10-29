"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { AdminIcon } from "./AdminUi";
import { countByStatus, useAdminLots } from "./adminLotsData";

interface NotificationItem {
    id: string;
    type: "lot" | "application" | "assignment" | "system";
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
        id: "notif-1",
        type: "lot",
        titleVi: "Bãi đỗ xe chờ phê duyệt",
        titleEn: "Parking lot awaiting approval",
        descVi: "Vincom Center Basement vừa nộp hồ sơ xin phê duyệt mở bãi.",
        descEn: "Vincom Center Basement submitted for review.",
        timeVi: "15 phút trước",
        timeEn: "15m ago",
        href: "/admin/parking-lots/pending",
        read: false,
    },
    {
        id: "notif-2",
        type: "application",
        titleVi: "Hồ sơ chủ bãi mới",
        titleEn: "New owner application",
        descVi: "Lê Thành Công vừa nộp hồ sơ doanh nghiệp kèm đầy đủ giấy tờ.",
        descEn: "Le Thanh Cong submitted business documents.",
        timeVi: "1 giờ trước",
        timeEn: "1h ago",
        href: "/admin/owner-applications",
        read: false,
    },
    {
        id: "notif-3",
        type: "lot",
        titleVi: "Bãi xe cần thẩm định",
        titleEn: "Lot inspection required",
        descVi: "Thảo Điền Riverside Parking chờ duyệt bảng giá và layout.",
        descEn: "Thao Dien Riverside Parking pending pricing & layout.",
        timeVi: "3 giờ trước",
        timeEn: "3h ago",
        href: "/admin/parking-lots/pending",
        read: false,
    },
];

export function AdminNotifications() {
    const { language } = useLanguage();
    const isVi = language === "vi";
    const [isOpen, setIsOpen] = useState(false);
    const [items, setItems] = useState<NotificationItem[]>(
        INITIAL_NOTIFICATIONS,
    );
    const dropdownRef = useRef<HTMLDivElement>(null);

    const lots = useAdminLots();
    const pendingLotsCount = countByStatus(lots).Pending;

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
            className="admin-notifications-container"
            style={{ position: "relative", display: "inline-block" }}
        >
            <button
                type="button"
                className="admin-notifications-btn"
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
                    <AdminIcon name="bell" size={17} />
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
                    className="admin-notifications-dropdown"
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
                        animation: "fadeIn 0.15s ease",
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
                                ? "Thông báo hệ thống"
                                : "System Notifications"}
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
                                            {item.type === "lot"
                                                ? "📋 "
                                                : item.type === "application"
                                                  ? "🏢 "
                                                  : "👤 "}
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
                            href="/admin/parking-lots/pending"
                            onClick={() => setIsOpen(false)}
                            style={{
                                color: "#0f172a",
                                fontWeight: 600,
                                textDecoration: "none",
                            }}
                        >
                            {isVi
                                ? `Xem hàng chờ duyệt (${pendingLotsCount})`
                                : `Review queue (${pendingLotsCount})`}{" "}
                            →
                        </Link>
                    </div>
                </div>
            )}
        </div>
    );
}
