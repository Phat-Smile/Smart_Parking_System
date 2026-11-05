import type { Metadata } from "next";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import {
    CustomerFooter,
    CustomerHeader,
} from "@/features/customer/CustomerShell";
import { CustomerProfilePage } from "@/features/customer/CustomerProfilePage";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "../account-pages.css";

export const metadata: Metadata = { title: "My Profile | ParkWise" };

export default function ProfileRoute() {
    return (
        <CustomerAccessGuard>
            <div
                className={`parkwise-page customer-shell-page customer-account-page ${parkwiseFont.className}`}
            >
                <CustomerHeader active="profile" />
                <main className="customer-account-main">
                    <CustomerProfilePage />
                </main>
                <CustomerFooter />
            </div>
        </CustomerAccessGuard>
    );
}
