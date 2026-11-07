"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import {
    getMyNotifications,
    markAllNotificationsRead,
    markNotificationRead,
    type CustomerNotificationItem,
} from "./customerNotificationApi";
import {
    ArrowRight,
    Bell,
    CalendarCheck2,
    CarFront,
    CheckCheck,
    Loader2,
    RefreshCw,
    Wallet,
} from "lucide-react";
import { ApiError } from "@/shared/errors/api-error";

const icons: Record<string, typeof CalendarCheck2> = {
    booking: CalendarCheck2,
    parking: CarFront,
    wallet: Wallet,
};

function formatNotificationTime(isoString: string): string {
    try {
        const date = new Date(isoString);
        const now = new Date();
        const diffMs = now.getTime() - date.getTime();
        const diffMinutes = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMinutes / 60);
        const diffDays = Math.floor(diffHours / 24);

        if (diffMinutes < 1) return "Vừa xong";
        if (diffMinutes < 60) return `${diffMinutes} phút trước`;
        if (diffHours < 24) return `${diffHours} giờ trước`;
        if (diffDays === 1) return "Hôm qua";
        if (diffDays < 7) return `${diffDays} ngày trước`;
        return date.toLocaleDateString("vi-VN", {
            day: "2-digit",
            month: "2-digit",
            year: "numeric",
        });
    } catch {
        return isoString;
    }
}

