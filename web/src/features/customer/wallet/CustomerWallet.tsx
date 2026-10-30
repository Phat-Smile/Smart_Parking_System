"use client";

import { useEffect, useRef, useState } from "react";
import {
    createWalletTopup,
    describeWalletError,
    formatVnd,
    getMyWallet,
    getWalletTransactions,
    type WalletBalance,
    type WalletTransactionsPage,
} from "./walletApi";

type Props = {
    /** Opens the VNPay payment page; the page passes window.location.assign. */
    onRedirect: (url: string) => void;
    /** New Idempotency-Key for a new top-up. */
    newKey: () => string;
};

/** Demo 3 (D03.2, D03.3, D03.6): balance, top-up through VNPay sandbox and wallet history. */
export function CustomerWallet({ onRedirect, newKey }: Props) {
    const [wallet, setWallet] = useState<WalletBalance | null>(null);
    const [walletError, setWalletError] = useState("");
    const [history, setHistory] = useState<WalletTransactionsPage | null>(null);
    const [historyError, setHistoryError] = useState("");
    const [amount, setAmount] = useState("50000");
    const [submitting, setSubmitting] = useState(false);
    const [topupError, setTopupError] = useState("");
    // One key per top-up: kept while the customer retries the same amount, dropped when it changes.
    const pendingKey = useRef<{ amount: number; key: string } | null>(null);

    useEffect(() => {
        let active = true;
        getMyWallet()
            .then((data) => active && setWallet(data))
            .catch((err) => active && setWalletError(describeWalletError(err)));
        getWalletTransactions()
            .then((data) => active && setHistory(data))
            .catch(
                (err) => active && setHistoryError(describeWalletError(err)),
            );
        return () => {
            active = false;
        };
    }, []);

    async function handleTopUp() {
        setTopupError("");
        const value = Number(amount);
        const limits = wallet?.limits;
        if (
            !Number.isInteger(value) ||
            (limits && (value < limits.minTopUp || value > limits.maxTopUp))
        ) {
            setTopupError(
                limits
                    ? `Số tiền nạp phải là số nguyên từ ${formatVnd(limits.minTopUp)} đến ${formatVnd(limits.maxTopUp)}.`
                    : "Số tiền nạp không hợp lệ.",
            );
            return;
        }
        if (pendingKey.current?.amount !== value) {
            pendingKey.current = { amount: value, key: newKey() };
        }

        setSubmitting(true);
        try {
            const result = await createWalletTopup(
                value,
                pendingKey.current.key,
            );
            if (!result.paymentUrl) {
                setTopupError("Payment chưa trả liên kết thanh toán VNPay.");
                return;
            }
            pendingKey.current = null;
            onRedirect(result.paymentUrl);
        } catch (err) {
            setTopupError(describeWalletError(err));
        } finally {
            setSubmitting(false);
        }
    }

    return (
        <div style={{ display: "grid", gap: 24 }}>
            <section aria-labelledby="wallet-balance-title">
                <h2 id="wallet-balance-title">Số dư</h2>
                {walletError ? (
                    <p role="alert">{walletError}</p>
                ) : !wallet ? (
                    <p>Đang tải số dư...</p>
                ) : (
                    <>
                        <p
                            data-testid="wallet-balance"
                            style={{ fontSize: 32, fontWeight: 700, margin: 0 }}
                        >
                            {formatVnd(wallet.balance)}
                        </p>
                        {wallet.isDemo && <small>{wallet.demoNotice}</small>}
                    </>
                )}
            </section>

            <section aria-labelledby="wallet-topup-title">
                <h2 id="wallet-topup-title">Nạp tiền</h2>
                <label htmlFor="wallet-topup-amount">Số tiền nạp (VND)</label>
                <input
                    id="wallet-topup-amount"
                    type="text"
                    inputMode="numeric"
                    value={amount}
                    onChange={(e) => setAmount(e.target.value.trim())}
                    disabled={submitting}
                    style={{ display: "block", margin: "6px 0 10px" }}
                />
                <button
                    type="button"
                    onClick={handleTopUp}
                    disabled={submitting || !wallet}
                >
                    {submitting ? "Đang tạo yêu cầu..." : "Nạp tiền qua VNPay"}
                </button>
                {topupError && <p role="alert">{topupError}</p>}
            </section>

            <section aria-labelledby="wallet-history-title">
                <h2 id="wallet-history-title">Lịch sử giao dịch</h2>
                {historyError ? (
                    <p role="alert">{historyError}</p>
                ) : !history ? (
                    <p>Đang tải lịch sử...</p>
                ) : history.items.length === 0 ? (
                    <p>Chưa có giao dịch nào.</p>
                ) : (
                    <table>
                        <thead>
                            <tr>
                                <th>Thời điểm</th>
                                <th>Loại</th>
                                <th>Số tiền</th>
                                <th>Số dư sau</th>
                            </tr>
                        </thead>
                        <tbody>
                            {history.items.map((item) => (
                                <tr key={item.entryId}>
                                    <td>
                                        {new Date(
                                            item.createdAtUtc,
                                        ).toLocaleString("vi-VN")}
                                    </td>
                                    <td>{item.entryType}</td>
                                    <td>{formatVnd(item.balanceDelta)}</td>
                                    <td>{formatVnd(item.balanceAfter)}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            </section>
        </div>
    );
}
