import type { Metadata } from "next";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import { CustomerMobileActivityTabs } from "@/features/customer/mobile/CustomerMobileActivityTabs";
import {
    CustomerSessionsIntro,
    CustomerSessionsList,
} from "@/features/customer/CustomerSessions";
import {
    CustomerFooter,
    CustomerHeader,
} from "@/features/customer/CustomerShell";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "./sessions.css";

export const metadata: Metadata = {
    title: "My Sessions | ParkWise",
    description: "Your open parking sessions and their session QR codes.",
};

export default function CustomerSessionsRoute() {
    return (
        <CustomerAccessGuard>
            <div
                className={`parkwise-page customer-shell-page customer-sessions-page ${parkwiseFont.className}`}
            >
                <CustomerHeader active="sessions" />
                <main className="customer-sessions-main">
                    <CustomerMobileActivityTabs />
                    <CustomerSessionsIntro />
                    <CustomerSessionsList />
                </main>
                <CustomerFooter />
            </div>
        </CustomerAccessGuard>
    );
}
