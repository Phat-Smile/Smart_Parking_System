"use client";

import { useState } from "react";
import Link from "next/link";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import {
    ArrowRight,
    Bell,
    CalendarCheck2,
    CarFront,
    CheckCheck,
    CircleHelp,
    Wallet,
} from "lucide-react";

type Notice = {
    id: string;
    type: "booking" | "parking" | "wallet";
    title: string;
    description: string;
    time: string;
    href: string;
};

const examples: Notice[] = [
    {
        id: "booking",
        type: "booking",
        title: "Booking confirmed",
        description: "View your booking details and QR code in My Bookings.",
        time: "Just now",
        href: "/customer/bookings",
    },
    {
        id: "parking",
        type: "parking",
        title: "Parking session active",
        description: "Use your session QR code when leaving the parking lot.",
        time: "Today",
        href: "/customer/sessions",
    },
    {
        id: "wallet",
        type: "wallet",
        title: "Wallet activity updated",
        description: "Check your balance and transaction history in My Wallet.",
        time: "Yesterday",
        href: "/customer/wallet",
    },
];

const icons = { booking: CalendarCheck2, parking: CarFront, wallet: Wallet };

export function CustomerNotificationsPage() {
    const { copy } = useCustomerLanguage();
    const [showExamples, setShowExamples] = useState(false);
    const [filter, setFilter] = useState<"all" | "unread">("all");
    const [readIds, setReadIds] = useState<string[]>([]);
    const notices = showExamples ? examples : [];
    const visible = filter === "unread" ? notices.filter((notice) => !readIds.includes(notice.id)) : notices;
    const unreadCount = notices.length - readIds.length;

    function toggleExamples() {
        setShowExamples((current) => !current);
        setFilter("all");
        setReadIds([]);
    }

    return (
        <div className="customer-account-content">
            <div className="customer-account-heading">
                <div>
                    <span className="customer-account-eyebrow">{copy("YOUR UPDATES", "CẬP NHẬT CỦA BẠN")}</span>
                    <h1>{copy("Notifications", "Thông báo")}</h1>
                    <p>{copy("Follow your bookings, parking sessions, and wallet activity in one place.", "Theo dõi đặt chỗ, phiên đỗ và hoạt động ví tại một nơi.")}</p>
                </div>
                <button className="customer-account-secondary" type="button" onClick={toggleExamples}>
                    <CircleHelp size={18} aria-hidden="true" />
                    {showExamples ? copy("Hide sample notices", "Ẩn thông báo mẫu") : copy("Show sample notices", "Xem thông báo mẫu")}
                </button>
            </div>

            {showExamples && (
                <p className="customer-notification-demo" role="status">
                    {copy("These are sample notices for preview only. They are not from your account.", "Đây chỉ là thông báo mẫu để xem trước, không thuộc tài khoản của bạn.")}
                </p>
            )}

            <section className="customer-account-card customer-notifications-card" aria-label={copy("Notification list", "Danh sách thông báo")}>
                <div className="customer-notification-toolbar">
                    <div className="customer-notification-tabs" aria-label={copy("Filter notifications", "Lọc thông báo")}>
                        <button type="button" aria-pressed={filter === "all"} onClick={() => setFilter("all")}>
                            {copy("All", "Tất cả")} <span>{notices.length}</span>
                        </button>
                        <button type="button" aria-pressed={filter === "unread"} onClick={() => setFilter("unread")}>
                            {copy("Unread", "Chưa đọc")} <span>{unreadCount}</span>
                        </button>
                    </div>
                    {unreadCount > 0 && (
                        <button className="customer-notification-mark-all" type="button" onClick={() => setReadIds(notices.map((notice) => notice.id))}>
                            <CheckCheck size={17} /> {copy("Mark all as read", "Đánh dấu tất cả đã đọc")}
                        </button>
                    )}
                </div>

                {visible.length === 0 ? (
                    <div className="customer-notification-empty">
                        <span><Bell size={30} strokeWidth={1.7} /></span>
                        <h2>{filter === "unread" ? copy("No unread notifications", "Không có thông báo chưa đọc") : copy("No notifications yet", "Chưa có thông báo")}</h2>
                        <p>
                            {filter === "unread"
                                ? copy("All sample notices have been marked as read.", "Tất cả thông báo mẫu đã được đánh dấu đã đọc.")
                                : copy("Updates about your bookings and parking sessions will appear here.", "Thông tin đặt chỗ và phiên đỗ sẽ xuất hiện tại đây.")}
                        </p>
                        {filter === "all" && <Link href="/customer/dashboard">{copy("Back to dashboard", "Về trang chủ")} <ArrowRight size={17} /></Link>}
                    </div>
                ) : (
                    <ul className="customer-notification-list">
                        {visible.map((notice) => {
                            const Icon = icons[notice.type];
                            const isRead = readIds.includes(notice.id);
                            return (
                                <li key={notice.id} className={isRead ? "is-read" : ""}>
                                    <span className={`customer-notification-icon is-${notice.type}`} aria-hidden="true"><Icon size={22} /></span>
                                    <div className="customer-notification-copy">
                                        <div><h2>{copy(notice.title, notice.type === "booking" ? "Đặt chỗ đã xác nhận" : notice.type === "parking" ? "Phiên đỗ đang hoạt động" : "Ví đã cập nhật")}</h2><time>{copy(notice.time, notice.type === "booking" ? "Vừa xong" : notice.type === "parking" ? "Hôm nay" : "Hôm qua")}</time></div>
                                        <p>{copy(notice.description, notice.type === "booking" ? "Xem chi tiết đặt chỗ và mã QR trong Đặt chỗ của tôi." : notice.type === "parking" ? "Dùng mã QR phiên đỗ khi rời bãi." : "Kiểm tra số dư và lịch sử giao dịch trong Ví của tôi.")}</p>
                                        <Link href={notice.href}>{copy("View details", "Xem chi tiết")} <ArrowRight size={16} /></Link>
                                    </div>
                                    {!isRead && <button type="button" className="customer-notification-read" onClick={() => setReadIds((current) => [...current, notice.id])} aria-label={`${copy("Mark as read", "Đánh dấu đã đọc")}: ${notice.title}`} title={copy("Mark as read", "Đánh dấu đã đọc")} />}
                                </li>
                            );
                        })}
                    </ul>
                )}
            </section>
        </div>
    );
}
