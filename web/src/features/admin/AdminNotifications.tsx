"use client";

import { useEffect, useMemo, useRef, useState } from "react";
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
        descVi: "Kiểm tra các bãi xe mới gửi hồ sơ xin phê duyệt mở bãi.",
        descEn: "Review newly submitted parking lot applications.",
        timeVi: "15 phút trước",
        timeEn: "15m ago",
        href: "/admin/parking-lots/pending",
        read: false,
    },
    {
        id: "notif-2",
        type: "application",
        titleVi: "Hồ sơ đối tác chủ bãi",
        titleEn: "Partner owner application",
        descVi: "Có hồ sơ đăng ký chủ bãi mới cần đối chiếu giấy tờ pháp lý.",
        descEn: "New partner documents awaiting legal verification.",
        timeVi: "1 giờ trước",
        timeEn: "1h ago",
        href: "/admin/owner-applications",
        read: false,
    },
    {
        id: "notif-3",
        type: "system",
        titleVi: "Đối soát & Nhật ký kiểm toán",
        titleEn: "Audit & Financial Reconciliation",
        descVi: "Hệ thống ghi nhận tự động các biến động ví và cấu hình giá.",
        descEn: "System audit trail is actively recording wallet and pricing events.",
        timeVi: "Hôm nay",
        timeEn: "Today",
        href: "/admin/audit-logs",
        read: false,
    },
];

