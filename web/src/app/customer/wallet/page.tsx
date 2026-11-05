import type { Metadata } from "next";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import { CustomerAccessGuard } from "@/features/customer/CustomerAccessGuard";
import {
    CustomerFooter,
    CustomerHeader,
} from "@/features/customer/CustomerShell";
import { CustomerWalletScreen } from "@/features/customer/wallet/WalletRoutes";
import { WalletPageIntro } from "@/features/customer/wallet/WalletPageCopy";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "./wallet.css";

export const metadata: Metadata = {
    title: "My Wallet | ParkWise",
    description: "Check your balance, top up with VNPay, and view wallet activity.",
};

export default function CustomerWalletRoute() {
    return (
        <CustomerAccessGuard>
            <div
                className={`parkwise-page customer-shell-page customer-wallet-page ${parkwiseFont.className}`}
            >
                <CustomerHeader active="wallet" />
                <main className="wallet-page-main">
                    <WalletPageIntro />
                    <CustomerWalletScreen />
                </main>
                <CustomerFooter />
            </div>
        </CustomerAccessGuard>
    );
}
