import type { Metadata } from "next";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import { VnpayReturnScreen } from "@/features/customer/wallet/WalletRoutes";
import { VnpayReturnIntro } from "@/features/customer/wallet/WalletPageCopy";
import {
    CustomerFooter,
    CustomerHeader,
} from "@/features/customer/CustomerShell";
import "../../parkwise.css";
import "@/features/customer/customer-shell.css";
import "../../customer/wallet/wallet.css";

export const metadata: Metadata = {
    title: "Top-up Result | ParkWise",
    description: "Check the status of your ParkWise wallet top-up.",
};

// Default VNPAY_RETURN_URL (.env.example): http://localhost:3000/payment/vnpay-return
export default async function VnpayReturnRoute({
    searchParams,
}: {
    searchParams: Promise<{ vnp_TxnRef?: string | string[] }>;
}) {
    const { vnp_TxnRef } = await searchParams;
    const topupId =
        typeof vnp_TxnRef === "string" && vnp_TxnRef ? vnp_TxnRef : null;
    return (
        <div
            className={`parkwise-page customer-shell-page customer-wallet-page wallet-return-page ${parkwiseFont.className}`}
        >
            <CustomerHeader active="wallet" />
            <main className="wallet-return-main">
                <VnpayReturnIntro />
                <VnpayReturnScreen topupId={topupId} />
            </main>
            <CustomerFooter />
        </div>
    );
}