export function CustomerNotificationsPage() {
    const { copy } = useCustomerLanguage();
    const [notifications, setNotifications] = useState<
        CustomerNotificationItem[]
    >([]);
    const [totalCount, setTotalCount] = useState(0);
    const [unreadCount, setUnreadCount] = useState(0);
    const [filter, setFilter] = useState<"all" | "unread">("all");
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const loadNotifications = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const data = await getMyNotifications(filter === "unread");
            setNotifications(data.items);
            setTotalCount(data.totalCount);
            setUnreadCount(data.unreadCount);
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                setError(
                    err.problem?.detail ||
                        err.message ||
                        "Không thể tải thông báo.",
                );
            } else if (err instanceof TypeError) {
                setError(
                    "Không thể kết nối đến máy chủ. Vui lòng thử lại sau.",
                );
            } else if (err instanceof Error) {
                setError(err.message);
            } else {
                setError("Đã xảy ra lỗi khi tải thông báo.");
            }
        } finally {
            setLoading(false);
        }
    }, [filter]);

    useEffect(() => {
        let active = true;
        queueMicrotask(() => {
            if (active) void loadNotifications();
        });
        return () => {
            active = false;
        };
    }, [loadNotifications]);

    async function handleMarkRead(id: string) {
        try {
            await markNotificationRead(id);
            setNotifications((prev) =>
                prev.map((item) =>
                    item.id === id
                        ? { ...item, readAtUtc: new Date().toISOString() }
                        : item,
                ),
            );
            setUnreadCount((c) => Math.max(0, c - 1));
        } catch (err: unknown) {
            console.error("Failed to mark notification as read", err);
        }
    }

    async function handleMarkAllRead() {
        try {
            await markAllNotificationsRead();
            const now = new Date().toISOString();
            setNotifications((prev) =>
                prev.map((item) => ({ ...item, readAtUtc: now })),
            );
            setUnreadCount(0);
        } catch (err: unknown) {
            console.error("Failed to mark all as read", err);
        }
    }

    return (
        <div className="customer-account-content">
            <div className="customer-account-heading">
                <div>
                    <span className="customer-account-eyebrow">
                        {copy("YOUR UPDATES", "CẬP NHẬT CỦA BẠN")}
                    </span>
                    <h1>{copy("Notifications", "Thông báo")}</h1>
                    <p>
                        {copy(
                            "Follow your bookings, parking sessions, and wallet activity in real time.",
                            "Theo dõi đặt chỗ, phiên đỗ và hoạt động ví theo thời gian thực.",
                        )}
                    </p>
                </div>
                <button
                    className="customer-account-secondary"
                    type="button"
                    onClick={() => void loadNotifications()}
                    disabled={loading}
                >
                    <RefreshCw
                        size={18}
                        className={loading ? "animate-spin" : ""}
                        aria-hidden="true"
                    />
                    {copy("Refresh", "Làm mới")}
                </button>
            </div>

            {error && (
                <div
                    className="customer-notification-demo customer-error-alert"
                    role="alert"
                >
                    <p>{error}</p>
                </div>
            )}

            <section
                className="customer-account-card customer-notifications-card"
                aria-label={copy("Notification list", "Danh sách thông báo")}
            >
                <div className="customer-notification-toolbar">
                    <div
                        className="customer-notification-tabs"
                        aria-label={copy(
                            "Filter notifications",
                            "Lọc thông báo",
                        )}
                    >
                        <button
                            type="button"
                            aria-pressed={filter === "all"}
                            onClick={() => setFilter("all")}
                        >
                            {copy("All", "Tất cả")} <span>{totalCount}</span>
                        </button>
                        <button
                            type="button"
                            aria-pressed={filter === "unread"}
                            onClick={() => setFilter("unread")}
                        >
                            {copy("Unread", "Chưa đọc")}{" "}
                            <span>{unreadCount}</span>
                        </button>
                    </div>
                    {unreadCount > 0 && (
                        <button
                            className="customer-notification-mark-all"
                            type="button"
                            onClick={() => void handleMarkAllRead()}
                        >
                            <CheckCheck size={17} />{" "}
                            {copy("Mark all as read", "Đánh dấu tất cả đã đọc")}
                        </button>
                    )}
                </div>

                {loading ? (
                    <div className="customer-notification-empty">
                        <Loader2
                            size={32}
                            className="animate-spin text-primary"
                        />
                        <p>
                            {copy(
                                "Loading notifications...",
                                "Đang tải thông báo...",
                            )}
                        </p>
                    </div>
                ) : notifications.length === 0 ? (
                    <div className="customer-notification-empty">
                        <span>
                            <Bell size={30} strokeWidth={1.7} />
                        </span>
                        <h2>
                            {filter === "unread"
                                ? copy(
                                      "No unread notifications",
                                      "Không có thông báo chưa đọc",
                                  )
                                : copy(
                                      "No notifications yet",
                                      "Chưa có thông báo nào",
                                  )}
                        </h2>
                        <p>
                            {filter === "unread"
                                ? copy(
                                      "All your notifications have been marked as read.",
                                      "Tất cả thông báo của bạn đã được đánh dấu đã đọc.",
                                  )
                                : copy(
                                      "Updates about your bookings, sessions, and payments will appear here.",
                                      "Thông tin cập nhật về đặt chỗ, phiên đỗ và thanh toán sẽ xuất hiện tại đây.",
                                  )}
                        </p>
                        {filter === "all" && (
                            <Link href="/customer/dashboard">
                                {copy("Back to dashboard", "Về trang chủ")}{" "}
                                <ArrowRight size={17} />
                            </Link>
                        )}
                    </div>
                ) : (
                    <ul className="customer-notification-list">
                        {notifications.map((notice) => {
                            const Icon = icons[notice.type] || Bell;
                            const isRead = notice.readAtUtc !== null;
                            return (
                                <li
                                    key={notice.id}
                                    className={isRead ? "is-read" : ""}
                                >
                                    <span
                                        className={`customer-notification-icon is-${notice.type}`}
                                        aria-hidden="true"
                                    >
                                        <Icon size={22} />
                                    </span>
                                    <div className="customer-notification-copy">
                                        <div>
                                            <h2>{notice.title}</h2>
                                            <time>
                                                {formatNotificationTime(
                                                    notice.createdAtUtc,
                                                )}
                                            </time>
                                        </div>
                                        <p>{notice.description}</p>
                                        {notice.href && (
                                            <Link href={notice.href}>
                                                {copy(
                                                    "View details",
                                                    "Xem chi tiết",
                                                )}{" "}
                                                <ArrowRight size={16} />
                                            </Link>
                                        )}
                                    </div>
                                    {!isRead && (
                                        <button
                                            type="button"
                                            className="customer-notification-read"
                                            onClick={() =>
                                                void handleMarkRead(notice.id)
                                            }
                                            aria-label={`${copy("Mark as read", "Đánh dấu đã đọc")}: ${notice.title}`}
                                            title={copy(
                                                "Mark as read",
                                                "Đánh dấu đã đọc",
                                            )}
                                        />
                                    )}
                                </li>
                            );
                        })}
                    </ul>
                )}
            </section>
        </div>
    );
}
