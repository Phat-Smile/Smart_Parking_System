import type { Metadata } from "next";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import { CustomerFooter, CustomerHeader } from "@/features/customer/CustomerShell";
import { CustomerNotificationsPage } from "@/features/customer/CustomerNotificationsPage";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "../account-pages.css";

export const metadata: Metadata = { title: "Notifications | ParkWise" };

export default function NotificationsRoute() {
    return (
        <CustomerAccessGuard>
            <div className={`parkwise-page customer-shell-page customer-account-page ${parkwiseFont.className}`}>
                <CustomerHeader active="notifications" />
                <main className="customer-account-main"><CustomerNotificationsPage /></main>
                <CustomerFooter />
            </div>
        </CustomerAccessGuard>
    );
}
