import type { Metadata } from "next";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import { CustomerWalletScreen } from "@/features/customer/wallet/WalletRoutes";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";

export const metadata: Metadata = {
    title: "Wallet | ParkWise",
    description: "Wallet balance, VNPay sandbox top-up and history.",
};

export default function CustomerWalletRoute() {
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
                <h1>Ví của tôi</h1>
                <CustomerWalletScreen />
            </main>
        </CustomerAccessGuard>
    );
}
