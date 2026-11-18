import type { Metadata } from "next";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import { CustomerDataShell } from "@/features/customer/CustomerDataShell";
import { CustomerBookingPassPage } from "@/features/customer/booking/CustomerBookingPassPage";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "../../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "../../data-pages.css";

export const metadata: Metadata = {
    title: "Driver Booking Pass | ParkWise",
    description:
        "Official Driver Booking Pass with scannable QR Code and barrier access token",
};

interface BookingPassRouteProps {
    params: Promise<{ id: string }>;
}

export default async function BookingPassRoute({
    params,
}: BookingPassRouteProps) {
    const { id } = await params;
    return (
        <CustomerAccessGuard>
            <CustomerDataShell
                active="bookings"
                fontClassName={parkwiseFont.className}
            >
                <CustomerBookingPassPage bookingId={id} />
            </CustomerDataShell>
        </CustomerAccessGuard>
    );
}
