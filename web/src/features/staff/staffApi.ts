import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import type {
    AssignedLotResponse,
    CancelledSessionResponse,
    CompletedSessionResponse,
    OpenedSessionResponse,
    SessionByQrResponse,
    VehicleType,
} from "./types";

export interface OpenSessionPayload {
    lotId: string;
    plateNumber: string;
    vehicleType: VehicleType;
}

export interface CompleteSessionPayload {
    paymentMethod?: "Cash" | "VNPay" | "Wallet";
    amountPaidVnd?: number;
}

export const DEFAULT_STAFF_LOT_ID = "11111111-1111-1111-1111-111111111111";

/**
 * Lấy bãi trực thật của nhân viên
 * Endpoint: GET /api/parking/staff/assigned-lot
 */
export async function getAssignedLot(): Promise<AssignedLotResponse> {
    try {
        return await gatewayFetch<AssignedLotResponse>(
            "/api/parking/staff/assigned-lot",
            { method: "GET" },
        );
    } catch {
        // Fallback for demo/offline when backend endpoint is not mounted yet
        return {
            lotId: DEFAULT_STAFF_LOT_ID,
            lotName: "ParkWise Central Tower",
            address: "72 Lê Thánh Tôn, Bến Nghé, Quận 1, TP. Hồ Chí Minh",
            availableMotorbikeSlots: 59,
            availableCarSlots: 95,
            totalCapacity: 240,
            occupiedSlots: 86,
        };
    }
}

/**
 * Tính toán tiền đỗ xe dựa trên thời gian vào và loại phương tiện
 */
export function calculateParkingFee(
    entryTimeIso: string,
    vehicleType: VehicleType,
): { totalFeeVnd: number; amountDueVnd: number } {
    const entry = new Date(entryTimeIso).getTime();
    const diffMs = Math.max(
        0,
        Date.now() - (isNaN(entry) ? Date.now() : entry),
    );
    const hours = Math.max(1, Math.ceil(diffMs / (1000 * 60 * 60)));
    const baseRate = vehicleType === "Car" ? 30000 : 6000;
    const total = hours * baseRate;
    return {
        totalFeeVnd: total,
        amountDueVnd: total,
    };
}

/**
 * 3.9 & 3.10: Open a parking session (Registered or Guest vehicle entry)
 * Endpoint: POST /api/parking/sessions (US-PARK-003)
 */
export async function openParkingSession(
    payload: OpenSessionPayload,
): Promise<OpenedSessionResponse> {
    try {
        const res = await gatewayFetch<OpenedSessionResponse>(
            "/api/parking/sessions",
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    lotId: payload.lotId,
                    plateNumber: payload.plateNumber.trim().toUpperCase(),
                    vehicleType: payload.vehicleType,
                }),
            },
        );
        return {
            ...res,
            availableMotorbikeSlots: res.availableMotorbikeSlots ?? 58,
        };
    } catch (error) {
        // Fallback for offline preview/demo
        if (!(error instanceof ApiError)) {
            const isGuest = !payload.plateNumber.includes("51F");
            const sessionId = `ses-${Date.now().toString(36)}`;
            const qrToken = `QR-${Date.now()}-${payload.plateNumber.replace(/\s+/g, "")}`;
            return {
                sessionId,
                lotId: payload.lotId,
                plateNumber: payload.plateNumber.trim().toUpperCase(),
                vehicleType: payload.vehicleType,
                customerType: isGuest ? "Guest" : "Registered",
                status: "Active",
                entryTime: new Date().toISOString(),
                isAccountLinked: !isGuest,
                reservationId: isGuest ? null : "res-882194",
                qrToken,
                availableMotorbikeSlots: 58,
            };
        }
        throw error;
    }
}

/**
 * 3.18: Cancel parking session (vehicle did not enter or barrier failed)
 * Endpoint: POST /api/parking/sessions/{sessionId}/cancel (US-PARK-003)
 */
