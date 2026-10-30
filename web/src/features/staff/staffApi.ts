import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import { toStaffCustomerType } from "./types";
import type {
    AssignedLotResponse,
    CancelledSessionResponse,
    CompletedSessionResponse,
    LotParkingSessionPageResponse,
    OpenedSessionResponse,
    OpenSessionByPlateResponse,
    ReissuedQrResponse,
    SessionByQrResponse,
    StaffLotItem,
    VehicleType,
    VerifiedExitResponse,
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

// Seed Lot 2: '10000000-0000-0000-0000-000000000002' ("Bai do Ngoai troi B") - has MOTO-B zone for Motorbike demo
export const DEFAULT_STAFF_LOT_ID = "10000000-0000-0000-0000-000000000002";

/**
 * Lấy danh sách các bãi đỗ mà nhân viên được phân công trực (D04.2 / #129)
 * Endpoint: GET /api/parking/staff/lots
 */
function sanitizeLotName(name: string): string {
    return name
        .replace(/\s*\(Demo[^)]*\)/gi, "")
        .replace(/Ngoài trời\s*/gi, "")
        .trim();
}

export async function getStaffLots(): Promise<StaffLotItem[]> {
    const res = await gatewayFetch<StaffLotItem[]>("/api/parking/staff/lots", {
        method: "GET",
    });
    return res.map((item) => ({
        id: item.id,
        name: sanitizeLotName(item.name),
        address: item.address || "",
        type: item.type,
    }));
}

interface MotorbikeAvailabilityResponse {
    totalCapacity: number;
    available: number;
}

interface LotAvailabilityResponse {
    car: { available: number };
}

// A count Parking cannot give (no motorbike zone, lot not public yet) is unknown: null, never a guess.
async function countOrNull<T>(
    path: string,
    pick: (body: T) => number,
): Promise<number | null> {
    try {
        return pick(await gatewayFetch<T>(path, { method: "GET" }));
    } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null;
        throw error;
    }
}

/**
 * Lấy bãi trực của nhân viên (D04.2) và số chỗ còn do Parking đếm.
 * Endpoints: GET /api/parking/staff/lots, GET /api/parking/lots/{id}/motorbike-availability,
 * GET /api/parking/lots/{id}/availability. Null when the account has no lot.
 */
export async function getAssignedLot(): Promise<AssignedLotResponse | null> {
    const lots = await getStaffLots();
    const chosen = lots.find((l) => l.id === DEFAULT_STAFF_LOT_ID) || lots[0];
    if (!chosen) return null;

    const lotPath = `/api/parking/lots/${encodeURIComponent(chosen.id)}`;
    let motorbikeCapacity: number | null = null;
    const availableMotorbikeSlots =
        await countOrNull<MotorbikeAvailabilityResponse>(
            `${lotPath}/motorbike-availability`,
            (body) => {
                motorbikeCapacity = body.totalCapacity;
                return body.available;
            },
        );
    const availableCarSlots = await countOrNull<LotAvailabilityResponse>(
        `${lotPath}/availability`,
        (body) => body.car.available,
    );

    return {
        lotId: chosen.id,
        lotName: chosen.name,
        address: chosen.address,
        availableMotorbikeSlots,
        motorbikeCapacity,
        availableCarSlots,
    };
}

interface OpenedSessionWire extends Omit<
    OpenedSessionResponse,
    "availableMotorbikeSlots"
> {
    availableMotorbikeSpaces: number | null;
}

/**
 * 3.9 & 3.10: Open a parking session (Registered or Guest vehicle entry)
 * Endpoint: POST /api/parking/sessions (US-PARK-003)
 */
export async function openParkingSession(
    payload: OpenSessionPayload,
): Promise<OpenedSessionResponse> {
    const { availableMotorbikeSpaces, ...res } =
        await gatewayFetch<OpenedSessionWire>("/api/parking/sessions", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                lotId: payload.lotId,
                plateNumber: payload.plateNumber.trim().toUpperCase(),
                vehicleType: payload.vehicleType,
            }),
        });
    return {
        ...res,
        customerType: toStaffCustomerType(res.customerType),
        availableMotorbikeSlots: availableMotorbikeSpaces ?? null,
    };
}

/**
 * 3.18: Cancel parking session (vehicle did not enter or barrier failed)
 * Endpoint: POST /api/parking/sessions/{sessionId}/cancel (US-PARK-003)
 */
export async function cancelParkingSession(
    sessionId: string,
    reason: string,
): Promise<CancelledSessionResponse> {
    return await gatewayFetch<CancelledSessionResponse>(
        `/api/parking/sessions/${encodeURIComponent(sessionId)}/cancel`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ reason: reason.trim() }),
        },
    );
}

