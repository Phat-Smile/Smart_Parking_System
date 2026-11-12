"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useCustomerLanguage } from "../i18n/customerLanguage";
import { getGatewayBaseUrl } from "@/shared/config/env";
import {
    ArrowLeft,
    CircleCheck,
    CircleX,
    Clock3,
    AlertCircle,
    ArrowRight,
    ShieldCheck,
    Wallet,
} from "lucide-react";
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
    const { copy, language } = useCustomerLanguage();
    const [status, setStatus] = useState<WalletTopupStatus | null>(null);
    const [error, setError] = useState("");
    const [gaveUp, setGaveUp] = useState(false);
    const [wallet, setWallet] = useState<WalletBalance | null>(null);
    const [nowMs, setNowMs] = useState(() => Date.now());
    const succeeded = status?.status === "Succeeded";
    const expiresAtMs = status ? Date.parse(status.expiresAtUtc) : NaN;
    const remainingSeconds = Number.isFinite(expiresAtMs)
        ? Math.max(0, Math.ceil((expiresAtMs - nowMs) / 1000))
        : null;

    useEffect(() => {
        if (status?.status !== "Pending") return;
        const timer = setInterval(() => setNowMs(Date.now()), 1000);
        return () => clearInterval(timer);
    }, [status?.status]);

    // VNPay cannot reach an IPN URL on localhost. For local sandbox demos only,
    // relay its signed browser return to the existing public IPN endpoint.
    // Payment verifies the signature, amount, transaction and idempotency.
    useEffect(() => {
        if (
            !topupId ||
            !["localhost", "127.0.0.1"].includes(window.location.hostname)
        )
            return;
        const params = new URLSearchParams(window.location.search);
        if (
            params.get("vnp_TxnRef") !== topupId ||
            !params.get("vnp_SecureHash") ||
            !params.get("vnp_Amount") ||
            !params.get("vnp_ResponseCode") ||
            !params.get("vnp_TransactionStatus")
        )
            return;

        const ipnUrl = new URL("/api/payment/vnpay/ipn", getGatewayBaseUrl());
        ipnUrl.search = window.location.search;
        void fetch(ipnUrl, { method: "GET", credentials: "omit" }).catch(() => {
            // Polling below still displays Payment's authoritative state.
        });
    }, [topupId]);

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
                    const expiry = Date.parse(current.expiresAtUtc);
                    if (
                        Number.isFinite(expiry)
                            ? Date.now() <= expiry + POLL_MS
                            : polls < MAX_POLLS
                    )
                        timer = setTimeout(poll, POLL_MS);
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
    }, [language, topupId]);

    if (!topupId)
        return (
            <div className="wallet-return-state" role="alert">
                <AlertCircle size={46} aria-hidden="true" />
                <h2>
                    {copy(
                        "Missing transaction reference",
                        "Thiếu mã giao dịch",
                    )}
                </h2>
                <p>
                    {copy(
                        "The VNPay transaction reference is missing. Please check your wallet balance.",
                        "Không có mã giao dịch VNPay. Vui lòng kiểm tra số dư ví.",
                    )}
                </p>
                <Link href="/customer/wallet">
                    <ArrowLeft size={17} aria-hidden="true" />{" "}
                    {copy("Back to My Wallet", "Về Ví của tôi")}
                </Link>
            </div>
        );
    if (error)
        return (
            <div className="wallet-return-state" role="alert">
                <AlertCircle size={46} aria-hidden="true" />
                <h2>
                    {copy(
                        "Could not check this transaction",
                        "Không thể kiểm tra giao dịch",
                    )}
                </h2>
                <p>{error}</p>
                <Link href="/customer/wallet">
                    <ArrowLeft size={17} aria-hidden="true" />{" "}
                    {copy("Back to My Wallet", "Về Ví của tôi")}
                </Link>
            </div>
        );
    if (!status)
        return (
            <div className="wallet-return-state">
                <Clock3 size={46} aria-hidden="true" />
                <h2>
                    {copy(
                        "Checking your transaction...",
                        "Đang kiểm tra giao dịch...",
                    )}
                </h2>
                <p>
                    {copy(
                        "ParkWise is checking the payment status.",
                        "ParkWise đang kiểm tra trạng thái thanh toán.",
                    )}
                </p>
            </div>
        );

    const statusLabel = succeeded
        ? copy("Successful", "Thành công")
        : status.status === "Pending"
          ? copy("Pending", "Đang chờ")
          : status.status === "Expired"
            ? copy("Expired", "Hết hạn")
            : copy("Failed", "Thất bại");

    return (
        <div
            className={`wallet-return-result-grid wallet-return-${status.status.toLowerCase()}`}
        >
            <section
                className="wallet-return-result-card"
                aria-labelledby="wallet-return-heading"
            >
                <div className="wallet-return-status-icon">
                    {succeeded ? (
                        <CircleCheck size={34} aria-hidden="true" />
                    ) : status.status === "Pending" ||
                      status.status === "Expired" ? (
                        <Clock3 size={34} aria-hidden="true" />
                    ) : (
                        <CircleX size={34} aria-hidden="true" />
                    )}
                </div>
                <span className="wallet-return-eyebrow">
                    {copy("WALLET TOP-UP", "GIAO DỊCH NẠP VÍ")}
                </span>
                <h2 id="wallet-return-heading">
                    {succeeded
                        ? copy("Top-up successful", "Nạp tiền thành công")
                        : status.status === "Pending"
                          ? copy(
                                "Waiting for confirmation",
                                "Đang chờ xác nhận",
                            )
                          : status.status === "Expired"
                            ? copy(
                                  "Payment link expired",
                                  "Liên kết thanh toán đã hết hạn",
                              )
                            : copy("Top-up failed", "Nạp tiền thất bại")}
                </h2>
                {status.status !== "Succeeded" &&
                    (status.status !== "Pending" || gaveUp) && (
                        <p className="wallet-return-result-copy">
                            {status.status === "Pending"
                                ? gaveUp
                                    ? copy(
                                          "Confirmation is taking longer than expected. Check your wallet again in a few minutes.",
                                          "Xác nhận đang lâu hơn dự kiến. Vui lòng kiểm tra lại ví sau vài phút.",
                                      )
                                    : copy(
                                          "ParkWise is waiting for confirmation from VNPay.",
                                          "ParkWise đang chờ VNPay xác nhận.",
                                      )
                                : status.status === "Expired"
                                  ? copy(
                                        "This payment link has expired. If you already paid, your balance may still update when VNPay confirms it.",
                                        "Liên kết thanh toán đã hết hạn. Nếu đã thanh toán, số dư vẫn có thể cập nhật khi VNPay xác nhận.",
                                    )
                                  : copy(
                                        "This amount has not been added to your wallet.",
                                        "Số tiền này chưa được cộng vào ví.",
                                    )}
                        </p>
                    )}
                {status.status === "Pending" &&
                    remainingSeconds !== null &&
                    !gaveUp && (
                        <div
                            className="wallet-return-countdown"
                            role="timer"
                            aria-live="off"
                        >
                            <span>
                                {copy("Time remaining", "Thời gian còn lại")}
                            </span>
                            <strong>
                                {remainingSeconds > 0
                                    ? `${String(Math.floor(remainingSeconds / 60)).padStart(2, "0")}:${String(remainingSeconds % 60).padStart(2, "0")}`
                                    : copy("Checking…", "Đang kiểm tra…")}
                            </strong>
                        </div>
                    )}
                <div className="wallet-return-amount-block">
                    <span>{copy("TOP-UP AMOUNT", "SỐ TIỀN NẠP")}</span>
                    <strong className="wallet-return-amount">
                        {formatVnd(status.amount)}
                    </strong>
                </div>
                {succeeded && wallet && (
                    <div className="wallet-return-balance">
                        <span className="wallet-return-balance-icon">
                            <Wallet size={22} aria-hidden="true" />
                        </span>
                        <span>
                            {copy(
                                "Current wallet balance",
                                "Số dư ví hiện tại",
                            )}
                            <strong data-testid="return-balance">
                                {formatVnd(wallet.balance)}
                            </strong>
                        </span>
                    </div>
                )}
                <Link className="wallet-return-action" href="/customer/wallet">
                    {copy("Back to My Wallet", "Về Ví của tôi")}{" "}
                    <ArrowRight size={18} aria-hidden="true" />
                </Link>
            </section>

            <aside
                className="wallet-return-detail-card"
                aria-label={copy(
                    "Top-up transaction details",
                    "Chi tiết giao dịch nạp ví",
                )}
            >
                <span className="wallet-return-eyebrow">
                    {copy("TRANSACTION DETAILS", "THÔNG TIN GIAO DỊCH")}
                </span>
                <h3>{copy("Top-up details", "Chi tiết nạp ví")}</h3>
                <dl>
                    <div>
                        <dt>{copy("Payment method", "Phương thức")}</dt>
                        <dd>VNPay</dd>
                    </div>
                    <div>
                        <dt>{copy("Status", "Trạng thái")}</dt>
                        <dd>
                            <span className="wallet-return-status-badge">
                                {statusLabel}
                            </span>
                        </dd>
                    </div>
                    <div>
                        <dt>{copy("Transaction reference", "Mã giao dịch")}</dt>
                        <dd>
                            <code>{status.topupId}</code>
                        </dd>
                    </div>
                </dl>
                <div className="wallet-return-note">
                    <ShieldCheck size={20} aria-hidden="true" />
                    <p>
                        {copy(
                            "Payments are processed securely by VNPay.",
                            "Thanh toán được xử lý an toàn qua VNPay.",
                        )}
                    </p>
                </div>
            </aside>
        </div>
    );
}
