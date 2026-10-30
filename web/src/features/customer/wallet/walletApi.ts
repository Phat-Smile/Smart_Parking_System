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
    }>;
}

export interface WalletTopupResult {
    topupId: string;
    amount: number;
    status: string;
    paymentUrl: string | null;
    createdAtUtc: string;
    expiresAtUtc: string;
    isReplay: boolean;
}

/** Status values of GET /wallets/top-up/{id}: Pending, Succeeded, Failed (Expired once Payment reports it). */
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
        "Yêu cầu nạp này đã được dùng với số tiền khác. Hãy tạo yêu cầu nạp mới.",
    "payment.topup_expired": "Yêu cầu nạp đã hết hạn. Hãy tạo yêu cầu nạp mới.",
    "payment.balance_limit_exceeded":
        "Số tiền nạp vượt hạn mức số dư tối đa của ví.",
    "payment.topup_not_found": "Không tìm thấy giao dịch nạp tiền này.",
    "payment.invalid_topup_id": "Mã giao dịch nạp tiền không hợp lệ.",
    "payment.customer_only": "Chỉ tài khoản khách hàng mới có ví.",
    "auth.forbidden": "Chỉ tài khoản khách hàng mới có ví.",
};

export function describeWalletError(error: unknown): string {
    if (!(error instanceof ApiError))
        return "Không kết nối được máy chủ. Vui lòng thử lại.";
    if (error.status === 401)
        return "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại.";
    if (error.status === 403)
        return "Chỉ tài khoản khách hàng mới có ví.";
    const code = error.problem?.errorCode;
    return (
        (code && WALLET_MESSAGES[code]) ||
        "Không xử lý được yêu cầu ví. Vui lòng thử lại."
    );
}

export function formatVnd(value: number): string {
    return `${new Intl.NumberFormat("vi-VN").format(value)} ₫`;
}
