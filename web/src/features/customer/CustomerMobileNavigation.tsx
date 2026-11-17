"use client";

import Link from "next/link";
import { ParkWiseHeader } from "@/features/parkwise/ParkWiseHeader";
import "@/features/parkwise/parkwise-controls.css";
import { usePathname } from "next/navigation";
import {
    ArrowLeft,
    Bell,
    CalendarDays,
    House,
    Search,
    UserRound,
} from "lucide-react";
import { CustomerLanguageSwitcher } from "./i18n/CustomerLanguageSwitcher";
import { useCustomerLanguage } from "./i18n/customerLanguage";

export function CustomerMobileNavigation() {
    const pathname = usePathname();
    const { copy } = useCustomerLanguage();
    const activity = [
        "/customer/bookings",
        "/customer/sessions",
        "/customer/receipts",
        "/customer/parking-history",
    ].some((route) => pathname === route || pathname.startsWith(`${route}/`));
    const account = [
        "/customer/profile",
        "/customer/complete-profile",
        "/customer/vehicles",
        "/customer/wallet",
    ].some((route) => pathname === route || pathname.startsWith(`${route}/`));
    const home = pathname === "/customer/dashboard";
    const notifications = pathname === "/customer/notifications";
    const titles: Record<string, string> = {
        "/customer/dashboard": "ParkWise",
        "/customer/profile": copy("Account", "Tài khoản"),
        "/customer/vehicles": copy("My vehicles", "Xe của tôi"),
        "/customer/wallet": copy("Wallet", "Ví của tôi"),
        "/customer/complete-profile": copy("Edit profile", "Chỉnh sửa hồ sơ"),
        "/search": copy("Find parking", "Tìm bãi đỗ"),
    };
    const title = activity
        ? copy("Booking", "Đặt chỗ")
        : notifications
          ? copy("Notifications", "Thông báo")
          : titles[pathname] || copy("Parking details", "Chi tiết bãi đỗ");
    const items = [
        {
            href: "/customer/dashboard",
            label: copy("Home", "Trang chủ"),
            icon: House,
            active:
                home ||
                pathname === "/search" ||
                pathname.startsWith("/parking-lots/"),
        },
        {
            href: "/customer/bookings",
            label: copy("Booking", "Đặt chỗ"),
            icon: CalendarDays,
            active: activity,
        },
        {
            href: "/customer/notifications",
            label: copy("Notification", "Thông báo"),
            icon: Bell,
            active: notifications,
        },
        {
            href: "/customer/profile",
            label: copy("Account", "Tài khoản"),
            icon: UserRound,
            active: account,
        },
    ];
    return (
        <>
            <ParkWiseHeader
                className={`pw-app-topbar${home ? " pw-app-topbar-home" : ""}`}
                rowClassName="pw-app-topbar-row"
                brandClassName="pw-app-brand"
                brandHref="/customer/dashboard"
                afterRow={
                    <div className="pw-app-title-row">
                        {!home &&
                            !activity &&
                            !notifications &&
                            pathname !== "/customer/profile" && (
                                <Link
                                    href={
                                        account
                                            ? "/customer/profile"
                                            : "/customer/dashboard"
                                    }
                                    className="pw-app-back"
                                    aria-label={copy("Back", "Quay lại")}
                                >
                                    <ArrowLeft size={23} aria-hidden="true" />
                                </Link>
                            )}
                        <h1 className="pw-app-topbar-title">{title}</h1>
                    </div>
                }
            >
                <div className="pw-app-topbar-actions">
                    <CustomerLanguageSwitcher />
                    {!home && (
                        <Link
                            href="/search"
                            aria-label={copy("Find parking", "Tìm bãi đỗ")}
                        >
                            <Search size={23} aria-hidden="true" />
                        </Link>
                    )}
                </div>
            </ParkWiseHeader>
            <nav
                className="pw-mobile-bottom-nav pw-app-floating-nav"
                aria-label={copy(
                    "Customer navigation",
                    "Điều hướng khách hàng",
                )}
            >
                {items.map(({ href, label, icon: Icon, active }) => (
                    <Link
                        key={href}
                        href={href}
                        className="parkwise-choice"
                        aria-current={active ? "page" : undefined}
                    >
                        <span className="pw-mobile-nav-icon">
                            <Icon
                                size={23}
                                strokeWidth={active ? 2.4 : 1.9}
                                aria-hidden="true"
                            />
                        </span>
                        <span>{label}</span>
                    </Link>
                ))}
            </nav>
        </>
    );
}
