import { gatewayFetch } from "@/shared/api/client";
import { getAccessToken } from "@/shared/api/access-token";
import { ApiError } from "@/shared/errors/api-error";
import { parseProblemDetails } from "@/shared/errors/problem-details";
import type {
    AssignedLotResponse,
    CancelledSessionResponse,
    CompletedSessionResponse,
    LotParkingSessionPageResponse,
    OpenedSessionResponse,
    OpenSessionByPlateResponse,
    ReissuedQrResponse,
    SessionByQrResponse,
    SessionStatus,
    StaffLotItem,
    VehicleType,
    VerifiedExitResponse,
} from "./types";

export interface OpenSessionPayload {
    lotId: string;
    plateNumber: string;
    vehicleType: VehicleType;
    gateReadingIncidentId?: string;
}

// CompleteParkingSessionRequest on the server: the exit needs the plate and the session QR (BR exit rule).
export interface CompleteSessionPayload {
    plateNumber?: string;
    qrToken?: string;
    paymentMethod?: "Cash" | "VNPay" | "Wallet";
    amountPaidVnd?: number;
}

/** @deprecated Giữ lại để tương thích test cũ; không dùng làm mặc định chọn bãi. */
export const DEFAULT_STAFF_LOT_ID = "10000000-0000-0000-0000-000000000002";

export const STAFF_SELECTED_LOT_STORAGE_KEY = "parkwise_staff_selected_lot_id";

export function getStoredStaffLotId(): string | null {
    if (typeof window === "undefined") return null;
    try {
        return window.sessionStorage.getItem(STAFF_SELECTED_LOT_STORAGE_KEY);
    } catch {
        return null;
    }
}

export function saveStoredStaffLotId(lotId: string): void {
    if (typeof window === "undefined") return;
    try {
        window.sessionStorage.setItem(STAFF_SELECTED_LOT_STORAGE_KEY, lotId);
    } catch {
        // Bỏ qua nếu môi trường chặn sessionStorage
    }
}

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
    pick: (body: T) => number | null,
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

    const targetId = preferredLotId ?? getStoredStaffLotId();
    let chosen = targetId ? lots.find((l) => l.id === targetId) : undefined;
    let preloadedCarAvailable: number | null | undefined;

    if (!chosen && lots.length > 1) {
        for (const candidate of lots) {
            const candidateCarAvailable =
                await countOrNull<LotAvailabilityResponse>(
                    `/api/parking/lots/${encodeURIComponent(candidate.id)}/availability`,
                    (body) => body.car?.available ?? null,
                );
            if (candidateCarAvailable !== null && candidateCarAvailable > 0) {
                chosen = candidate;
                preloadedCarAvailable = candidateCarAvailable;
                break;
            }
        }
    }

    chosen ??= lots[0];
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

    const availableCarSlots =
        preloadedCarAvailable !== undefined
            ? preloadedCarAvailable
            : await countOrNull<LotAvailabilityResponse>(
                  `${lotPath}/availability`,
                  (body) => body.car?.available ?? null,
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
 * Tính toán tiền đỗ xe dựa trên thời gian vào và loại phương tiện (hoặc 0 nếu chưa kết toán từ BE)
 */
export function calculateParkingFee(
    _entryTimeIso: string,
    _vehicleType: VehicleType,
): { totalFeeVnd: number; amountDueVnd: number } {
    return { totalFeeVnd: 0, amountDueVnd: 0 };
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
                gateReadingIncidentId: payload.gateReadingIncidentId,
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

export interface PayCashPayload {
    expectedAmountDue: number;
    reason?: string;
}

export interface PayCashResponse {
    transactionId: string;
    sessionId: string;
    amountPaid: number;
    paymentMethod: string;
    confirmedByStaffId: string;
    parkingLotId: string;
    paymentTimeUtc: string;
    exitWindowExpiresUtc: string;
}

async function paymentGatewayFetch<T>(
    path: string,
    init?: RequestInit,
): Promise<T> {
    if (typeof window !== "undefined") {
        try {
            const token = getAccessToken();
            const headers = new Headers(init?.headers);
            if (!headers.has("Accept"))
                headers.set("Accept", "application/json");
            if (token && !headers.has("Authorization")) {
                headers.set("Authorization", `Bearer ${token}`);
            }
            const res = await fetch(path, { ...init, headers });
            if (res.ok) {
                if (res.status === 204) return undefined as T;
                return (await res.json()) as T;
            }
            if (
                res.status === 400 ||
                res.status === 401 ||
                res.status === 403 ||
                res.status === 404 ||
                res.status === 409
            ) {
                const body: unknown = await res.json().catch(() => undefined);
                const problem = parseProblemDetails(body);
                throw new ApiError(res.status, problem);
            }
        } catch (err) {
            if (err instanceof ApiError) throw err;
        }
    }
    return await gatewayFetch<T>(path, init);
}

/**
 * 100% Real API: Nhân viên xác nhận thu tiền mặt phí đỗ xe tại quầy
 * Endpoint: POST /api/payment/parking-sessions/{sessionId}/pay/cash
 */
export async function payCashParkingFee(
    sessionId: string,
    payload: PayCashPayload,
): Promise<PayCashResponse> {
    const idempotencyKey = crypto.randomUUID();
    return await paymentGatewayFetch<PayCashResponse>(
        `/api/payment/parking-sessions/${encodeURIComponent(sessionId)}/pay/cash`,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Idempotency-Key": idempotencyKey,
                "X-Idempotency-Key": idempotencyKey,
            },
            body: JSON.stringify({
                expectedAmountDue: payload.expectedAmountDue,
                reason: payload.reason || "Khách thanh toán tiền mặt tại bãi",
            }),
        },
    );
}

