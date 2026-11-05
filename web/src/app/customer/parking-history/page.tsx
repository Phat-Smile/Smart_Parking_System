import type { Metadata } from "next";
import { CustomerDataShell } from "@/features/customer/CustomerDataShell";
import CustomerParkingHistoryPage from "@/features/customer/CustomerParkingHistoryPage";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "../data-pages.css";

export const metadata: Metadata = { title: "Parking History | ParkWise" };

export default function ParkingHistoryRoute() {
    return (
        <CustomerDataShell active="history" fontClassName={parkwiseFont.className}>
            <CustomerParkingHistoryPage />
        </CustomerDataShell>
    );
}
