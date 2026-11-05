import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";

// Shapes and routes from docs/api/Payment-Wallet-Contract.md: the DTO on success, ProblemDetails on errors.

export interface WalletBalance {
    walletId: string | null;
    balance: number;
    currency: string;
    isDemo: boolean;
    demoNotice: string;
    limits: { minTopUp: number; maxTopUp: number; maxBalance: number };
    gatewayMode: string;
    pendingTopUps: Array<{
        topupId: string;
        amount: number;
        createdAtUtc: string;
        expiresAtUtc?: string;
    }>;
    expiredTopUps?: Array<{
        topupId: string;
        amount: number;
        createdAtUtc: string;
        expiresAtUtc: string;
    }>;
}

export interface WalletTopupResult {
    topupId: string;
    amount: number;
    status: string;
    paymentUrl: string | null;
    createdAtUtc: string;
    expiresAtUtc: string;
    // The current Payment DTO omits this field; the UI uses topupId when paymentUrl is null.
    isReplay?: boolean;
}

/** Status values of GET /wallets/top-up/{id}: Pending, Succeeded, Failed, Expired. */
export interface WalletTopupStatus {
    topupId: string;
    amount: number;
    status: string;
    createdAtUtc: string;
    completedAtUtc: string | null;
    expiresAtUtc: string;
}

export interface WalletTransaction {
    entryId: string;
    createdAtUtc: string;
    entryType: string;
    amount: number;
    balanceDelta: number;
    affectsBalance: boolean;
    balanceAfter: number;
    reason: string | null;
    referenceType: string | null;
    referenceId: string | null;
}

export interface WalletTransactionsPage {
    items: WalletTransaction[];
    page: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
}

export async function getMyWallet(): Promise<WalletBalance> {
    return await gatewayFetch<WalletBalance>("/api/payment/wallets/me");
}

/** The same key for retries of one top-up; a new key for a new top-up (Payment replays the first one). */
export async function createWalletTopup(
    amount: number,
    idempotencyKey: string,
): Promise<WalletTopupResult> {
    return await gatewayFetch<WalletTopupResult>(
        "/api/payment/wallets/top-up",
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Idempotency-Key": idempotencyKey,
            },
            body: JSON.stringify({ amount }),
        },
    );
}

/** topupId is the VNPay transaction reference (vnp_TxnRef). */
export async function getWalletTopup(
    topupId: string,
): Promise<WalletTopupStatus> {
    return await gatewayFetch<WalletTopupStatus>(
        `/api/payment/wallets/top-up/${encodeURIComponent(topupId)}`,
    );
}

export async function getWalletTransactions(
    page = 1,
    pageSize = 20,
): Promise<WalletTransactionsPage> {
    return await gatewayFetch<WalletTransactionsPage>(
        `/api/payment/wallets/transactions?page=${page}&pageSize=${pageSize}`,
    );
}

const WALLET_MESSAGES: Record<string, string> = {
    "payment.idempotency_key_reused":
        "This top-up request was used with a different amount. Start a new top-up.",
    "payment.topup_expired":
        "This top-up request has expired. Start a new top-up.",
    "payment.balance_limit_exceeded":
        "The top-up would exceed your wallet balance limit.",
    "payment.topup_not_found": "This top-up could not be found.",
    "payment.invalid_topup_id": "The top-up reference is invalid.",
    "payment.customer_only": "Only customer accounts can use a wallet.",
};

export function describeWalletError(error: unknown): string {
    if (!(error instanceof ApiError))
        return "Could not connect to the server. Please try again.";
    if (error.status === 401)
        return "Your session has expired. Please sign in again.";
    const code = error.problem?.errorCode;
    return (
        (code && WALLET_MESSAGES[code]) ||
        "Could not process your wallet request. Please try again."
    );
}

export function formatVnd(value: number): string {
    return `${new Intl.NumberFormat("vi-VN").format(value)} ₫`;
}
