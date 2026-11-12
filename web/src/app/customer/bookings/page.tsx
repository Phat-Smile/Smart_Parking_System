import type { Metadata } from "next";
import { CustomerDataShell } from "@/features/customer/CustomerDataShell";
import CustomerBookingsPage from "@/features/customer/booking/CustomerBookingsPage";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "../data-pages.css";

export const metadata: Metadata = { title: "My Bookings | ParkWise" };

export default function BookingsRoute() {
    return (
        <CustomerDataShell
            active="bookings"
            fontClassName={parkwiseFont.className}
        >
            <CustomerBookingsPage />
        </CustomerDataShell>
    );
}
