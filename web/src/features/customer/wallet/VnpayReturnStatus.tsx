"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
    describeWalletError,
    formatVnd,
    getMyWallet,
    getWalletTopup,
    type WalletBalance,
    type WalletTopupStatus,
} from "./walletApi";

const POLL_MS = 3000;
const MAX_POLLS = 10;

/**
 * D03.4: the page VNPay sends the customer back to. The query string is not trusted: the result is read
 * from Payment, which only changes after the signed IPN (D03.5).
 */
export function VnpayReturnStatus({ topupId }: { topupId: string | null }) {
    const [status, setStatus] = useState<WalletTopupStatus | null>(null);
    const [error, setError] = useState("");
    const [gaveUp, setGaveUp] = useState(false);
    const [wallet, setWallet] = useState<WalletBalance | null>(null);
    const succeeded = status?.status === "Succeeded";

    // Once Payment credited the top-up, read the new balance (the IPN may arrive after the first read).
    useEffect(() => {
        if (!succeeded) return;
        let active = true;
        getMyWallet()
            .then((data) => active && setWallet(data))
            .catch(() => {
                // The wallet page shows the balance and its own error.
            });
        return () => {
            active = false;
        };
    }, [succeeded]);

    useEffect(() => {
        if (!topupId) return;
        let active = true;
        let timer: ReturnType<typeof setTimeout> | undefined;
        let polls = 0;

        async function poll() {
            polls += 1;
            try {
                const current = await getWalletTopup(topupId!);
                if (!active) return;
                setStatus(current);
                if (current.status === "Pending") {
                    if (polls < MAX_POLLS) timer = setTimeout(poll, POLL_MS);
                    else setGaveUp(true);
                }
            } catch (err) {
                if (active) setError(describeWalletError(err));
            }
        }

        void poll();
        return () => {
            active = false;
            if (timer) clearTimeout(timer);
        };
    }, [topupId]);

    if (!topupId)
        return (
            <p role="alert">
                Thiếu mã giao dịch từ VNPay. Hãy xem lại số dư trong{" "}
                <Link href="/customer/wallet">Ví của tôi</Link>.
            </p>
        );
    if (error) return <p role="alert">{error}</p>;
    if (!status) return <p>Đang kiểm tra giao dịch...</p>;

    return (
        <div>
            <p>
                Giao dịch <code>{status.topupId}</code> ·{" "}
                {formatVnd(status.amount)}
            </p>
            {succeeded && (
                <>
                    <p>Nạp tiền thành công. Số dư đã được cộng.</p>
                    {wallet && (
                        <p>
                            Số dư hiện tại:{" "}
                            <strong data-testid="return-balance">
                                {formatVnd(wallet.balance)}
                            </strong>
                        </p>
                    )}
                </>
            )}
            {status.status === "Pending" && (
                <p>
                    {gaveUp
                        ? "Giao dịch vẫn đang chờ xác nhận từ ngân hàng. Bạn có thể quay lại ví sau ít phút."
                        : "Giao dịch đang chờ xác nhận từ ngân hàng..."}
                </p>
            )}
            {status.status === "Failed" && <p>Giao dịch không thành công.</p>}
            {status.status === "Expired" && (
                <p>Giao dịch đã hết hạn, chưa có tiền được trừ.</p>
            )}
            <Link href="/customer/wallet">Về Ví của tôi</Link>
        </div>
    );
}