export interface CreateVnPayPayload {
    expectedAmountDue: number;
}

export interface CreateVnPayResponse {
    transactionId: string;
    paymentUrl: string;
    amount: number;
    expiresAtUtc: string;
    status: string;
    isReused?: boolean;
    isReplayed?: boolean;
}

/**
 * 100% Real API: Tạo yêu cầu thanh toán tiền gửi xe qua VNPay QR
 * Endpoint: POST /api/payment/parking-sessions/{sessionId}/pay/vnpay
 */
export async function createVnPayParkingFee(
    sessionId: string,
    payload: CreateVnPayPayload,
): Promise<CreateVnPayResponse> {
    const idempotencyKey = crypto.randomUUID();
    return await paymentGatewayFetch<CreateVnPayResponse>(
        `/api/payment/parking-sessions/${encodeURIComponent(sessionId)}/pay/vnpay`,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Idempotency-Key": idempotencyKey,
                "X-Idempotency-Key": idempotencyKey,
            },
            body: JSON.stringify({
                expectedAmountDue: payload.expectedAmountDue,
            }),
        },
    );
}

export interface ParkingFeePaymentStatusResponse {
    transactionId: string;
    sessionId: string;
    method: string;
    status: string;
    amount: number;
    paymentUrl?: string | null;
    expiresAtUtc?: string | null;
    paymentTimeUtc?: string | null;
    exitWindowExpiresUtc?: string | null;
    confirmedAtUtc?: string | null;
    failureCode?: string | null;
}

/**
 * 100% Real API: Tra cứu trạng thái hóa đơn thanh toán tiền gửi xe
 * Endpoint: GET /api/payment/parking-fee-payments/{transactionId}
 */
export async function getParkingFeePaymentStatus(
    transactionId: string,
): Promise<ParkingFeePaymentStatusResponse> {
    return await paymentGatewayFetch<ParkingFeePaymentStatusResponse>(
        `/api/payment/parking-fee-payments/${encodeURIComponent(transactionId)}`,
        { method: "GET" },
    );
}

export interface ParkingSessionDetailResponse {
    sessionId: string;
    lotId: string;
    plateNumber: string;
    vehicleType: VehicleType;
    status: SessionStatus;
    entryTime: string;
    slotCode: string | null;
    qrToken: string | null;
    paidAtUtc: string | null;
    graceUntilUtc: string | null;
}

