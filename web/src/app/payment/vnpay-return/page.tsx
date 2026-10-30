import type { Metadata } from "next";
import { parkwiseFont } from "@/features/parkwise/parkwiseFont";
import { VnpayReturnScreen } from "@/features/customer/wallet/WalletRoutes";
import "../../parkwise.css";

export const metadata: Metadata = {
    title: "Top-up result | ParkWise",
    description: "Result of a VNPay sandbox wallet top-up.",
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
        <main
            className={parkwiseFont.className}
            style={{ maxWidth: 720, margin: "0 auto", padding: "32px 16px" }}
        >
            <h1>Kết quả nạp tiền</h1>
            <VnpayReturnScreen topupId={topupId} />
        </main>
    );
}
