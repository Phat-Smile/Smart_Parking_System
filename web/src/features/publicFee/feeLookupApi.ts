import { getGatewayBaseUrl } from "@/shared/config/env";

export interface PublicParkingSessionFeeResponse {
    sessionId: string;
    maskedPlateNumber: string;
    status: string;
    entryTimeUtc: string;
    calculatedAtUtc: string;
    durationMinutes: number;
    calculationMethod: string;
    totalFee: number;
    paidAmount: number;
    amountDue: number;
}

interface RawPublicParkingSessionFeeResponse {
    sessionId?: string;
    SessionId?: string;
    maskedPlateNumber?: string;
    MaskedPlateNumber?: string;
    status?: string;
    Status?: string;
    entryTimeUtc?: string;
    EntryTimeUtc?: string;
    calculatedAtUtc?: string;
    CalculatedAtUtc?: string;
    durationMinutes?: number;
    DurationMinutes?: number;
    calculationMethod?: string;
    CalculationMethod?: string;
    totalFee?: number;
    TotalFee?: number;
    paidAmount?: number;
    PaidAmount?: number;
    amountDue?: number;
    AmountDue?: number;
}

export class FeeLookupError extends Error {
    constructor(
        public readonly statusCode: number,
        message: string,
    ) {
        super(message);
        this.name = "FeeLookupError";
    }
}

export function formatVnd(amount: number): string {
    return `${(amount || 0).toLocaleString("vi-VN")} ₫`;
}

export function formatDurationVi(minutes: number): string {
    if (!minutes || minutes <= 0) return "0 phút";
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours > 0 && mins > 0) {
        return `${hours} giờ ${mins} phút`;
    }
    if (hours > 0) {
        return `${hours} giờ`;
    }
    return `${mins} phút`;
}

export function formatDateTimeVi(isoString: string): string {
    if (!isoString) return "—";
    try {
        const d = new Date(isoString);
        if (Number.isNaN(d.getTime())) return isoString;
        return d.toLocaleString("vi-VN", {
            day: "2-digit",
            month: "2-digit",
            year: "numeric",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
        });
    } catch {
        return isoString;
    }
}

export function mapStatusVi(status: string): {
    label: string;
    tone: "active" | "paid" | "completed" | "cancelled" | "default";
} {
    const s = (status || "").trim().toLowerCase();
    switch (s) {
        case "active":
            return { label: "Đang đỗ", tone: "active" };
        case "paid":
            return { label: "Đã thanh toán", tone: "paid" };
        case "completed":
            return { label: "Hoàn thành", tone: "completed" };
        case "cancelled":
        case "canceled":
            return { label: "Đã hủy", tone: "cancelled" };
        default:
            return { label: status || "Không xác định", tone: "default" };
    }
}

export function mapCalculationMethodVi(method: string): string {
    const m = (method || "").trim().toLowerCase();
    switch (m) {
        case "block":
            return "Theo block thời gian";
        case "turn":
            return "Theo lượt gửi xe";
        case "hour":
            return "Theo giờ";
        case "day":
            return "Theo ngày";
        default:
            return method || "Tiêu chuẩn";
    }
}

export async function fetchPublicParkingFee(
    qrToken: string,
    signal?: AbortSignal,
): Promise<PublicParkingSessionFeeResponse> {
    const trimmed = (qrToken || "").trim();
    if (!trimmed) {
        throw new FeeLookupError(
            404,
            "Không tìm thấy phiên đỗ hoặc mã QR không hợp lệ",
        );
    }

    const gatewayUrl = getGatewayBaseUrl();
    const endpoint = `${gatewayUrl}/api/payment/public/sessions/fee?qr=${encodeURIComponent(trimmed)}`;

    let response: Response;
    try {
        response = await fetch(endpoint, {
            method: "GET",
            headers: {
                Accept: "application/json",
            },
            cache: "no-store",
            signal,
        });
    } catch (err: unknown) {
        if (err instanceof DOMException && err.name === "AbortError") {
            throw err;
        }
        throw new FeeLookupError(
            503,
            "Dịch vụ tính phí tạm thời không khả dụng",
        );
    }

    if (response.status === 404) {
        throw new FeeLookupError(
            404,
            "Không tìm thấy phiên đỗ hoặc mã QR không hợp lệ",
        );
    }

    if (response.status === 429) {
        throw new FeeLookupError(
            429,
            "Vui lòng thử lại sau giây lát do vượt giới hạn tra cứu",
        );
    }

    if (response.status === 503) {
        throw new FeeLookupError(
            503,
            "Dịch vụ tính phí tạm thời không khả dụng",
        );
    }

    if (!response.ok) {
        throw new FeeLookupError(
            response.status,
            "Có lỗi xảy ra trong quá trình tra cứu. Vui lòng thử lại sau.",
        );
    }

    const raw = (await response.json()) as RawPublicParkingSessionFeeResponse;

    return {
        sessionId: raw.sessionId ?? raw.SessionId ?? "",
        maskedPlateNumber: raw.maskedPlateNumber ?? raw.MaskedPlateNumber ?? "",
        status: raw.status ?? raw.Status ?? "Active",
        entryTimeUtc: raw.entryTimeUtc ?? raw.EntryTimeUtc ?? "",
        calculatedAtUtc: raw.calculatedAtUtc ?? raw.CalculatedAtUtc ?? "",
        durationMinutes: raw.durationMinutes ?? raw.DurationMinutes ?? 0,
        calculationMethod:
            raw.calculationMethod ?? raw.CalculationMethod ?? "Block",
        totalFee: raw.totalFee ?? raw.TotalFee ?? 0,
        paidAmount: raw.paidAmount ?? raw.PaidAmount ?? 0,
        amountDue: raw.amountDue ?? raw.AmountDue ?? 0,
    };
}
