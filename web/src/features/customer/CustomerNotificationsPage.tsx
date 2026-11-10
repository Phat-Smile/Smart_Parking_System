"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { ArrowRight, Bell, CalendarCheck2, CarFront, CheckCheck, RefreshCw, Wallet } from "lucide-react";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import {
    getMyNotifications,
    markAllNotificationsRead,
    markNotificationRead,
    type CustomerNotificationItem,
} from "./customerNotificationApi";

const icons: Record<string, typeof CalendarCheck2> = {
    booking: CalendarCheck2,
    parking: CarFront,
    wallet: Wallet,
};

function localizedNotification(
    notice: CustomerNotificationItem,
    copy: (english: string, vietnamese: string) => string,
) {
    const title = notice.title.toLowerCase();
    if (notice.type === "wallet" && title.includes("top-up")) {
        return {
            title: copy("Wallet top-up completed", "Đã nạp tiền vào ví"),
            description: copy("Your wallet balance was updated.", "Số dư ví của bạn đã được cập nhật."),
        };
    }
    if (notice.type === "parking" && title.includes("active")) {
        return {
            title: copy("Parking session active", "Phiên đỗ xe đang hoạt động"),
            description: copy("Use your session QR code when leaving.", "Hãy dùng mã QR phiên đỗ xe khi rời bãi."),
        };
    }
    if (notice.type === "parking" && title.includes("completed")) {
        return {
            title: copy("Parking session completed", "Phiên đỗ xe đã kết thúc"),
            description: copy("Your parking session has ended.", "Phiên đỗ xe của bạn đã kết thúc."),
        };
    }
    if (notice.type === "booking" && title.includes("confirmed")) {
        return {
            title: copy("Booking confirmed", "Đặt chỗ thành công"),
            description: copy("Your parking booking is confirmed.", "Đặt chỗ đỗ xe của bạn đã được xác nhận."),
        };
    }
    return { title: notice.title, description: notice.description };
}

