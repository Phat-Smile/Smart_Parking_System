"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useCustomerLanguage } from "../i18n/customerLanguage";
import {
    ArrowDownLeft,
    ArrowRight,
    ArrowUpRight,
    Clock3,
    CreditCard,
    History,
    RefreshCw,
    ShieldCheck,
    Wallet,
} from "lucide-react";
import {
    createWalletTopup,
    describeWalletError,
    formatVnd,
    getMyWallet,
    getWalletTransactions,
    type WalletBalance,
    type WalletTransaction,
    type WalletTransactionsPage,
} from "./walletApi";

type Props = {
    onRedirect: (url: string) => void;
    onOpenStatus: (topupId: string) => void;
    newKey: () => string;
};
const SUGGESTED_AMOUNTS = [50000, 100000, 200000, 500000];

function transactionLabel(item: WalletTransaction) {
    if (item.entryType === "TopUp") return "Top-up";
    if (item.entryType === "FeePayment") return "Parking payment";
    if (item.entryType === "Refund") return "Refund";
    return item.entryType;
}

function transactionDate(value: string) {
    return new Date(value).toLocaleString("vi-VN", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
    });
}

function formatTimeRemaining(expiresAtUtc: string, nowMs: number) {
    const expiry = Date.parse(expiresAtUtc);
    if (!Number.isFinite(expiry)) return null;
    const seconds = Math.max(0, Math.ceil((expiry - nowMs) / 1000));
    return `${String(Math.floor(seconds / 60)).padStart(2, "0")}:${String(seconds % 60).padStart(2, "0")}`;
}