export function AdminNotifications() {
    const { language } = useLanguage();
    const isVi = language === "vi";
    const [isOpen, setIsOpen] = useState(false);
    const [tab, setTab] = useState<"all" | "unread">("all");
    const [items, setItems] = useState<NotificationItem[]>(
        INITIAL_NOTIFICATIONS,
    );
    const [liveRead, setLiveRead] = useState(false);
    const dropdownRef = useRef<HTMLDivElement>(null);

    const lots = useAdminLots();
    const pendingLotsCount = countByStatus(lots).Pending;

    const combinedItems = useMemo(() => {
        const list = [...items];
        if (pendingLotsCount > 0) {
            list.unshift({
                id: "live-pending-lots",
                type: "lot",
                titleVi: `${pendingLotsCount} bãi xe đang chờ thẩm định`,
                titleEn: `${pendingLotsCount} parking lot(s) awaiting review`,
                descVi: "Nhấn để mở danh sách hàng đợi xét duyệt bãi đỗ xe ngay.",
                descEn: "Click to open the pending parking lot review queue.",
                timeVi: "Trực tiếp",
                timeEn: "Live",
                href: "/admin/parking-lots/pending",
                read: liveRead,
            });
        }
        return list;
    }, [items, pendingLotsCount, liveRead]);

    const unreadCount = combinedItems.filter((n) => !n.read).length;
    const filteredItems =
        tab === "unread" ? combinedItems.filter((n) => !n.read) : combinedItems;

    // Close on click outside or Escape
    useEffect(() => {
        function handleClickOutside(event: MouseEvent) {
            if (
                dropdownRef.current &&
                !dropdownRef.current.contains(event.target as Node)
            ) {
                setIsOpen(false);
            }
        }
        function handleKeyDown(event: KeyboardEvent) {
            if (event.key === "Escape") setIsOpen(false);
        }
        if (isOpen) {
            document.addEventListener("mousedown", handleClickOutside);
            document.addEventListener("keydown", handleKeyDown);
        }
        return () => {
            document.removeEventListener("mousedown", handleClickOutside);
            document.removeEventListener("keydown", handleKeyDown);
        };
    }, [isOpen]);

    function markAllAsRead() {
        setLiveRead(true);
        setItems((prev) => prev.map((item) => ({ ...item, read: true })));
    }

    function markAsRead(id: string) {
        if (id === "live-pending-lots") {
            setLiveRead(true);
        } else {
            setItems((prev) =>
                prev.map((item) =>
                    item.id === id ? { ...item, read: true } : item,
                ),
            );
        }
        setIsOpen(false);
    }

    function getIconName(type: NotificationItem["type"]) {
        if (type === "lot") return "lots";
        if (type === "application") return "fileCheck";
        if (type === "assignment") return "briefcase";
        return "shield";
    }

    return (
        <div ref={dropdownRef} className="admin-notif-wrap">
            <button
                type="button"
                className={`admin-topbar-btn admin-notifications-btn ${isOpen ? "is-open" : ""}`}
                onClick={() => setIsOpen(!isOpen)}
                aria-label={isVi ? "Thông báo" : "Notifications"}
                aria-expanded={isOpen}
            >
                <span className="admin-notif-bell-wrap">
                    <AdminIcon name="bell" size={18} />
                    {unreadCount > 0 && (
                        <span
                            className="admin-notif-badge"
                            aria-label={
                                isVi
                                    ? `${unreadCount} thông báo chưa đọc`
                                    : `${unreadCount} unread notifications`
                            }
                        >
                            {unreadCount > 99 ? "99+" : unreadCount}
                        </span>
                    )}
                </span>
                <span className="admin-topbar-btn-label">
                    {isVi ? "Thông báo" : "Notifications"}
                </span>
            </button>

            {isOpen && (
                <div
                    className="admin-dropdown-panel admin-notifications-dropdown"
                    role="dialog"
                    aria-label={
                        isVi ? "Thông báo hệ thống" : "System Notifications"
                    }
                >
                    <div className="admin-dropdown-header">
                        <div className="admin-dropdown-title-row">
                            <strong>
                                {isVi
                                    ? "Thông báo hệ thống"
                                    : "System Notifications"}
                            </strong>
                            {unreadCount > 0 && (
                                <span className="admin-notif-unread-pill">
                                    {unreadCount} {isVi ? "chưa đọc" : "unread"}
                                </span>
                            )}
                        </div>
                        {unreadCount > 0 && (
                            <button
                                type="button"
                                className="admin-notif-mark-all"
                                onClick={markAllAsRead}
                            >
                                {isVi ? "Đánh dấu đã đọc" : "Mark all read"}
                            </button>
                        )}
                    </div>

                    <div className="admin-notif-tabs" role="tablist">
                        <button
                            type="button"
                            role="tab"
                            aria-selected={tab === "all"}
                            className={tab === "all" ? "active" : ""}
                            onClick={() => setTab("all")}
                        >
                            {isVi ? "Tất cả" : "All"} ({combinedItems.length})
                        </button>
                        <button
                            type="button"
                            role="tab"
                            aria-selected={tab === "unread"}
                            className={tab === "unread" ? "active" : ""}
                            onClick={() => setTab("unread")}
                        >
                            {isVi ? "Chưa đọc" : "Unread"} ({unreadCount})
                        </button>
                    </div>

                    <div className="admin-notif-list">
                        {filteredItems.length === 0 ? (
                            <div className="admin-notif-empty">
                                <span className="admin-notif-empty-icon">
                                    <AdminIcon name="checkCircle" size={22} />
                                </span>
                                <p>
                                    {tab === "unread"
                                        ? isVi
                                            ? "Bạn đã đọc hết mọi thông báo!"
                                            : "You're all caught up!"
                                        : isVi
                                          ? "Không có thông báo mới"
                                          : "No new notifications"}
                                </p>
                            </div>
                        ) : (
                            filteredItems.map((item) => (
                                <Link
                                    key={item.id}
                                    href={item.href}
                                    onClick={() => markAsRead(item.id)}
                                    className={`admin-notif-item ${item.read ? "is-read" : "is-unread"}`}
                                >
                                    <span
                                        className={`admin-notif-type-icon type-${item.type}`}
                                    >
                                        <AdminIcon
                                            name={getIconName(item.type)}
                                            size={16}
                                        />
                                    </span>
                                    <div className="admin-notif-content">
                                        <div className="admin-notif-top">
                                            <span className="admin-notif-item-title">
                                                {isVi
                                                    ? item.titleVi
                                                    : item.titleEn}
                                            </span>
                                            <small className="admin-notif-time">
                                                {isVi
                                                    ? item.timeVi
                                                    : item.timeEn}
                                            </small>
                                        </div>
                                        <p className="admin-notif-desc">
                                            {isVi ? item.descVi : item.descEn}
                                        </p>
                                    </div>
                                    {!item.read && (
                                        <span
                                            className="admin-notif-dot"
                                            aria-hidden="true"
                                        />
                                    )}
                                </Link>
                            ))
                        )}
                    </div>

                    <div className="admin-dropdown-footer">
                        <Link
                            href="/admin/parking-lots/pending"
                            onClick={() => setIsOpen(false)}
                            className="admin-dropdown-footer-link"
                        >
                            <span>
                                {isVi
                                    ? `Xem hàng chờ duyệt (${pendingLotsCount})`
                                    : `Review queue (${pendingLotsCount})`}
                            </span>
                            <AdminIcon name="arrow" size={14} />
                        </Link>
                    </div>
                </div>
            )}
        </div>
    );
}