export function CustomerNotificationsPage() {
    const { copy, language } = useCustomerLanguage();
    const [notices, setNotices] = useState<CustomerNotificationItem[]>([]);
    const [filter, setFilter] = useState<"all" | "unread">("all");
    const [totalCount, setTotalCount] = useState(0);
    const [unreadCount, setUnreadCount] = useState(0);
    const [page, setPage] = useState(1);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");

    const load = useCallback(async (nextPage: number, unreadOnly = false) => {
        setLoading(true);
        setError("");
        try {
            const result = await getMyNotifications(unreadOnly, nextPage, 20);
            setNotices(result.items);
            setPage(result.page);
            setTotalCount(result.totalCount);
            setUnreadCount(result.unreadCount);
        } catch {
            setError(copy("Could not load notifications. Please try again.", "Không thể tải thông báo. Vui lòng thử lại."));
        } finally {
            setLoading(false);
        }
    }, [copy]);

    useEffect(() => { queueMicrotask(() => void load(1, false)); }, [load]);

    async function markRead(id: string) {
        try {
            await markNotificationRead(id);
            window.dispatchEvent(new Event("customer-notifications-changed"));
            setNotices((current) => current.map((item) => item.id === id
                ? { ...item, readAtUtc: new Date().toISOString() } : item));
            setUnreadCount((current) => Math.max(0, current - 1));
        } catch {
            setError(copy("Could not mark this notification as read.", "Không thể đánh dấu thông báo đã đọc."));
        }
    }

    async function markAllRead() {
        try {
            await markAllNotificationsRead();
            window.dispatchEvent(new Event("customer-notifications-changed"));
            const now = new Date().toISOString();
            setNotices((current) => current.map((item) => ({ ...item, readAtUtc: item.readAtUtc ?? now })));
            setUnreadCount(0);
        } catch {
            setError(copy("Could not mark notifications as read.", "Không thể đánh dấu thông báo đã đọc."));
        }
    }

    const visible = notices;
    const pageSize = 20;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
    const pageNumbers = Array.from(
        { length: Math.min(5, totalPages) },
        (_, index) => Math.max(1, Math.min(totalPages - 4, page - 2)) + index,
    );

    return (
        <div className="customer-account-content">
            <div className="customer-account-heading">
                <div>
                    <span className="customer-account-eyebrow">{copy("YOUR UPDATES", "CẬP NHẬT CỦA BẠN")}</span>
                    <h1>{copy("Notifications", "Thông báo")}</h1>
                    <p>{copy("Follow your bookings, parking sessions, and wallet activity in one place.",
                        "Theo dõi đặt chỗ, phiên đỗ và hoạt động ví tại một nơi.")}</p>
                </div>
                <button className="customer-account-secondary" type="button" onClick={() => void load(1, filter === "unread")} disabled={loading}>
                    <RefreshCw size={18} aria-hidden="true" /> {copy("Refresh", "Làm mới")}
                </button>
            </div>

            <section className="customer-account-card customer-notifications-card"
                aria-label={copy("Notification list", "Danh sách thông báo")}>
                <div className="customer-notification-toolbar">
                    <div className="customer-notification-tabs" aria-label={copy("Filter notifications", "Lọc thông báo")}>
                        <button type="button" aria-pressed={filter === "all"} onClick={() => { setFilter("all"); void load(1, false); }}>
                            {copy("All", "Tất cả")} <span>{totalCount}</span>
                        </button>
                        <button type="button" aria-pressed={filter === "unread"} onClick={() => { setFilter("unread"); void load(1, true); }}>
                            {copy("Unread", "Chưa đọc")} <span>{unreadCount}</span>
                        </button>
                    </div>
                    {filter === "all" && unreadCount > 0 && <button className="customer-notification-mark-all" type="button" onClick={() => void markAllRead()}>
                        <CheckCheck size={17} /> {copy("Mark all as read", "Đánh dấu tất cả đã đọc")}
                    </button>}
                </div>

                {error && <p role="alert" className="customer-notification-demo">{error}</p>}
                {loading && notices.length === 0 ? (
                    <p className="customer-notification-demo" role="status">{copy("Loading notifications…", "Đang tải thông báo…")}</p>
                ) : visible.length === 0 ? (
                    <div className="customer-notification-empty">
                        <span><Bell size={30} strokeWidth={1.7} /></span>
                        <h2>{filter === "unread" ? copy("No unread notifications", "Không có thông báo chưa đọc")
                            : copy("No notifications yet", "Chưa có thông báo")}</h2>
                        <p>{copy("Updates about your bookings and parking sessions will appear here.",
                            "Thông tin đặt chỗ và phiên đỗ sẽ xuất hiện tại đây.")}</p>
                        <Link href="/customer/dashboard">{copy("Back to dashboard", "Về trang chủ")} <ArrowRight size={17} /></Link>
                    </div>
                ) : (
                    <ul className="customer-notification-list">
                        {visible.map((notice) => {
                            const Icon = icons[notice.type] ?? Bell;
                            const localized = localizedNotification(notice, copy);
                            const isRead = Boolean(notice.readAtUtc);
                            const safeHref = notice.href.startsWith("/customer/") ? notice.href : "/customer/dashboard";
                            return <li key={notice.id} className={isRead ? "is-read" : ""}>
                                <span className={`customer-notification-icon is-${notice.type}`} aria-hidden="true"><Icon size={22} /></span>
                                <div className="customer-notification-copy">
                                    <div>
                                        <h2>{localized.title}</h2>
                                        <time dateTime={notice.createdAtUtc}>
                                            {new Date(notice.createdAtUtc).toLocaleString(language === "vi" ? "vi-VN" : "en-GB")}
                                        </time>
                                    </div>
                                    <p>{localized.description}</p>
                                    <Link href={safeHref}>{copy("View details", "Xem chi tiết")} <ArrowRight size={16} /></Link>
                                </div>
                                {!isRead && <button type="button" className="customer-notification-read"
                                    onClick={() => void markRead(notice.id)}
                                    aria-label={`${copy("Mark as read", "Đánh dấu đã đọc")}: ${notice.title}`} />}
                            </li>;
                        })}
                    </ul>
                )}
                {totalPages > 1 && <nav className="customer-notification-pagination"
                    aria-label={copy("Notification pages", "Trang thông báo")}>
                    <button type="button" className="customer-notification-page-arrow"
                        onClick={() => void load(page - 1, filter === "unread")}
                        disabled={loading || page <= 1} aria-label={copy("Previous page", "Trang trước")}>
                        ‹ <span>{copy("Previous", "Trước")}</span>
                    </button>
                    {pageNumbers[0] > 1 && <>
                        <button type="button" className="customer-notification-page-number" onClick={() => void load(1, filter === "unread")}>1</button>
                        {pageNumbers[0] > 2 && <span className="customer-notification-page-ellipsis" aria-hidden="true">…</span>}
                    </>}
                    {pageNumbers.map((pageNumber) => <button key={pageNumber} type="button"
                        className={`customer-notification-page-number${pageNumber === page ? " is-current" : ""}`}
                        onClick={() => void load(pageNumber, filter === "unread")}
                        aria-current={pageNumber === page ? "page" : undefined} disabled={loading}>
                        {pageNumber}
                    </button>)}
                    {pageNumbers[pageNumbers.length - 1] < totalPages && <>
                        {pageNumbers[pageNumbers.length - 1] < totalPages - 1 && <span className="customer-notification-page-ellipsis" aria-hidden="true">…</span>}
                        <button type="button" className="customer-notification-page-number" onClick={() => void load(totalPages, filter === "unread")}>{totalPages}</button>
                    </>}
                    <span className="customer-notification-page-status" aria-live="polite">
                        {copy(`Page ${page} of ${totalPages}`, `Trang ${page}/${totalPages}`)}
                    </span>
                    <button type="button" className="customer-notification-page-arrow"
                        onClick={() => void load(page + 1, filter === "unread")}
                        disabled={loading || page >= totalPages} aria-label={copy("Next page", "Trang sau")}>
                        <span>{copy("Next", "Tiếp")}</span> ›
                    </button>
                </nav>}
            </section>
        </div>
    );
}
