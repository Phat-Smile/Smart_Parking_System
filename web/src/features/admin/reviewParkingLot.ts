import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import { sampleLots } from "./adminLotsData";
import type {
    AdminParkingLot,
    ApprovalStatus,
    LotFloorLayout,
    LotZone,
    ReviewedParkingLot,
} from "./types";

export const MAX_REJECTION_REASON = 500;

const guidPattern =
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isParkingLotId(value: string) {
    return (
        guidPattern.test(value.trim()) &&
        value.trim() !== "00000000-0000-0000-0000-000000000000"
    );
}

/**
 * Lấy danh sách bãi chờ duyệt bằng GET /api/parking/admin/lots?status=Pending
 */
export async function getAdminParkingLots(
    status?: ApprovalStatus | "All",
): Promise<AdminParkingLot[]> {
    const statusQuery =
        status && status !== "All"
            ? `?status=${encodeURIComponent(status)}`
            : "";
    try {
        const response = await gatewayFetch<
            AdminParkingLot[] | { items: AdminParkingLot[] }
        >(`/api/parking/admin/lots${statusQuery}`, {
            method: "GET",
        });
        const items = Array.isArray(response)
            ? response
            : Array.isArray(response?.items)
              ? response.items
              : [];

        if (items.length > 0) {
            return items.map((raw) => {
                const item = raw as unknown as Record<string, unknown>;
                return {
                    id: String(item.id || item.parkingLotId || ""),
                    name: String(
                        item.name || item.parkingLotName || "Bãi đỗ xe",
                    ),
                    ownerName: String(
                        item.ownerName || item.ownerFullName || "Chủ bãi",
                    ),
                    ownerEmail: String(item.ownerEmail || ""),
                    submittedAtUtc: String(
                        item.submittedAtUtc ||
                            item.createdAtUtc ||
                            new Date().toISOString(),
                    ),
                    status: (item.status ||
                        item.approvalStatus ||
                        "Pending") as ApprovalStatus,
                    rejectionReason:
                        (item.rejectionReason as string | null) ?? null,
                    reviewedAtUtc:
                        (item.reviewedAtUtc as string | null) ?? null,
                    address: String(item.address || ""),
                    city: String(item.city || "TP. Hồ Chí Minh"),
                    latitude:
                        typeof item.latitude === "number"
                            ? item.latitude
                            : 10.77808,
                    longitude:
                        typeof item.longitude === "number"
                            ? item.longitude
                            : 106.70201,
                    is24h: typeof item.is24h === "boolean" ? item.is24h : true,
                    openTime: String(item.openTime || "00:00"),
                    closeTime: String(item.closeTime || "23:59"),
                    vehicleTypes: Array.isArray(item.vehicleTypes)
                        ? (item.vehicleTypes as string[])
                        : ["Car", "Motorbike"],
                    totalCapacity:
                        typeof item.totalCapacity === "number"
                            ? item.totalCapacity
                            : 100,
                    zones: (item.zones as LotZone[]) || [],
                    pricing: (item.pricing as AdminParkingLot["pricing"]) || {
                        firstHourVnd: 20000,
                        extraHourVnd: 15000,
                        dailyCapVnd: 200000,
                        overnightVnd: 100000,
                    },
                    holdDepositVnd:
                        typeof item.holdDepositVnd === "number"
                            ? item.holdDepositVnd
                            : 10000,
                    reservation:
                        (item.reservation as AdminParkingLot["reservation"]) || {
                            holdMinutes: 30,
                            freeCancelMinutes: 15,
                            noShowRule:
                                "Deposit is kept after the hold window expires.",
                        },
                    layout: (item.layout as LotFloorLayout[]) || [],
                    source: "backend" as const,
                };
            });
        }
        return items;
    } catch {
        // Fallback for offline development / preview
        return sampleLots.filter(
            (l) => !status || status === "All" || l.status === status,
        );
    }
}

/** US-ADMIN-002-A: Pending -> Approved. Nút Duyệt gọi thẳng POST /api/parking/admin/lots/{id}/approve. */
export async function approveParkingLot(
    parkingLotId: string,
): Promise<ReviewedParkingLot> {
    return await gatewayFetch<ReviewedParkingLot>(
        `/api/parking/admin/lots/${encodeURIComponent(parkingLotId)}/approve`,
        { method: "POST" },
    );
}

/** US-ADMIN-002-B: Pending -> Rejected. Nút Từ chối gọi thẳng POST /api/parking/admin/lots/{id}/reject. */
export async function rejectParkingLot(
    parkingLotId: string,
    reason: string,
): Promise<ReviewedParkingLot> {
    return await gatewayFetch<ReviewedParkingLot>(
        `/api/parking/admin/lots/${encodeURIComponent(parkingLotId)}/reject`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ reason: reason.trim() }),
        },
    );
}

/** Returns a validation message for a rejection reason, or "" when valid. */
export function validateRejectionReason(
    reason: string,
    language: string = "en",
) {
    const trimmed = reason.trim();
    if (!trimmed) {
        return language === "vi"
            ? "Vui lòng nhập lý do từ chối để chủ bãi nắm thông tin chỉnh sửa."
            : "Enter a reason so the owner knows what to fix.";
    }
    if (trimmed.length > MAX_REJECTION_REASON) {
        return language === "vi"
            ? `Lý do không được vượt quá ${MAX_REJECTION_REASON} ký tự.`
            : `The reason can be at most ${MAX_REJECTION_REASON} characters.`;
    }
    return "";
}

/** Maps the documented 400/401/403/404/409 failures to admin-facing copy. */
export function describeReviewError(error: unknown, language: string = "en") {
    const isVi = language === "vi";
    if (!(error instanceof ApiError))
        return isVi
            ? "Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng và thử lại."
            : "We couldn't reach the server. Check your connection and try again.";
    const code = error.problem?.errorCode;
    if (code === "parking.lot_already_reviewed" || error.status === 409)
        return isVi
            ? "Bãi đỗ xe này đã được xử lý (có thể bởi một quản trị viên khác). Vui lòng tải lại trang để xem quyết định cuối cùng."
            : "This parking lot was already reviewed (possibly by another administrator). Refresh to see the final decision.";
    if (code === "parking.lot_not_found" || error.status === 404)
        return isVi
            ? "Bãi đỗ xe này không còn tồn tại trên hệ thống."
            : "This parking lot no longer exists.";
    if (error.status === 400)
        return isVi
            ? "Quyết định bị từ chối do không hợp lệ. Vui lòng kiểm tra lại lý do từ chối (1–500 ký tự) và thử lại."
            : "The decision was rejected as invalid. Check the rejection reason (1–500 characters) and try again.";
    if (error.status === 401)
        return isVi
            ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại để tiếp tục."
            : "Your session expired. Sign in again to continue.";
    if (error.status === 403)
        return isVi
            ? "Chỉ tài khoản Quản trị hệ thống (SystemAdmin) mới có quyền xét duyệt bãi đỗ xe."
            : "Only SystemAdmin accounts can review parking lots.";
    return isVi
        ? "Không thể lưu quyết định. Vui lòng thử lại."
        : "The decision could not be saved. Please try again.";
}
