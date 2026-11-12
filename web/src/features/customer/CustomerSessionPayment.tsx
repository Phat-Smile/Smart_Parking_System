"use client";

import { useRef, useState } from "react";
import Link from "next/link";
import {
    createVnPayParkingFeePayment,
    getParkingSessionFee,
    payParkingFeeByWallet,
    type ParkingFeePaymentStatus,
    type ParkingSessionFee,
} from "./parkingFeeApi";
import { getParkingFeeReceiptApi } from "./receiptApi";

const money = (amount: number) =>
    `${new Intl.NumberFormat("vi-VN").format(amount)} ₫`;
const transactionKey = (sessionId: string) =>
    `parkwise:parking-fee-payment:${sessionId}`;

export function CustomerSessionPayment({ sessionId }: { sessionId: string }) {
    const [fee, setFee] = useState<ParkingSessionFee | null>(null);
    const [payment, setPayment] = useState<ParkingFeePaymentStatus | null>(
        null,
    );
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState("");
    const retryKey = useRef<{
        method: "wallet" | "vnpay";
        amount: number;
        key: string;
    } | null>(null);

    async function refreshPayment() {
        const transactionId = window.sessionStorage.getItem(
            transactionKey(sessionId),
        );
        if (!transactionId) {
            setError("No payment has been started for this parking session.");
            return;
        }
        setBusy(true);
        setError("");
        try {
            // The Payment status route currently lacks resource ownership checks.
            // The existing session receipt is used until Backend fixes that route.
            const receipt = await getParkingFeeReceiptApi(sessionId);
            if (receipt.paymentId !== transactionId)
                throw new Error("Receipt belongs to another payment");
            setPayment({
                transactionId,
                sessionId,
                method: receipt.paymentMethod,
                status: "Succeeded",
                amount: receipt.netPaidAmount,
                paymentUrl: null,
                expiresAtUtc: null,
                failureCode: null,
            });
        } catch {
            setError("Payment is not confirmed yet. Please retry shortly.");
        } finally {
            setBusy(false);
        }
    }

    async function loadFee() {
        setBusy(true);
        setError("");
        try {
            setFee(await getParkingSessionFee(sessionId));
            if (window.sessionStorage.getItem(transactionKey(sessionId)))
                await refreshPayment();
        } catch {
            setError(
                "Could not calculate the current parking fee. Please retry.",
            );
        } finally {
            setBusy(false);
        }
    }

    async function pay(method: "wallet" | "vnpay") {
        if (!fee) return;
        setBusy(true);
        setError("");
        try {
            const currentFee = await getParkingSessionFee(sessionId);
            if (currentFee.amountDue !== fee.amountDue) {
                setFee(currentFee);
                setError(
                    "The fee has changed. Review the new amount before paying.",
                );
                setBusy(false);
                return;
            }
        } catch {
            setError("Could not confirm the current fee. Please retry.");
            setBusy(false);
            return;
        }
        if (
            !retryKey.current ||
            retryKey.current.method !== method ||
            retryKey.current.amount !== fee.amountDue
        ) {
            retryKey.current = {
                method,
                amount: fee.amountDue,
                key: crypto.randomUUID(),
            };
        }
        try {
            if (method === "wallet") {
                const result = await payParkingFeeByWallet(
                    sessionId,
                    fee.amountDue,
                    retryKey.current.key,
                );
                window.sessionStorage.setItem(
                    transactionKey(sessionId),
                    result.transactionId,
                );
                setPayment({
                    transactionId: result.transactionId,
                    sessionId,
                    method: "Wallet",
                    status: "Succeeded",
                    amount: result.amountPaid,
                    paymentUrl: null,
                    expiresAtUtc: null,
                    failureCode: null,
                });
            } else {
                const result = await createVnPayParkingFeePayment(
                    sessionId,
                    fee.amountDue,
                    retryKey.current.key,
                );
                if (!/^https:\/\//i.test(result.paymentUrl))
                    throw new Error("Invalid payment link");
                const txnRef = new URL(result.paymentUrl).searchParams.get(
                    "vnp_TxnRef",
                );
                if (txnRef)
                    window.sessionStorage.setItem(
                        `parkwise:parking-fee-return:${txnRef}`,
                        JSON.stringify({
                            sessionId,
                            transactionId: result.transactionId,
                        }),
                    );
                window.sessionStorage.setItem(
                    transactionKey(sessionId),
                    result.transactionId,
                );
                window.location.assign(result.paymentUrl);
            }
            retryKey.current = null;
        } catch {
            setError(
                "Payment could not be completed. Refresh the fee and retry, or ask staff for help.",
            );
        } finally {
            setBusy(false);
        }
    }

    return (
        <div
            style={{
                flexBasis: "100%",
                borderTop: "1px solid #e3e8f2",
                paddingTop: 12,
            }}
        >
            <button
                type="button"
                disabled={busy}
                onClick={() => void loadFee()}
            >
                {fee ? "Refresh parking fee" : "View parking fee"}
            </button>
            {fee && (
                <div>
                    <p>
                        Parking fee: {money(fee.totalFee)} · Deposit:{" "}
                        {money(fee.depositAmount)} · Due now:{" "}
                        <strong>{money(fee.amountDue)}</strong>
                    </p>
                    {payment?.status.toLowerCase() !== "succeeded" && (
                        <button
                            type="button"
                            disabled={busy}
                            onClick={() => void pay("wallet")}
                        >
                            Pay from wallet
                        </button>
                    )}{" "}
                    {fee.amountDue > 0 &&
                        payment?.status.toLowerCase() !== "succeeded" && (
                            <button
                                type="button"
                                disabled={busy}
                                onClick={() => void pay("vnpay")}
                            >
                                Pay with VNPay
                            </button>
                        )}
                </div>
            )}
            {payment && (
                <p role="status">
                    Payment status: {payment.status}.{" "}
                    {payment.status.toLowerCase() === "succeeded" && (
                        <Link
                            href={`/customer/receipts/${encodeURIComponent(sessionId)}`}
                        >
                            View receipt
                        </Link>
                    )}
                </p>
            )}
            <button
                type="button"
                disabled={busy}
                onClick={() => void refreshPayment()}
            >
                Check payment status
            </button>
            {error && <p role="alert">{error}</p>}
        </div>
    );
}