/** Fee preview from Payment (DTO, docs/api/Payment-Wallet-Contract.md). */
export interface ParkingSessionFee {
    sessionId: string;
    totalFee: number;
    depositAmount: number;
    amountDue: number;
}

/** Endpoint: GET /api/payment/parking-sessions/{sessionId}/fee */
export async function getParkingSessionFee(
    sessionId: string,
): Promise<ParkingSessionFee> {
    return await gatewayFetch<ParkingSessionFee>(
        `/api/payment/parking-sessions/${encodeURIComponent(sessionId)}/fee`,
        { method: "GET" },
    );
}

/**
 * 3.14 & 3.15: Find parking session by scanned QR token (US-PARK-009)
 * Endpoint: GET /api/parking/sessions/by-qr/{qrToken}
 */
export async function findSessionByQr(
    qrToken: string,
): Promise<SessionByQrResponse> {
    const res = await gatewayFetch<SessionByQrResponse>(
        `/api/parking/sessions/by-qr/${encodeURIComponent(qrToken.trim())}`,
        { method: "GET" },
    );
    // The fee is Payment's; when it cannot be read the screen says so instead of guessing.
    const fee = await getParkingSessionFee(res.sessionId).catch(() => null);
    return {
        ...res,
        customerType: toStaffCustomerType(res.customerType),
        qrToken: qrToken.trim(),
        totalFeeVnd: fee?.totalFee,
        amountDueVnd: fee?.amountDue,
    };
}

/**
 * Xác nhận xe ra và đóng phiên gửi xe thật
 * Endpoint: POST /api/parking/sessions/{id}/complete (not in Parking yet: exit payment is Sprint 3)
 */
export async function completeParkingSession(
    sessionId: string,
    payload?: CompleteSessionPayload,
): Promise<CompletedSessionResponse> {
    return await gatewayFetch<CompletedSessionResponse>(
        `/api/parking/sessions/${encodeURIComponent(sessionId)}/complete`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload ?? {}),
        },
    );
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

/**
 * US-STAFF-004: Tìm phiên đỗ mở bằng biển số khi khách làm mất vé / QR (BR-11.d)
 * Endpoint: GET /api/parking/sessions/open?lotId={lotId}&plateNumber={plateNumber}
 */
export async function findOpenSessionByPlate(
    lotId: string,
    plateNumber: string,
): Promise<OpenSessionByPlateResponse> {
    const qs = new URLSearchParams({
        lotId,
        plateNumber: plateNumber.trim().toUpperCase(),
    });
    return await gatewayFetch<OpenSessionByPlateResponse>(
        `/api/parking/sessions/open?${qs.toString()}`,
        { method: "GET" },
    );
}

/**
 * US-STAFF-004: Cấp lại mã QR mới khi khách làm mất thẻ / vé
 * Endpoint: POST /api/parking/sessions/{id}/reissue-qr
 */
export async function reissueParkingSessionQr(
    sessionId: string,
    reason: string,
): Promise<ReissuedQrResponse> {
    return await gatewayFetch<ReissuedQrResponse>(
        `/api/parking/sessions/${encodeURIComponent(sessionId)}/reissue-qr`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ reason: reason.trim() }),
        },
    );
}

/**
 * US-STAFF-004: Nhân viên xác minh xe ra thủ công tại quầy / cổng (BR-11.c)
 * Endpoint: POST /api/parking/sessions/{id}/manual-verify
 */
export async function manualVerifyExit(
    sessionId: string,
    reason: string,
): Promise<VerifiedExitResponse> {
    return await gatewayFetch<VerifiedExitResponse>(
        `/api/parking/sessions/${encodeURIComponent(sessionId)}/manual-verify`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ reason: reason.trim() }),
        },
    );
}

/**
 * Lấy danh sách các phiên đỗ xe theo bãi (#129)
 * Endpoint: GET /api/parking/staff/lots/{lotId}/sessions
 */
export async function listLotSessions(
    lotId: string,
    status?: string,
    page: number = 1,
    pageSize: number = 20,
): Promise<LotParkingSessionPageResponse> {
    const query = new URLSearchParams({
        page: String(page),
        pageSize: String(pageSize),
    });
    if (status) query.set("status", status);

    return await gatewayFetch<LotParkingSessionPageResponse>(
        `/api/parking/staff/lots/${encodeURIComponent(lotId)}/sessions?${query.toString()}`,
        { method: "GET" },
    );
}
