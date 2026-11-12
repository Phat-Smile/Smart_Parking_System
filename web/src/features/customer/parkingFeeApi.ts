import { gatewayFetch } from "@/shared/api/client";

export type ParkingSessionFee = {
    sessionId: string;
    plateNumber: string;
    durationMinutes: number;
    totalFee: number;
    depositAmount: number;
    amountDue: number;
    calculatedAtUtc: string;
};

export type ParkingFeePaymentStatus = {
    transactionId: string;
    sessionId: string;
    method: string;
    status: string;
    amount: number;
    paymentUrl: string | null;
    expiresAtUtc: string | null;
    failureCode: string | null;
};

export type WalletFeePayment = {
    transactionId: string;
    sessionId: string;
    amountPaid: number;
    walletBalanceRemaining: number;
    exitWindowExpiresUtc: string;
};

export type VnPayFeePayment = {
    transactionId: string;
    paymentUrl: string;
    amount: number;
    expiresAtUtc: string;
    status: string;
};

const feePath = (sessionId: string) =>
    `/api/payment/parking-sessions/${encodeURIComponent(sessionId)}`;

export function getParkingSessionFee(sessionId: string) {
    return gatewayFetch<ParkingSessionFee>(`${feePath(sessionId)}/fee`);
}

export function payParkingFeeByWallet(
    sessionId: string,
    expectedAmountDue: number,
    idempotencyKey: string,
) {
    return gatewayFetch<WalletFeePayment>(`${feePath(sessionId)}/pay/wallet`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "X-Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify({ expectedAmountDue }),
    });
}

export function createVnPayParkingFeePayment(
    sessionId: string,
    expectedAmountDue: number,
    idempotencyKey: string,
) {
    return gatewayFetch<VnPayFeePayment>(`${feePath(sessionId)}/pay/vnpay`, {
        method: "POST",
        headers: {
            "Content-Type": "application/json",
            "X-Idempotency-Key": idempotencyKey,
        },
        body: JSON.stringify({ expectedAmountDue }),
    });
}