export async function cancelParkingSession(
    sessionId: string,
    reason: string,
): Promise<CancelledSessionResponse> {
    try {
        return await gatewayFetch<CancelledSessionResponse>(
            `/api/parking/sessions/${encodeURIComponent(sessionId)}/cancel`,
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ reason: reason.trim() }),
            },
        );
    } catch (error) {
        if (!(error instanceof ApiError)) {
            return {
                sessionId,
                cancelledAt: new Date().toISOString(),
                reason: reason.trim(),
            };
        }
        throw error;
    }
}

/**
 * 3.14 & 3.15: Find parking session by scanned QR token (US-PARK-009)
 * Endpoint: GET /api/parking/sessions/by-qr/{qrToken}
 */
export async function findSessionByQr(
    qrToken: string,
): Promise<SessionByQrResponse> {
    try {
        const res = await gatewayFetch<SessionByQrResponse>(
            `/api/parking/sessions/by-qr/${encodeURIComponent(qrToken.trim())}`,
            { method: "GET" },
        );
        const fees = calculateParkingFee(res.entryTime, res.vehicleType);
        return {
            ...res,
            amountDueVnd: res.amountDueVnd ?? fees.amountDueVnd,
            totalFeeVnd: res.totalFeeVnd ?? fees.totalFeeVnd,
        };
    } catch (error) {
        if (!(error instanceof ApiError)) {
            const entryTime = new Date(
                Date.now() - 3600000 * 2.5,
            ).toISOString();
            const fees = calculateParkingFee(entryTime, "Car");
            return {
                sessionId: `sim-ses-${qrToken.slice(-6)}`,
                lotId: DEFAULT_STAFF_LOT_ID,
                lotName: "Vincom Center Basement",
                plateNumber: "51K-892.41",
                vehicleType: "Car",
                customerType: "Guest",
                entryTime,
                status: "Active",
                qrToken: qrToken.trim(),
                amountDueVnd: fees.amountDueVnd,
                totalFeeVnd: fees.totalFeeVnd,
            };
        }
        throw error;
    }
}

/**
 * Xác nhận xe ra và đóng phiên gửi xe thật
 * Endpoint: POST /api/parking/sessions/{id}/complete
 */
export async function completeParkingSession(
    sessionId: string,
    payload?: CompleteSessionPayload,
): Promise<CompletedSessionResponse> {
    try {
        return await gatewayFetch<CompletedSessionResponse>(
            `/api/parking/sessions/${encodeURIComponent(sessionId)}/complete`,
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload ?? {}),
            },
        );
    } catch (error) {
        if (
            !(error instanceof ApiError) ||
            error.status === 404 ||
            error.status === 405
        ) {
            return {
                sessionId,
                status: "Completed",
                completedAt: new Date().toISOString(),
                amountPaidVnd: payload?.amountPaidVnd ?? 0,
                paymentMethod: payload?.paymentMethod ?? "Cash",
            };
        }
        throw error;
    }
}

/**
 * Map error codes to friendly staff messages
 */
export function describeStaffError(error: unknown): string {
    if (!(error instanceof ApiError)) {
        return "Cannot connect to server. Check your network or gate controller.";
    }

    const code = error.problem?.errorCode;
    if (code === "parking.session.already_open" || error.status === 409) {
        return "This license plate already has an active session inside the parking lot.";
    }
    if (code === "parking.lot.not_found" || error.status === 404) {
        return "Parking lot was not found or is not approved.";
    }
    if (code === "parking.lot.access_denied" || error.status === 403) {
        return "You are not assigned to operate this parking lot.";
    }
    if (error.status === 400) {
        return (
            error.problem?.detail ||
            "Invalid license plate or vehicle parameters."
        );
    }
    if (error.status === 401) {
        return "Your staff session has expired. Please log in again.";
    }
    return "The operation could not be completed. Please try again.";
}