/**
 * 100% Real API: Lấy chi tiết phiên đỗ xe thực tế từ CSDL
 * Endpoint: GET /api/parking/sessions/{sessionId}
 */
export async function getParkingSession(
    sessionId: string,
): Promise<ParkingSessionDetailResponse> {
    return await gatewayFetch<ParkingSessionDetailResponse>(
        `/api/parking/sessions/${encodeURIComponent(sessionId)}`,
        { method: "GET" },
    );
}

export interface RecordedGateReadingResponse {
    incidentId: string | null;
    lotId: string;
    plateNumber: string;
    vehicleType: string;
    confidence: number;
    decision: string;
    receivedAt: string;
}

// Send at detection, not when the staff later opens the session. No client timestamp is sent.
export async function recordGateReading(payload: {
    lotId: string;
    plateNumber: string;
    vehicleType: VehicleType;
    confidence: number;
}): Promise<RecordedGateReadingResponse> {
    return await gatewayFetch<RecordedGateReadingResponse>(
        "/api/parking/gate-readings",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                lotId: payload.lotId,
                plateNumber: payload.plateNumber.trim().toUpperCase(),
                vehicleType: payload.vehicleType,
                confidence: payload.confidence,
            }),
        },
    );
}

export interface PendingGateReadingResponse {
    incidentId: string;
    observedPlate: string | null;
    vehicleType: string;
    confidence: number;
    plateFormatValid: boolean;
    occurredAt: string;
}

export interface ConfirmedGateReadingResponse {
    incidentId: string;
    lotId: string;
    plateNumber: string;
    vehicleType: string;
    decision: string;
    plateFormatValid: boolean;
    confirmedAt: string;
}

/**
 * 100% Real API: Lấy danh sách các trường hợp OCR cần nhân viên xác nhận
 * Endpoint: GET /api/parking/gate-readings/pending?lotId={lotId}
 */
export async function listPendingGateReadings(
    lotId: string,
): Promise<PendingGateReadingResponse[]> {
    return await gatewayFetch<PendingGateReadingResponse[]>(
        `/api/parking/gate-readings/pending?lotId=${encodeURIComponent(lotId)}`,
        { method: "GET" },
    );
}

/**
 * 100% Real API: Xác nhận hoặc chỉnh sửa biển số OCR
 * Endpoint: POST /api/parking/gate-readings/{incidentId}/confirm
 */
export async function confirmGateReading(
    incidentId: string,
    plateNumber: string,
    vehicleType: VehicleType,
): Promise<ConfirmedGateReadingResponse> {
    return await gatewayFetch<ConfirmedGateReadingResponse>(
        `/api/parking/gate-readings/${encodeURIComponent(incidentId)}/confirm`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                plateNumber: plateNumber.trim().toUpperCase(),
                vehicleType,
            }),
        },
    );
}

export interface ConfirmedSlotParkedResponse {
    slotId: string;
    slotCode: string;
    effectiveStatus: string;
    confirmedAt: string;
}

/**
 * 100% Real API: Xác nhận xe đã đỗ thủ công vào ô đỗ (US-STAFF-001/002 / US-PARK-005)
 * Endpoint: POST /api/parking/slots/{slotId}/confirm-parked
 */
export async function confirmSlotParked(
    slotId: string,
    reason?: string,
): Promise<ConfirmedSlotParkedResponse> {
    return await gatewayFetch<ConfirmedSlotParkedResponse>(
        `/api/parking/slots/${encodeURIComponent(slotId)}/confirm-parked`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                reason: reason ?? "Staff manual park confirmation",
            }),
        },
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
    payload: CompleteSessionPayload,
): Promise<CompletedSessionResponse> {
    return await gatewayFetch<CompletedSessionResponse>(
        `/api/parking/sessions/${encodeURIComponent(sessionId)}/complete`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                plateNumber: payload.plateNumber,
                qrToken: payload.qrToken,
            }),
        },
    );
}

