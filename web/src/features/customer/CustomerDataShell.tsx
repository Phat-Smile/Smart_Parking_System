"use client";

import type { ReactNode } from "react";
import { CustomerAccessGuard } from "./CustomerAccessGuard";
import { CustomerFooter, CustomerHeader } from "./CustomerShell";
import { useCustomerLanguage } from "./i18n/customerLanguage";

export function CustomerDataShell({
    children,
    fontClassName,
    active,
}: {
    children: ReactNode;
    fontClassName: string;
    active: "bookings" | "history";
}) {
    const { language } = useCustomerLanguage();
    return (
        <CustomerAccessGuard>
            <div
                lang={language}
                className={`parkwise-page customer-shell-page customer-data-page ${fontClassName}`}
            >
                <CustomerHeader active={active} />
                <main className="customer-data-main">
                    <div className="customer-data-panel">{children}</div>
                </main>
                <CustomerFooter />
            </div>
        </CustomerAccessGuard>
    );
}
