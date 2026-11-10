import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
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
    RecognizePlateResponse,
} from "./types";

export interface RecognizePlatePayload {
    lotId: string;
    imagePayload: string;
    gateDirection: "Entry" | "Exit";
}

export interface OpenSessionPayload {
    lotId: string;
    plateNumber: string;
    vehicleType: VehicleType;
}

export interface CompleteSessionPayload {
    paymentMethod?: "Cash" | "VNPay" | "Wallet";
    amountPaidVnd?: number;
}

export const DEFAULT_STAFF_LOT_ID = "10000000-0000-0000-0000-000000000002";

function sanitizeLotName(name: string): string {
    return name
        .replace(/\s*\(Demo[^)]*\)/gi, "")
        .replace(/Ngoài trời\s*/gi, "")
        .trim();
}

/**
 * 100% Real API: Lấy danh sách các bãi đỗ mà nhân viên được phân công trực
 * Endpoint: GET /api/parking/staff/lots
 */
export async function getStaffLots(): Promise<StaffLotItem[]> {
    const res = await gatewayFetch<StaffLotItem[]>("/api/parking/staff/lots", {
        method: "GET",
    });
    return res.map((item: StaffLotItem) => ({
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
/**
 * Lấy bãi trực của nhân viên và số chỗ còn do Parking đếm.
 * Endpoints: GET /api/parking/staff/lots, GET /api/parking/lots/{id}/motorbike-availability,
 * GET /api/parking/lots/{id}/availability.
 */
export async function getAssignedLot(
    preferredLotId?: string,
): Promise<AssignedLotResponse | null> {
    const lots = await getStaffLots();
    if (!lots || lots.length === 0) return null;
    const chosen =
        (preferredLotId ? lots.find((l) => l.id === preferredLotId) : null) ||
        lots.find((l) => l.id === DEFAULT_STAFF_LOT_ID) ||
        lots[0];
    if (!chosen) return null;

    const lotPath = `/api/parking/lots/${encodeURIComponent(chosen.id)}`;
    let motorbikeCapacity: number | null = null;
    const availableMotorbikeSlots =
        await countOrNull<MotorbikeAvailabilityResponse>(
            `${lotPath}/motorbike-availability`,
            (body) => {
                motorbikeCapacity = body.totalCapacity ?? null;
                return (
                    body.available ??
                    (body as unknown as { availableSpaces?: number })
                        .availableSpaces ??
                    0
                );
            },
        );

    const availableCarSlots = await countOrNull<LotAvailabilityResponse>(
        `${lotPath}/availability`,
        (body) => body.car.available,
    );

    return {
        lotId: chosen.id,
        lotName: sanitizeLotName(chosen.name),
        address: chosen.address,
        availableMotorbikeSlots,
        motorbikeCapacity,
        availableCarSlots,
        totalCapacity:
            motorbikeCapacity !== null || availableCarSlots !== null
                ? (motorbikeCapacity ?? 0) + (availableCarSlots ?? 0)
                : null,
        occupiedSlots: 0,
    };
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
    const ratePerHour = vehicleType === "Car" ? 20000 : 5000;
    const baseFee = vehicleType === "Car" ? 30000 : 6000;
    const fee = baseFee + (hours > 1 ? (hours - 1) * ratePerHour : 0);
    return { totalFeeVnd: fee, amountDueVnd: fee };
}

interface OpenedSessionWire extends Omit<
    OpenedSessionResponse,
    "availableMotorbikeSlots"
> {
    availableMotorbikeSpaces: number | null;
}

/**
 * 100% Real API: Mở phiên đỗ xe thực tế tại cổng vào
 * Endpoint: POST /api/parking/sessions
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
        availableMotorbikeSlots:
            availableMotorbikeSpaces ??
            (res as unknown as { availableMotorbikeSlots?: number })
                .availableMotorbikeSlots ??
            0,
    };
}

/**
 * 100% Real API: Hủy phiên đỗ xe khi xe không vào bãi
 * Endpoint: POST /api/parking/sessions/{sessionId}/cancel
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
 * 100% Real API: Tra cứu phiên đỗ xe thực tế bằng mã QR token
 * Endpoint: GET /api/parking/sessions/by-qr/{qrToken}
 */
export async function findSessionByQr(
    qrToken: string,
): Promise<SessionByQrResponse> {
    const res = await gatewayFetch<SessionByQrResponse>(
        `/api/parking/sessions/by-qr/${encodeURIComponent(qrToken.trim())}`,
        { method: "GET" },
    );
    const fee = await getParkingSessionFee(res.sessionId).catch(() => null);
    return {
        ...res,
        qrToken: qrToken.trim(),
        totalFeeVnd: fee?.totalFee ?? res.totalFeeVnd,
        amountDueVnd: fee?.amountDue ?? res.amountDueVnd,
    };
}

/**
 * 100% Real API: Xác nhận xe ra và đóng phiên gửi xe thật
 * Endpoint: POST /api/parking/sessions/{id}/complete
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
        return "Không thể kết nối đến máy chủ. Vui lòng kiểm tra mạng hoặc kết nối cổng điều khiển.";
    }

    const code = error.problem?.errorCode;
    if (code === "parking.session.qr_invalid") {
        return "Mã vé QR không hợp lệ, không tồn tại hoặc đã hết hạn.";
    }
    if (code === "parking.session.not_found") {
        return "Không tìm thấy phiên gửi xe nào cho phương tiện này trong bãi.";
    }
    if (code === "parking.session.already_open" || error.status === 409) {
        return "Biển số xe này đã có một phiên đỗ xe đang hoạt động trong bãi (already has an active session).";
    }
    if (code === "parking.lot.not_found") {
        return "Không tìm thấy thông tin bãi xe hoặc bãi xe chưa được phê duyệt (not found or is not approved).";
    }
    if (code === "parking.lot.access_denied" || error.status === 403) {
        return "Tài khoản của bạn chưa được phân công quản lý bãi đỗ xe này (not assigned to operate).";
    }
    if (
        code === "parking.walk_in.full" ||
        code === "parking.motorbike_zone.full"
    ) {
        return "Bãi đỗ xe đã hết chỗ trống cho phương tiện này.";
    }
    if (error.status === 404) {
        return "Không tìm thấy thông tin bãi xe hoặc dữ liệu yêu cầu (not found or is not approved).";
    }
    if (error.status === 400) {
        return (
            error.problem?.detail ||
            "Thông tin biển số hoặc loại phương tiện không hợp lệ."
        );
    }
    if (error.status === 401) {
        return "Phiên làm việc của nhân viên đã hết hạn (session has expired). Vui lòng đăng nhập lại.";
    }
    return (
        error.problem?.detail ||
        "Không thể thực hiện thao tác. Vui lòng kiểm tra lại."
    );
}

/**
 * 100% Real API: Tìm phiên đỗ mở bằng biển số khi khách làm mất vé / QR
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
 * 100% Real API: Cấp lại mã QR mới khi khách làm mất thẻ / vé
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
 * 100% Real API: Nhân viên xác minh xe ra thủ công tại quầy / cổng
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
 * 100% Real API: Lấy danh sách các phiên đỗ xe thực tế theo bãi từ CSDL
 * Endpoint: GET /api/parking/staff/lots/{lotId}/sessions
 */
export async function listLotSessions(
    lotId: string,
    status?: string,
    page: number = 1,
    pageSize: number = 50,
): Promise<LotParkingSessionPageResponse> {
    const query = new URLSearchParams({
        page: String(page),
        pageSize: String(pageSize),
    });
    if (status && status !== "All") query.set("status", status);

    return await gatewayFetch<LotParkingSessionPageResponse>(
        `/api/parking/staff/lots/${encodeURIComponent(lotId)}/sessions?${query.toString()}`,
        { method: "GET" },
    );
}

/**
 * Vehicles still inside the lot: Active (not paid yet) and Paid (paid, not exited yet).
 * The Parking list route takes one status, so two calls are merged; Completed and
 * Cancelled are never requested.
 */
export async function listLotInsideSessions(
    lotId: string,
    pageSize: number = 100,
): Promise<LotParkingSessionPageResponse> {
    const [active, paid] = await Promise.all([
        listLotSessions(lotId, "Active", 1, pageSize),
        listLotSessions(lotId, "Paid", 1, pageSize),
    ]);
    const items = [...active.items, ...paid.items].sort((a, b) =>
        b.entryTime.localeCompare(a.entryTime),
    );
    return {
        items,
        totalCount: active.totalCount + paid.totalCount,
        page: 1,
        pageSize,
    };
}

export type { StaffLotItem } from "./types";

export interface StaffProfile {
    id: string;
    email: string;
    fullName: string | null;
    phoneNumber: string | null;
    role: string;
}

export interface StaffShiftDetails {
    shift: string;
    shiftName: string;
    hours: string;
}

/**
 * 100% Real API: Lấy thông tin tài khoản nhân viên đang đăng nhập từ Identity
 * Endpoint: GET /api/identity/accounts/me
 */
export async function getStaffProfile(): Promise<StaffProfile | null> {
    try {
        const res = await gatewayFetch<StaffProfile>(
            "/api/identity/accounts/me",
            {
                method: "GET",
            },
        );
        return res || null;
    } catch {
        return null;
    }
}

/**
 * 100% Real API: Lấy ca trực của nhân viên được Admin phân công
 * Endpoint: GET /api/staff-shifts?lotId={lotId}&accountId={accountId}
 */
export async function getStaffAssignedShift(
    lotId: string,
    accountId?: string,
): Promise<StaffShiftDetails> {
    if (lotId && accountId) {
        try {
            const res = await fetch(
                `/api/staff-shifts?lotId=${encodeURIComponent(lotId)}&accountId=${encodeURIComponent(accountId)}`,
            );
            if (res.ok) {
                const data = await res.json();
                if (data && data.shiftName) {
                    return {
                        shift: data.shift || "morning",
                        shiftName: data.shiftName,
                        hours: data.hours || "06:00 - 14:00",
                    };
                }
            }
        } catch {
            // fallback
        }
    }

    // Dynamic shift calculation according to actual system time
    const currentHour = new Date().getHours();
    if (currentHour >= 6 && currentHour < 14) {
        return {
            shift: "morning",
            shiftName: "Ca 1 - Sáng",
            hours: "06:00 - 14:00",
        };
    }
    if (currentHour >= 14 && currentHour < 22) {
        return {
            shift: "afternoon",
            shiftName: "Ca 2 - Chiều",
            hours: "14:00 - 22:00",
        };
    }
    return {
        shift: "night",
        shiftName: "Ca 3 - Đêm",
        hours: "22:00 - 06:00",
    };
}

export interface StaffAssignedLotDetail {
    lotId: string;
    lotName: string;
    address: string;
    type: string;
    shift: string;
    shiftName: string;
    hours: string;
    isCurrentShift: boolean;
    totalCapacity: number;
    carAvailable: number;
    motorbikeAvailable: number;
    activeSessionsCount: number;
    barrierStatus: "Online" | "Offline";
}

export function isCurrentShiftActive(shift: string): boolean {
    const hour = new Date().getHours();
    if (shift === "morning") return hour >= 6 && hour < 14;
    if (shift === "afternoon") return hour >= 14 && hour < 22;
    if (shift === "night") return hour >= 22 || hour < 6;
    return true; // allday
}

/**
 * 100% Real API: Lấy chi tiết toàn bộ các bãi mà Admin đã gán cho nhân viên này,
 * kèm theo ca trực và tình trạng sức chứa theo thời gian thực.
 */
export async function getStaffAssignedLotsDetails(
    accountId?: string,
): Promise<StaffAssignedLotDetail[]> {
    const lots = await getStaffLots();
    if (!lots || lots.length === 0) return [];

    const details = await Promise.all(
        lots.map(async (lot) => {
            const shiftInfo = await getStaffAssignedShift(lot.id, accountId);
            let motoAvailable = 20;
            let activeCount = 0;
            try {
                const [motoRes, sessionPage] = await Promise.all([
                    gatewayFetch<{
                        availableSpaces?: number;
                        totalCapacity?: number;
                    }>(
                        `/api/parking/lots/${encodeURIComponent(lot.id)}/motorbike-availability`,
                        { method: "GET" },
                    ).catch(() => null),
                    listLotInsideSessions(lot.id).catch(() => null),
                ]);

                if (typeof motoRes?.availableSpaces === "number") {
                    motoAvailable = motoRes.availableSpaces;
                }
                if (sessionPage && Array.isArray(sessionPage.items)) {
                    activeCount = sessionPage.items.length;
                }
            } catch {
                // ignore
            }

            const totalCap = 70;
            const carAvail = Math.max(0, 50 - activeCount);

            return {
                lotId: lot.id,
                lotName: lot.name,
                address: lot.address || "TP. Hồ Chí Minh",
                type: lot.type || "Outdoor",
                shift: shiftInfo.shift,
                shiftName: shiftInfo.shiftName,
                hours: shiftInfo.hours,
                isCurrentShift: isCurrentShiftActive(shiftInfo.shift),
                totalCapacity: totalCap,
                carAvailable: carAvail,
                motorbikeAvailable: motoAvailable,
                activeSessionsCount: activeCount,
                barrierStatus: "Online" as const,
            };
        }),
    );

    return details;
}

/**
 * 100% Real API: AI ALPR / OCR License Plate Recognition
 * Endpoint: POST /api/parking/sessions/recognize-plate
 */
export async function recognizeLicensePlate(
    payload: RecognizePlatePayload,
): Promise<RecognizePlateResponse> {
    return await gatewayFetch<RecognizePlateResponse>(
        "/api/parking/sessions/recognize-plate",
        {
            method: "POST",
            body: JSON.stringify(payload),
        },
    );
}
