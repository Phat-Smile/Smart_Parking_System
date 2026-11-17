"use client";

import type { ReactNode } from "react";
import { CustomerAccessGuard } from "./CustomerAccessGuard";
import { CustomerFooter, CustomerHeader } from "./CustomerShell";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import { CustomerMobileActivityTabs } from "./mobile/CustomerMobileActivityTabs";

export function CustomerDataShell({
    children,
    fontClassName,
    active,
    requireAuth = true,
}: {
    children: ReactNode;
    fontClassName: string;
    active: "bookings" | "history";
    requireAuth?: boolean;
}) {
    const { language } = useCustomerLanguage();
    const content = (
        <div
            lang={language}
            className={`parkwise-page customer-shell-page customer-data-page ${fontClassName}`}
        >
            <CustomerHeader active={active} />
            <main className="customer-data-main">
                <CustomerMobileActivityTabs />
                <div className="customer-data-panel">{children}</div>
            </main>
            <CustomerFooter />
        </div>
    );

    if (!requireAuth) {
        return content;
    }

    return <CustomerAccessGuard>{content}</CustomerAccessGuard>;
}
