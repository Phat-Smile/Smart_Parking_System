import type { Metadata } from "next";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import { CustomerSessionsList } from "@/features/customer/CustomerSessions";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";

export const metadata: Metadata = {
    title: "My Sessions | ParkWise",
    description: "Your open parking sessions and their session QR codes.",
};

export default function CustomerSessionsRoute() {
    return (
        <CustomerAccessGuard>
            <main
                className={parkwiseFont.className}
                style={{
                    maxWidth: 880,
                    margin: "0 auto",
                    padding: "32px 16px",
                }}
            >
                <h1>Phiên đỗ của tôi</h1>
                <p>
                    Đưa QR phiên đỗ cho nhân viên ở cổng ra. Đây không phải mã
                    thanh toán.
                </p>
                <CustomerSessionsList />
            </main>
        </CustomerAccessGuard>
    );
}
