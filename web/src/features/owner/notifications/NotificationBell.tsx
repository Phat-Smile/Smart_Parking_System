"use client";

import React, {
    useState,
    useRef,
    useEffect,
    useMemo,
    useSyncExternalStore,
} from "react";
import { createPortal } from "react-dom";
import { useRouter, usePathname } from "next/navigation";
import { useNotificationStore } from "./useNotificationStore";
import { NotificationCategory } from "./notificationTypes";

interface NotificationBellProps {
    variant: "sidebar" | "header";
}

const emptySubscribe = () => () => {};

const CATEGORY_TABS: { key: NotificationCategory; label: string }[] = [
    { key: "all", label: "Tất cả •" },
    { key: "lots", label: "Bãi của tôi •" },
    { key: "reservations", label: "Đặt chỗ •" },
    { key: "sessions", label: "Phiên đỗ xe •" },
    { key: "layout", label: "Sơ đồ •" },
    { key: "reports", label: "Báo cáo •" },
    { key: "system", label: "Hệ thống •" },
];

const CATEGORY_LABELS: Record<string, string> = {
    lots: "Bãi của tôi",
    reservations: "Đặt chỗ",
    sessions: "Phiên đỗ xe",
    layout: "Sơ đồ",
    reports: "Báo cáo",
    system: "Hệ thống",
};

function formatRelativeTime(isoString: string): string {
    const diffMs = Date.now() - new Date(isoString).getTime();
    const mins = Math.max(1, Math.round(diffMs / 60000));
    if (mins < 60) return `${mins} phút trước`;
    const hours = Math.round(mins / 60);
    if (hours < 24) return `${hours} giờ trước`;
    const days = Math.round(hours / 24);
    return `${days} ngày trước`;
}

