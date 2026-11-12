"use client";

import { useState } from "react";
import Link from "next/link";
import { getParkingFeeReceiptApi } from "./receiptApi";

export function CustomerParkingFeeReturn({
    sessionId,
    transactionId,
}: {
    sessionId: string;
    transactionId: string;
}) {
    const [checking, setChecking] = useState(false);
    const [confirmed, setConfirmed] = useState(false);
    const [message, setMessage] = useState("");

    async function checkReceipt() {
        setChecking(true);
        setMessage("");
        try {
            const receipt = await getParkingFeeReceiptApi(sessionId);
            if (receipt.paymentId !== transactionId)
                throw new Error("Receipt belongs to another transaction");
            setConfirmed(true);
        } catch {
            setMessage(
                "Payment is still being confirmed, or the receipt is unavailable. Try again shortly.",
            );
        } finally {
            setChecking(false);
        }
    }

    return (
        <section>
            <h1>Parking fee payment</h1>
            <p>
                Returning from VNPay does not confirm payment. Check the receipt
                after the payment is processed.
            </p>
            <button
                type="button"
                disabled={checking}
                onClick={() => void checkReceipt()}
            >
                {checking ? "Checking..." : "Check receipt"}
            </button>
            {confirmed && (
                <p role="status">
                    Payment confirmed.{" "}
                    <Link
                        href={`/customer/receipts/${encodeURIComponent(sessionId)}`}
                    >
                        View receipt
                    </Link>
                </p>
            )}
            {message && <p role="status">{message}</p>}
            <p>
                <Link href="/customer/sessions">Back to parking sessions</Link>
            </p>
        </section>
    );
}