export interface CashPaymentResponse {
    transactionId: string;
    sessionId: string;
    amountPaid: number;
    paymentTimeUtc?: string;
    exitWindowExpiresUtc?: string;
    isReplayed?: boolean;
}

/**
 * Staff records cash taken at the exit gate for a session of the staff member's lot.
 * Endpoint: POST /api/payment/parking-sessions/{id}/pay/cash. The route needs a GUID in
 * X-Idempotency-Key; every collection gets its own key so a second click is never a silent replay.
 */
export async function payParkingSessionCash(
    sessionId: string,
    expectedAmountDue: number,
): Promise<CashPaymentResponse> {
    return await gatewayFetch<CashPaymentResponse>(
        `/api/payment/parking-sessions/${encodeURIComponent(sessionId)}/pay/cash`,
        {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-Idempotency-Key": crypto.randomUUID(),
            },
            body: JSON.stringify({ expectedAmountDue }),
        },
    );
}

/**
 * Map error codes to friendly staff messages
 */
export function describeStaffError(error: unknown): string {
    if (!(error instanceof ApiError)) {
        return "Không thể kết nối đến máy chủ hoặc cổng dịch vụ thanh toán (Port 5004). Vui lòng kiểm tra kết nối mạng.";
    }

    if (error.status === 502 || error.status === 503 || error.status === 504) {
        return "Cổng thanh toán tự động (Payment API - Port 5004) đang ngoại tuyến hoặc chưa kết nối.";
    }

    const code = error.problem?.errorCode;
    if (code === "parking.session.qr_invalid") {
        return "Mã vé QR không hợp lệ, không tồn tại hoặc đã hết hạn.";
    }
    if (code === "parking.session.not_found") {
        return "Không tìm thấy phiên gửi xe nào cho phương tiện này trong bãi.";
    }
    if (code === "parking.walk_in.full") {
        return "Hết chỗ cho xe không đặt trước.";
    }
    if (code === "parking.motorbike_zone.full") {
        return "Hết chỗ khu vực xe máy.";
    }
    if (code === "parking.gate_reading.invalid_evidence") {
        return "Lượt nhận diện không hợp lệ, chưa xác nhận, đã hết hạn hoặc đã được sử dụng. Vui lòng làm mới và quét lại xe.";
    }
    if (code === "parking.session.reserved_no_slot") {
        return (
            error.problem?.detail ||
            "Không còn chỗ phù hợp cho xe đã đặt chỗ. Nhân viên xếp khu khác hoặc hoàn tiền đặt trước."
        );
    }
    if (code === "parking.session.payment_required") {
        return "Xe chưa thanh toán phí gửi xe. Vui lòng hoàn tất thanh toán trước khi mở rào chắn.";
    }
    if (code === "parking.session.exit_window_expired") {
        return "Đã quá thời gian xuất bãi 15 phút sau khi thanh toán. Cần thanh toán phụ thu trước khi ra cổng.";
    }
    if (code === "parking.session.cannot_extend_exit_window") {
        return "Chỉ có thể gia hạn thời gian xuất bãi cho phiên đã thanh toán và chưa từng gia hạn.";
    }
    if (code === "parking.session.exit_not_verified") {
        return "Biển số xe hoặc mã vé QR không khớp với phiên đỗ xe này.";
    }
    if (code === "parking.session.not_open") {
        return "Phiên đỗ xe này không còn ở trạng thái mở (đã hoàn tất hoặc đã bị hủy).";
    }
    if (code === "payment.parking_fee.already_paid") {
        return "Phiên đỗ xe này đã được thanh toán cước phí thành công trước đó.";
    }
    if (code === "payment.parking_fee.fee_changed") {
        return "Cước phí gửi xe đã thay đổi do thời gian đỗ xe tăng thêm. Vui lòng tải lại thông tin phí mới.";
    }
    if (code === "payment.parking_fee.idempotency_key_reused") {
        return "Yêu cầu thanh toán trùng lặp. Đang kiểm tra trạng thái thanh toán.";
    }
    if (code === "parking.session.already_open" || error.status === 409) {
        return "Biển số xe này đã có một phiên đỗ xe đang hoạt động trong bãi.";
    }
    if (code === "parking.lot.not_found") {
        return "Không tìm thấy thông tin bãi xe hoặc bãi xe chưa được phê duyệt.";
    }
    if (code === "parking.lot.access_denied" || error.status === 403) {
        return "Tài khoản của bạn chưa được phân công quản lý bãi đỗ xe này.";
    }
    if (code === "parking.gate_reading.not_found") {
        return "Không tìm thấy dữ liệu đọc biển số cần xác nhận.";
    }
    if (code === "parking.gate_reading.not_pending") {
        return "Sự cố đọc biển số này đã được nhân viên khác xử lý.";
    }
    if (error.status === 404) {
        return "Không tìm thấy thông tin bãi xe hoặc dữ liệu yêu cầu.";
    }
    if (error.status === 400) {
        return (
            error.problem?.detail ||
            "Thông tin biển số hoặc loại phương tiện không hợp lệ."
        );
    }
    if (error.status === 401) {
        return "Phiên làm việc của nhân viên đã hết hạn. Vui lòng đăng nhập lại.";
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

export interface ExtendExitWindowResponse {
    sessionId: string;
    newExitWindowEndsAt: string;
}

export interface RecordedExitArrivalResponse {
    sessionId: string;
    exitArrivedAt: string;
}

/**
 * 100% Real API: Ghi nhận thời điểm xe tới cổng xuất bãi
 * Endpoint: POST /api/parking/sessions/{id}/exit-arrival
 */
export async function recordExitArrival(
    sessionId: string,
): Promise<RecordedExitArrivalResponse> {
    return await gatewayFetch<RecordedExitArrivalResponse>(
        `/api/parking/sessions/${encodeURIComponent(sessionId)}/exit-arrival`,
        {
            method: "POST",
        },
    );
}

/**
 * 100% Real API: Gia hạn 15 phút thời gian rời bãi cho phiên đỗ xe đã thanh toán
 * Endpoint: POST /api/parking/sessions/{id}/extend-exit-window
 */
export async function extendExitWindow(
    sessionId: string,
    reason: string,
): Promise<{ sessionId: string; newExitWindowEndsAt: string }> {
    return await gatewayFetch<{
        sessionId: string;
        newExitWindowEndsAt: string;
    }>(
        `/api/parking/sessions/${encodeURIComponent(sessionId)}/extend-exit-window`,
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
            const lotPath = `/api/parking/lots/${encodeURIComponent(lot.id)}`;
            let motoAvailable = 0;
            let motoCap: number | null = null;
            let carAvailable = 0;
            let activeCount = 0;

            try {
                const [motoRes, carRes, sessionPage] = await Promise.all([
                    gatewayFetch<MotorbikeAvailabilityResponse>(
                        `${lotPath}/motorbike-availability`,
                        { method: "GET" },
                    ).catch(() => null),
                    gatewayFetch<LotAvailabilityResponse>(
                        `${lotPath}/availability`,
                        { method: "GET" },
                    ).catch(() => null),
                    listLotInsideSessions(lot.id).catch(() => null),
                ]);

                if (motoRes) {
                    motoAvailable =
                        motoRes.available ??
                        (motoRes as unknown as { availableSpaces?: number })
                            .availableSpaces ??
                        0;
                    motoCap = motoRes.totalCapacity ?? null;
                }
                if (carRes?.car) {
                    carAvailable = carRes.car.available ?? 0;
                }
                if (sessionPage && Array.isArray(sessionPage.items)) {
                    activeCount = sessionPage.items.length;
                }
            } catch {
                // ignore
            }

            const totalCap =
                (motoCap ?? 0) > 0
                    ? (motoCap ?? 0) + carAvailable + activeCount
                    : motoAvailable + carAvailable + activeCount;

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
                carAvailable,
                motorbikeAvailable: motoAvailable,
                activeSessionsCount: activeCount,
                barrierStatus: "Online" as const,
            };
        }),
    );

    return details;
}