export function NotificationBell({ variant }: NotificationBellProps) {
    const router = useRouter();
    const pathname = usePathname();
    const [open, setOpen] = useState(false);
    const mounted = useSyncExternalStore(
        emptySubscribe,
        () => true,
        () => false,
    );
    const bellRef = useRef<HTMLDivElement>(null);
    const panelRef = useRef<HTMLDivElement>(null);

    const {
        notifications,
        activeTab,
        setActiveTab,
        markAsRead,
        markAllAsRead,
        categoryPreferences,
        fetchNotifications,
        isLoading,
    } = useNotificationStore();

    useEffect(() => {
        if (mounted) {
            void fetchNotifications();
        }
    }, [mounted, fetchNotifications]);

    // Đóng bảng thông báo ngay khi chuyển trang theo chuẩn React
    const [prevPath, setPrevPath] = useState(pathname);
    if (prevPath !== pathname) {
        setPrevPath(pathname);
        setOpen(false);
    }

    // Đóng khi click ra ngoài hoặc bấm Escape
    useEffect(() => {
        function handleClickOutside(e: MouseEvent | TouchEvent) {
            const target = e.target as Node;
            if (
                panelRef.current &&
                !panelRef.current.contains(target) &&
                bellRef.current &&
                !bellRef.current.contains(target)
            ) {
                setOpen(false);
            }
        }

        function handleKeyDown(e: KeyboardEvent) {
            if (e.key === "Escape") {
                setOpen(false);
            }
        }

        if (open) {
            document.addEventListener("mousedown", handleClickOutside);
            document.addEventListener("touchstart", handleClickOutside);
            document.addEventListener("keydown", handleKeyDown);
        }
        return () => {
            document.removeEventListener("mousedown", handleClickOutside);
            document.removeEventListener("touchstart", handleClickOutside);
            document.removeEventListener("keydown", handleKeyDown);
        };
    }, [open]);

    // Thông báo khả dụng (loại trừ các mục đã bị tắt trong Cài đặt thông báo)
    const allowedNotifications = useMemo(() => {
        return notifications.filter((n) => {
            if (!categoryPreferences) return true;
            const pref = categoryPreferences[n.category];
            return pref !== false;
        });
    }, [notifications, categoryPreferences]);

    // Số lượng chưa đọc thực tế
    const unreadCount = useMemo(() => {
        return allowedNotifications.filter((n) => !n.read).length;
    }, [allowedNotifications]);

    // Thông báo theo Tab đang chọn
    const filteredNotifications = useMemo(() => {
        if (activeTab === "all") return allowedNotifications;
        return allowedNotifications.filter((n) => n.category === activeTab);
    }, [allowedNotifications, activeTab]);

    return (
        <div
            ref={bellRef}
            className={`ov2-bell-wrapper ov2-bell-${variant}`}
            style={{ position: "relative" }}
        >
            {variant === "sidebar" ? (
                <button
                    type="button"
                    onClick={() => setOpen((o) => !o)}
                    onMouseDown={(e) => e.stopPropagation()}
                    aria-label="Thông báo"
                    aria-expanded={open}
                    className="ov2-bell-btn"
                    style={{
                        position: "relative",
                        width: 36,
                        height: 36,
                        borderRadius: "50%",
                        background: "rgba(255, 255, 255, 0.08)",
                        border: "1px solid rgba(255, 255, 255, 0.16)",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        cursor: "pointer",
                        color: "#ffffff",
                        transition: "all 0.15s ease",
                        padding: 0,
                    }}
                >
                    {/* Biểu tượng chuông thông báo */}
                    <svg
                        width="17"
                        height="17"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        aria-hidden="true"
                        style={{ pointerEvents: "none" }}
                    >
                        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                        <path d="M13.73 21a2 2 0 0 1-3.46 0" />
                    </svg>

                    {/* Huy hiệu số 5 nhỏ thu gọn ở góc trên phải */}
                    {unreadCount > 0 && (
                        <span
                            style={{
                                position: "absolute",
                                top: -3,
                                right: -3,
                                minWidth: 17,
                                height: 17,
                                borderRadius: "50%",
                                background: "#ea4335",
                                border: "2px solid #101c36",
                                color: "#ffffff",
                                fontSize: 10,
                                fontWeight: 800,
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                lineHeight: 1,
                                padding: "0 3px",
                                pointerEvents: "none",
                            }}
                        >
                            {unreadCount > 99 ? "99+" : unreadCount}
                        </span>
                    )}
                </button>
            ) : (
                <button
                    type="button"
                    onClick={() => setOpen((o) => !o)}
                    aria-label="Thông báo"
                    aria-expanded={open}
                    className="ov2-bell-btn"
                    style={{
                        position: "relative",
                        background: "#f3f6fb",
                        border: "1px solid #e3e8f2",
                        borderRadius: "50%",
                        width: 36,
                        height: 36,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        cursor: "pointer",
                        color: "#0f1b3d",
                        transition: "all 0.15s ease",
                    }}
                >
                    <svg
                        width="17"
                        height="17"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
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
                                top: -3,
                                right: -3,
                                background: "#ea4335",
                                color: "#fff",
                                fontSize: 10.5,
                                fontWeight: 800,
                                minWidth: 17,
                                height: 17,
                                padding: "0 4px",
                                borderRadius: 999,
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                lineHeight: 1,
                            }}
                        >
                            {unreadCount > 99 ? "99+" : unreadCount}
                        </span>
                    )}
                </button>
            )}

            {/* Backdrop trong suốt hỗ trợ đóng popover khi click ngoài mà không làm tối/mờ trang */}
            {open &&
                mounted &&
                createPortal(
                    <>
                        <div
                            onClick={() => setOpen(false)}
                            aria-hidden="true"
                            style={{
                                position: "fixed",
                                inset: 0,
                                zIndex: 99998,
                                background: "transparent",
                            }}
                        />

                        <div
                            ref={panelRef}
                            role="region"
                            aria-label="Bảng thông báo"
                            className="ov2-notif-panel"
                            style={{
                                position: "fixed",
                                top: variant === "sidebar" ? "18px" : "64px",
                                left: variant === "sidebar" ? "240px" : "auto",
                                right: variant === "sidebar" ? "auto" : "20px",
                                width: 380,
                                maxWidth: "calc(100vw - 260px)",
                                background: "#ffffff",
                                borderRadius: 16,
                                boxShadow:
                                    "0 12px 32px rgba(15, 27, 61, 0.16), 0 2px 6px rgba(15, 27, 61, 0.04)",
                                border: "1px solid #e5e7eb",
                                zIndex: 99999,
                                padding: "14px 0 8px",
                                animation: "ov2SlideIn 0.18s ease-out",
                            }}
                        >
                            {/* Header bảng thông báo chuẩn mẫu */}
                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                    alignItems: "center",
                                    padding: "0 16px 12px",
                                }}
                            >
                                <div
                                    style={{
                                        fontWeight: 700,
                                        fontSize: 16,
                                        color: "#111827",
                                    }}
                                >
                                    Thông báo
                                </div>
                                <button
                                    type="button"
                                    onClick={() => markAllAsRead()}
                                    style={{
                                        border: "1px solid #e5e7eb",
                                        background: "#ffffff",
                                        color: "#374151",
                                        fontSize: 12.5,
                                        fontWeight: 500,
                                        cursor: "pointer",
                                        padding: "4px 10px",
                                        borderRadius: 8,
                                        boxShadow: "0 1px 2px rgba(0,0,0,0.04)",
                                        transition: "background 0.12s ease",
                                    }}
                                    onMouseEnter={(e) =>
                                        (e.currentTarget.style.backgroundColor =
                                            "#f9fafb")
                                    }
                                    onMouseLeave={(e) =>
                                        (e.currentTarget.style.backgroundColor =
                                            "#ffffff")
                                    }
                                >
                                    Đánh dấu đã đọc
                                </button>
                            </div>

                            {/* Category Tabs chuẩn mẫu */}
                            <div
                                style={{
                                    display: "flex",
                                    gap: 6,
                                    overflowX: "auto",
                                    padding: "0 16px 8px",
                                    scrollbarWidth: "none",
                                }}
                            >
                                {CATEGORY_TABS.map((tab) => {
                                    const isEnabled =
                                        tab.key === "all" ||
                                        !categoryPreferences ||
                                        categoryPreferences[tab.key] !== false;
                                    if (!isEnabled) return null;
                                    const isSelected = activeTab === tab.key;

                                    return (
                                        <button
                                            key={tab.key}
                                            type="button"
                                            onClick={() =>
                                                setActiveTab(tab.key)
                                            }
                                            style={{
                                                border: isSelected
                                                    ? "none"
                                                    : "1px solid #e5e7eb",
                                                background: isSelected
                                                    ? "#18181b"
                                                    : "#ffffff",
                                                color: isSelected
                                                    ? "#ffffff"
                                                    : "#374151",
                                                borderRadius: 9999,
                                                padding: "5px 12px",
                                                fontSize: 12,
                                                fontWeight: isSelected
                                                    ? 600
                                                    : 500,
                                                cursor: "pointer",
                                                whiteSpace: "nowrap",
                                                display: "flex",
                                                alignItems: "center",
                                                gap: 4,
                                                flexShrink: 0,
                                            }}
                                        >
                                            {tab.label}
                                        </button>
                                    );
                                })}
                            </div>

                            {/* Thanh trượt chỉ mục grey */}
                            <div style={{ padding: "0 16px 10px" }}>
                                <div
                                    style={{
                                        width: "100%",
                                        height: 4,
                                        background: "#f3f4f6",
                                        borderRadius: 999,
                                        overflow: "hidden",
                                    }}
                                >
                                    <div
                                        style={{
                                            width: "32%",
                                            height: "100%",
                                            background: "#9ca3af",
                                            borderRadius: 999,
                                        }}
                                    />
                                </div>
                            </div>

                            <div style={{ height: 1, background: "#f3f4f6" }} />

                            {/* Danh sách thông báo */}
                            <div
                                className="ov2-notif-list"
                                style={{ maxHeight: 520, overflowY: "auto" }}
                            >
                                {filteredNotifications.length > 0 ? (
                                    filteredNotifications.map((n) => (
                                        <div
                                            key={n.id}
                                            onClick={() => {
                                                markAsRead(n.id);
                                                if (n.href) {
                                                    setOpen(false);
                                                    router.push(n.href);
                                                }
                                            }}
                                            style={{
                                                padding: "12px 16px",
                                                borderBottom:
                                                    "1px solid #f3f4f6",
                                                cursor: "pointer",
                                                backgroundColor: "#ffffff",
                                                transition:
                                                    "background-color 0.12s ease",
                                            }}
                                            onMouseEnter={(e) =>
                                                (e.currentTarget.style.backgroundColor =
                                                    "#f9fafb")
                                            }
                                            onMouseLeave={(e) =>
                                                (e.currentTarget.style.backgroundColor =
                                                    "#ffffff")
                                            }
                                        >
                                            <div
                                                style={{
                                                    display: "flex",
                                                    alignItems: "center",
                                                    gap: 6,
                                                }}
                                            >
                                                {!n.read && (
                                                    <span
                                                        style={{
                                                            color: "#2563eb",
                                                            fontSize: 16,
                                                            lineHeight: 1,
                                                        }}
                                                    >
                                                        •
                                                    </span>
                                                )}
                                                <span
                                                    style={{
                                                        fontWeight: n.read
                                                            ? 600
                                                            : 700,
                                                        fontSize: 13.5,
                                                        color: "#111827",
                                                    }}
                                                >
                                                    {n.title}
                                                </span>
                                            </div>
                                            <div
                                                style={{
                                                    fontSize: 12,
                                                    color: "#6b7280",
                                                    margin: "2px 0 3px 0",
                                                }}
                                            >
                                                {CATEGORY_LABELS[n.category] ||
                                                    n.category}{" "}
                                                ·{" "}
                                                {formatRelativeTime(
                                                    n.createdAt,
                                                )}
                                            </div>
                                            <div
                                                style={{
                                                    fontSize: 12.5,
                                                    color: "#4b5563",
                                                    lineHeight: 1.4,
                                                }}
                                            >
                                                {n.body}
                                            </div>
                                        </div>
                                    ))
                                ) : (
                                    <div
                                        style={{
                                            padding: "28px 16px",
                                            textAlign: "center",
                                            color: "#6b7280",
                                            fontSize: 12.5,
                                        }}
                                    >
                                        Không có thông báo nào trong mục này
                                    </div>
                                )}
                            </div>
                        </div>
                    </>,
                    document.body,
                )}
        </div>
    );
}
