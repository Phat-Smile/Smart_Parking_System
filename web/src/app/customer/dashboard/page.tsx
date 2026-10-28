import type { Metadata } from "next";
import { CustomerDashboardPage } from "@/features/customer/CustomerDashboardPage";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "./dashboard.css";

export const metadata: Metadata = {
    title: "Customer Dashboard | ParkWise",
    description: "Your ParkWise parking overview and quick actions.",
    icons: { icon: "/images/parkwise/logo.png" },
};

export default function CustomerDashboardRoute() {
    return (
        <CustomerAccessGuard>
            <CustomerDashboardPage fontClassName={parkwiseFont.className} />
        </CustomerAccessGuard>
    );
}
