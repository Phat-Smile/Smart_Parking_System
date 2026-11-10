import { getGatewayBaseUrl } from "@/shared/config/env";
import { getAccessToken } from "@/shared/api/access-token";

export interface ParkingFeeReceipt {
    receiptId: string;
    sessionId: string;
    paymentId: string;
    parkingLotId: string;
    parkingLotName: string;
    licensePlate: string;
    vehicleType: string;
    checkInTimeUtc: string;
    checkOutTimeUtc: string;
    totalDurationHours: number;
    standardFee: number;
    surgeMultiplier: number;
    prepaidDepositOffset: number;
    netPaidAmount: number;
    paymentMethod: string;
    paidAtUtc: string;
    ledgerTransactionReference?: string | null;
}

export async function getParkingFeeReceiptApi(
    sessionId: string,
    signal?: AbortSignal,
): Promise<ParkingFeeReceipt> {
    const token = getAccessToken();
    const headers: Record<string, string> = {
        "Content-Type": "application/json",
    };
    if (token) {
        headers["Authorization"] = `Bearer ${token}`;
    }

    const response = await fetch(
        `${getGatewayBaseUrl()}/api/payment/parking-sessions/${encodeURIComponent(sessionId)}/receipt`,
        {
            method: "GET",
            headers,
            signal,
            credentials: "include",
        },
    );

    if (!response.ok) {
        throw new Error(`Failed to fetch receipt for session ${sessionId}`);
    }

    return (await response.json()) as ParkingFeeReceipt;
}