export function CustomerWallet({ onRedirect, onOpenStatus, newKey }: Props) {
    const { copy } = useCustomerLanguage();
    const [wallet, setWallet] = useState<WalletBalance | null>(null);
    const [walletError, setWalletError] = useState("");
    const [history, setHistory] = useState<WalletTransactionsPage | null>(null);
    const [historyError, setHistoryError] = useState("");
    const [amount, setAmount] = useState("50000");
    const [submitting, setSubmitting] = useState(false);
    const [topupError, setTopupError] = useState("");
    const [reloadCount, setReloadCount] = useState(0);
    const [historyPage, setHistoryPage] = useState(1);
    const [nowMs, setNowMs] = useState(() => Date.now());
    const pendingKey = useRef<{ amount: number; key: string } | null>(null);
    const pendingIds =
        wallet?.pendingTopUps.map((topup) => topup.topupId).join(",") ?? "";
    const activePendingTopUps = wallet?.pendingTopUps.filter((topup) => {
        const expiry = Date.parse(topup.expiresAtUtc ?? "");
        return !Number.isFinite(expiry) || expiry > nowMs;
    }) ?? [];

    useEffect(() => {
        if (!pendingIds) return;
        const timer = setInterval(() => setNowMs(Date.now()), 1000);
        return () => clearInterval(timer);
    }, [pendingIds]);

    useEffect(() => {
        let active = true;
        getMyWallet()
            .then((data) => {
                if (active) setWallet(data);
            })
            .catch((error) => {
                if (active) setWalletError(describeWalletError(error));
            });
        getWalletTransactions(historyPage)
            .then((data) => {
                if (active) setHistory(data);
            })
            .catch((error) => {
                if (active) setHistoryError(describeWalletError(error));
            });
        return () => {
            active = false;
        };
    }, [reloadCount, historyPage]);

    useEffect(() => {
        if (!pendingIds) return;
        let active = true;
        let inFlight = false;
        let checks = 0;
        const timer = setInterval(async () => {
            // Keep checking while a saved payment link may still be valid.
            if (checks >= 240) {
                clearInterval(timer);
                return;
            }
            if (inFlight) return;
            inFlight = true;
            checks += 1;
            try {
                const updated = await getMyWallet();
                if (!active) return;
                setWallet(updated);
                const nextIds = updated.pendingTopUps
                    .map((topup) => topup.topupId)
                    .join(",");
                if (nextIds !== pendingIds) {
                    const nextHistory =
                        await getWalletTransactions(historyPage);
                    if (active) setHistory(nextHistory);
                }
            } catch {
                // The manual refresh button remains available if a check fails.
            } finally {
                inFlight = false;
            }
        }, 5000);
        return () => {
            active = false;
            clearInterval(timer);
        };
    }, [pendingIds, historyPage]);

    function refreshWallet() {
        setWallet(null);
        setHistory(null);
        setWalletError("");
        setHistoryError("");
        setReloadCount((count) => count + 1);
    }

    function goToHistoryPage(page: number) {
        setHistory(null);
        setHistoryError("");
        setHistoryPage(page);
    }

    async function handleTopUp() {
        setTopupError("");
        const value = Number(amount);
        const limits = wallet?.limits;
        if (
            !amount ||
            !Number.isInteger(value) ||
            (limits && (value < limits.minTopUp || value > limits.maxTopUp))
        ) {
            setTopupError(
                limits
                    ? `Enter a whole amount from ${formatVnd(limits.minTopUp)} to ${formatVnd(limits.maxTopUp)}.`
                    : "Enter a valid top-up amount.",
            );
            return;
        }
        if (pendingKey.current?.amount !== value)
            pendingKey.current = { amount: value, key: newKey() };
        setSubmitting(true);
        try {
            const result = await createWalletTopup(
                value,
                pendingKey.current.key,
            );
            pendingKey.current = null;
            if (!result.paymentUrl) {
                // Replay of a top-up that is already settled: there is nothing to pay, show its result.
                if (
                    result.isReplay ||
                    (result.topupId && result.status !== "Pending")
                ) {
                    onOpenStatus(result.topupId);
                } else {
                    setTopupError(
                        "The payment link is unavailable. Please try again.",
                    );
                }
                return;
            }
            onRedirect(result.paymentUrl);
        } catch (error) {
            setTopupError(describeWalletError(error));
        } finally {
            setSubmitting(false);
        }
    }

    const suggestions = SUGGESTED_AMOUNTS.filter(
        (value) =>
            !wallet ||
            (value >= wallet.limits.minTopUp &&
                value <= wallet.limits.maxTopUp),
    );

    return (
        <div className="wallet-layout">
            <div className="wallet-main-column">
                <section
                    className="wallet-balance-card"
                    aria-labelledby="wallet-balance-title"
                >
                    <div className="wallet-balance-card-top">
                        <span className="wallet-balance-mark">
                            <Wallet size={21} aria-hidden="true" />
                        </span>
                        <span className="wallet-balance-card-name">
                            PARKWISE WALLET
                        </span>
                        <span className="wallet-balance-card-currency">
                            VND
                        </span>
                    </div>
                    <div className="wallet-balance-content">
                        <h2 id="wallet-balance-title">{copy("Available balance", "Số dư khả dụng")}</h2>
                        {walletError ? (
                            <p className="wallet-balance-error" role="alert">
                                {walletError}
                            </p>
                        ) : !wallet ? (
                            <p className="wallet-balance-loading">
                                {copy("Loading balance...", "Đang tải số dư...")}
                            </p>
                        ) : (
                            <p
                                className="wallet-balance-value"
                                data-testid="wallet-balance"
                            >
                                {formatVnd(wallet.balance)}
                            </p>
                        )}
                        <p className="wallet-balance-caption">
                            {copy("Ready for your next parking trip", "Sẵn sàng cho chuyến đỗ xe tiếp theo")}
                        </p>
                    </div>
                    <div className="wallet-balance-card-bottom">
                        <span>
                            <ShieldCheck size={17} aria-hidden="true" /> {copy("Your ParkWise wallet", "Ví ParkWise của bạn")}
                        </span>
                        <button
                            type="button"
                            onClick={refreshWallet}
                            aria-label={copy("Refresh wallet and transaction history", "Làm mới ví và lịch sử giao dịch")}
                        >
                            <RefreshCw size={17} aria-hidden="true" /> {copy("Refresh", "Làm mới")}
                        </button>
                    </div>
                </section>

                {wallet?.gatewayMode === "Sandbox" && (
                    <p className="wallet-demo-notice">
                        <ShieldCheck size={18} aria-hidden="true" />
                        <span>
                            {copy("This wallet balance is for sandbox testing only. It cannot be withdrawn, transferred, or linked to a bank account.", "Số dư ví chỉ dùng để thử nghiệm. Không thể rút, chuyển nhượng hoặc liên kết tài khoản ngân hàng.")}
                        </span>
                    </p>
                )}

                <section
                    className="wallet-panel wallet-history-panel"
                    aria-labelledby="wallet-history-title"
                >
                    <div className="wallet-panel-heading">
                        <div>
                            <span className="wallet-eyebrow">
                                {copy("YOUR ACTIVITY", "HOẠT ĐỘNG CỦA BẠN")}
                            </span>
                            <h2 id="wallet-history-title">{copy("Transaction history", "Lịch sử giao dịch")}</h2>
                        </div>
                        {history && (
                            <span className="wallet-history-count">
                                {history.totalItems} {copy("transactions", "giao dịch")}
                            </span>
                        )}
                    </div>
                    {historyError ? (
                        <div className="wallet-history-empty" role="alert">
                            {historyError}
                        </div>
                    ) : !history ? (
                        <div className="wallet-history-empty">
                            {copy("Loading history...", "Đang tải lịch sử...")}
                        </div>
                    ) : history.items.length === 0 ? (
                        <div className="wallet-history-empty">
                            <History size={28} aria-hidden="true" />
                            <strong>{copy("No transactions yet", "Chưa có giao dịch")}</strong>
                            <span>
                                {copy("Your top-ups and payments will appear here.", "Các lần nạp tiền và thanh toán sẽ xuất hiện tại đây.")}
                            </span>
                        </div>
                    ) : (
                        <>
                            <ul className="wallet-transactions">
                                {history.items.map((item) => (
                                    <li
                                        className="wallet-transaction"
                                        key={item.entryId}
                                    >
                                        <span
                                            className={`wallet-transaction-icon ${item.affectsBalance && item.balanceDelta > 0 ? "is-credit" : "is-debit"}`}
                                        >
                                            {item.affectsBalance &&
                                            item.balanceDelta > 0 ? (
                                                <ArrowDownLeft
                                                    size={21}
                                                    aria-hidden="true"
                                                />
                                            ) : (
                                                <ArrowUpRight
                                                    size={21}
                                                    aria-hidden="true"
                                                />
                                            )}
                                        </span>
                                        <span className="wallet-transaction-detail">
                                            <strong>
                                                {copy(transactionLabel(item), item.entryType === "TopUp" ? "Nạp tiền" : item.entryType === "FeePayment" ? "Thanh toán phí đỗ" : item.entryType === "Refund" ? "Hoàn tiền" : item.entryType)}
                                            </strong>
                                            <small>
                                                {transactionDate(
                                                    item.createdAtUtc,
                                                )}
                                            </small>
                                        </span>
                                        <span className="wallet-transaction-amount">
                                            <strong
                                                className={
                                                    item.affectsBalance &&
                                                    item.balanceDelta > 0
                                                        ? "is-credit"
                                                        : ""
                                                }
                                            >
                                                {item.affectsBalance ? (
                                                    <>
                                                        {item.balanceDelta > 0
                                                            ? "+"
                                                            : ""}
                                                        {formatVnd(
                                                            item.balanceDelta,
                                                        )}
                                                    </>
                                                ) : (
                                                    `${copy("No balance change", "Không đổi số dư")} (${formatVnd(item.amount)})`
                                                )}
                                            </strong>
                                            <small>
                                                {copy("Balance", "Số dư")}{" "}
                                                {formatVnd(item.balanceAfter)}
                                            </small>
                                        </span>
                                    </li>
                                ))}
                            </ul>
                            {history.totalPages > 1 && (
                                <nav
                                    className="wallet-history-pagination"
                                    aria-label={copy("Transaction history pages", "Các trang lịch sử giao dịch")}
                                >
                                    <button
                                        type="button"
                                        disabled={historyPage <= 1}
                                        onClick={() =>
                                            goToHistoryPage(historyPage - 1)
                                        }
                                    >
                                        {copy("Previous", "Trước")}
                                    </button>
                                    <span>
                                        {copy("Page", "Trang")} {history.page} /{" "}
                                        {history.totalPages}
                                    </span>
                                    <button
                                        type="button"
                                        disabled={
                                            historyPage >= history.totalPages
                                        }
                                        onClick={() =>
                                            goToHistoryPage(historyPage + 1)
                                        }
                                    >
                                        {copy("Next", "Sau")}
                                    </button>
                                </nav>
                            )}
                        </>
                    )}
                </section>
            </div>

            <div className="wallet-side-column">
                <section
                    className="wallet-panel wallet-topup-panel"
                    aria-labelledby="wallet-topup-title"
                >
                    <div className="wallet-panel-heading">
                        <div>
                            <span className="wallet-eyebrow">
                                {copy("QUICK WALLET TOP-UP", "NẠP VÍ NHANH")}
                            </span>
                            <h2 id="wallet-topup-title">{copy("Add money", "Nạp tiền")}</h2>
                        </div>
                        <span className="wallet-heading-icon">
                            <CreditCard size={21} aria-hidden="true" />
                        </span>
                    </div>
                    <p className="wallet-panel-intro">
                        {copy("Choose an amount, then pay securely through VNPay.", "Chọn số tiền, sau đó thanh toán an toàn qua VNPay.")}
                    </p>
                    <label
                        className="wallet-field-label"
                        htmlFor="wallet-topup-amount"
                    >
                        {copy("Top-up amount (VND)", "Số tiền nạp (VND)")}
                    </label>
                    <div className="wallet-amount-field">
                        <input
                            id="wallet-topup-amount"
                            type="text"
                            inputMode="numeric"
                            autoComplete="off"
                            value={amount.replace(/\B(?=(\d{3})+(?!\d))/g, ".")}
                            onChange={(event) => {
                                setAmount(
                                    event.target.value.replace(/\D/g, ""),
                                );
                                setTopupError("");
                            }}
                            disabled={submitting}
                            aria-describedby={
                                topupError
                                    ? "wallet-topup-error"
                                    : "wallet-topup-hint"
                            }
                            aria-invalid={Boolean(topupError)}
                        />
                        <span>₫</span>
                    </div>
                    <p className="wallet-field-hint" id="wallet-topup-hint">
                        {wallet
                            ? `${copy("From", "Từ")} ${formatVnd(wallet.limits.minTopUp)} ${copy("to", "đến")} ${formatVnd(wallet.limits.maxTopUp)} ${copy("per top-up.", "mỗi lần nạp.")}`
                            : copy("Enter the amount to add to your wallet.", "Nhập số tiền muốn nạp vào ví.")}
                    </p>
                    <div
                        className="wallet-amount-options"
                        aria-label={copy("Suggested amounts", "Số tiền gợi ý")}
                    >
                        {suggestions.map((value) => (
                            <button
                                type="button"
                                key={value}
                                className={
                                    amount === String(value)
                                        ? "is-selected"
                                        : ""
                                }
                                onClick={() => {
                                    setAmount(String(value));
                                    setTopupError("");
                                }}
                                disabled={submitting}
                                aria-pressed={amount === String(value)}
                            >
                                {new Intl.NumberFormat("vi-VN").format(value)}
                            </button>
                        ))}
                    </div>
                    {topupError && (
                        <p
                            className="wallet-form-error"
                            id="wallet-topup-error"
                            role="alert"
                        >
                            {topupError}
                        </p>
                    )}
                    <button
                        className="wallet-topup-submit"
                        type="button"
                        onClick={handleTopUp}
                        disabled={submitting || !wallet}
                    >
                        {submitting
                            ? copy("Creating payment...", "Đang tạo thanh toán...")
                            : copy("Top up with VNPay", "Nạp tiền qua VNPay")}
                        {!submitting && (
                            <ArrowRight size={19} aria-hidden="true" />
                        )}
                    </button>
                    <p className="wallet-payment-note">
                        <ShieldCheck size={16} aria-hidden="true" /> {copy("Payment is processed by VNPay", "Thanh toán được xử lý qua VNPay")}
                    </p>
                </section>

                {wallet?.expiredTopUps && wallet.expiredTopUps.length > 0 && (
                    <section
                        className="wallet-panel wallet-expired-panel"
                        aria-labelledby="wallet-expired-title"
                    >
                        <div className="wallet-panel-heading">
                            <div>
                                <span className="wallet-eyebrow">{copy("PAST REQUESTS", "YÊU CẦU TRƯỚC ĐÂY")}</span>
                                <h2 id="wallet-expired-title">{copy("Recent expired top-ups", "Yêu cầu nạp đã hết hạn")}</h2>
                            </div>
                            <Clock3 size={20} aria-hidden="true" />
                        </div>
                        <p className="wallet-expired-intro">
                            {copy("These payment links expired. If you paid before the deadline, a later VNPay confirmation can still update your balance.", "Các liên kết thanh toán này đã hết hạn. Nếu bạn đã trả tiền trước hạn, số dư vẫn có thể cập nhật khi VNPay xác nhận sau đó.")}
                        </p>
                        <ul className="wallet-expired-list">
                            {wallet.expiredTopUps.map((topup) => (
                                <li key={topup.topupId}>
                                    <span>
                                        <strong>{formatVnd(topup.amount)}</strong>
                                        <small>{copy("Expired", "Hết hạn")} {transactionDate(topup.expiresAtUtc)}</small>
                                    </span>
                                    <Link href={`/payment/vnpay-return?vnp_TxnRef=${encodeURIComponent(topup.topupId)}`}>
                                        {copy("View details", "Xem chi tiết")} <ArrowRight size={15} aria-hidden="true" />
                                    </Link>
                                </li>
                            ))}
                        </ul>
                    </section>
                )}

                {activePendingTopUps.length > 0 && (
                    <section
                        className="wallet-panel wallet-pending-panel"
                        aria-labelledby="wallet-pending-title"
                    >
                        <div className="wallet-panel-heading">
                            <div>
                                <span className="wallet-eyebrow">
                                    {copy("PAYMENT IN PROGRESS", "ĐANG THANH TOÁN")}
                                </span>
                                <h2 id="wallet-pending-title">
                                    {copy("Awaiting confirmation", "Đang chờ xác nhận")}
                                </h2>
                            </div>
                            <Clock3 size={20} aria-hidden="true" />
                        </div>
                        <ul>
                            {activePendingTopUps.map((topup) => {
                                const timeLeft = topup.expiresAtUtc
                                    ? formatTimeRemaining(topup.expiresAtUtc, nowMs)
                                    : null;
                                return (
                                    <li key={topup.topupId}>
                                        <span>
                                            <strong>{formatVnd(topup.amount)}</strong>
                                            <small>{transactionDate(topup.createdAtUtc)}</small>
                                            {timeLeft && (
                                                <small>{timeLeft} {copy("remaining to pay", "còn lại để thanh toán")}</small>
                                            )}
                                        </span>
                                        <Link
                                            href={`/payment/vnpay-return?vnp_TxnRef=${encodeURIComponent(topup.topupId)}`}
                                            aria-label={`${copy("View status of", "Xem trạng thái yêu cầu nạp")} ${formatVnd(topup.amount)}`}
                                        >
                                            {copy("View status", "Xem trạng thái")}{" "}
                                            <ArrowRight size={15} aria-hidden="true" />
                                        </Link>
                                    </li>
                                );
                            })}
                        </ul>
                    </section>
                )}
            </div>
        </div>
    );
}
