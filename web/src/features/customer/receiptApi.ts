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

export interface RawBackendReceipt {
    receiptId?: string;
    sessionId?: string;
    paymentId?: string;
    parkingLotId?: string;
    parkingLotName?: string;
    licensePlate?: string;
    plateNumber?: string;
    vehicleType?: string;
    checkInTimeUtc?: string;
    checkOutTimeUtc?: string;
    entryTimeUtc?: string;
    paymentTimeUtc?: string;
    exitWindowExpiresUtc?: string;
    totalDurationHours?: number;
    durationMinutes?: number;
    standardFee?: number;
    totalFee?: number;
    surgeMultiplier?: number;
    prepaidDepositOffset?: number;
    prepaidAmount?: number;
    netPaidAmount?: number;
    amountPaid?: number;
    paymentMethod?: string;
    paidAtUtc?: string;
    status?: string;
    ledgerTransactionReference?: string | null;
    transactionReference?: string | null;
}

export function adaptBackendReceipt(raw: RawBackendReceipt): ParkingFeeReceipt {
    const paymentId = raw.paymentId || raw.receiptId || "";
    const rawMinutes =
        typeof raw.durationMinutes === "number" ? raw.durationMinutes : null;
    const durationHours =
        rawMinutes !== null
            ? Math.round((rawMinutes / 60) * 10) / 10
            : typeof raw.totalDurationHours === "number"
              ? raw.totalDurationHours
              : 0;

    const standardFee =
        typeof raw.totalFee === "number"
            ? raw.totalFee
            : typeof raw.standardFee === "number"
              ? raw.standardFee
              : 0;
    const prepaidOffset =
        typeof raw.prepaidAmount === "number"
            ? raw.prepaidAmount
            : typeof raw.prepaidDepositOffset === "number"
              ? raw.prepaidDepositOffset
              : 0;
    const netPaid =
        typeof raw.amountPaid === "number"
            ? raw.amountPaid
            : typeof raw.netPaidAmount === "number"
              ? raw.netPaidAmount
              : standardFee - prepaidOffset;

    return {
        receiptId: raw.receiptId || paymentId,
        sessionId: raw.sessionId || "",
        paymentId: paymentId,
        parkingLotId: raw.parkingLotId || "",
        parkingLotName: raw.parkingLotName || "Bãi đỗ xe Smart Parking",
        licensePlate: raw.licensePlate || raw.plateNumber || "",
        vehicleType: raw.vehicleType || "Car",
        checkInTimeUtc:
            raw.checkInTimeUtc ||
            raw.entryTimeUtc ||
            raw.paymentTimeUtc ||
            new Date().toISOString(),
        checkOutTimeUtc:
            raw.checkOutTimeUtc ||
            raw.paymentTimeUtc ||
            new Date().toISOString(),
        totalDurationHours: durationHours,
        standardFee: standardFee,
        surgeMultiplier:
            typeof raw.surgeMultiplier === "number" ? raw.surgeMultiplier : 1.0,
        prepaidDepositOffset: prepaidOffset,
        netPaidAmount: netPaid,
        paymentMethod: raw.paymentMethod || "VNPAY",
        paidAtUtc:
            raw.paidAtUtc || raw.paymentTimeUtc || new Date().toISOString(),
        ledgerTransactionReference:
            raw.ledgerTransactionReference || raw.transactionReference || null,
    };
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

    const raw = (await response.json()) as RawBackendReceipt;
    return adaptBackendReceipt(raw);
}
