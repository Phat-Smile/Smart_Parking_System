import type { Metadata } from "next";
import { CustomerDataShell } from "@/features/customer/CustomerDataShell";
import { CustomerReceiptPage } from "@/features/customer/CustomerReceiptPage";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import "../../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "../../data-pages.css";

export const metadata: Metadata = {
    title: "Electronic Parking Receipt | ParkWise",
    description:
        "Detailed electronic parking receipt with duration, surge rate, and deposit settlement",
};

interface ReceiptRouteProps {
    params: Promise<{ sessionId: string }>;
}

export default async function ReceiptRoute({ params }: ReceiptRouteProps) {
    const { sessionId } = await params;
    return (
        <CustomerDataShell
            active="history"
            fontClassName={parkwiseFont.className}
            requireAuth={false}
        >
            <CustomerReceiptPage sessionId={sessionId} />
        </CustomerDataShell>
    );
}
