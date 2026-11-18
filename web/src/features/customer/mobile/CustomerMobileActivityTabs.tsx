"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useCustomerLanguage } from "../i18n/customerLanguage";
import "@/features/parkwise/parkwise-controls.css";

export function CustomerMobileActivityTabs() {
    const pathname = usePathname();
    const { copy } = useCustomerLanguage();
    return (
        <nav
            className="pw-app-activity-tabs"
            aria-label={copy("Activity categories", "Loại hoạt động")}
        >
            {[
                {
                    href: "/customer/bookings",
                    label: copy("Bookings", "Đặt chỗ"),
                },
                {
                    href: "/customer/sessions",
                    label: copy("Parking now", "Đang đỗ"),
                },
                {
                    href: "/customer/parking-history",
                    label: copy("History", "Lịch sử"),
                },
            ].map(({ href, label }) => (
                <Link
                    href={href}
                    key={href}
                    className="parkwise-choice"
                    aria-current={pathname === href ? "page" : undefined}
                >
                    {label}
                </Link>
            ))}
        </nav>
    );
}
