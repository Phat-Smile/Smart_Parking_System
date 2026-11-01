import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import type {
    AdminLotLayout,
    AdminLotPricing,
    AdminParkingLot,
    AdminParkingLotDetail,
    ApprovalStatus,
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

interface AdminLotListResponse {
    items: Array<{
        id: string;
        ownerUserId: string | null;
        name: string;
        address: string;
        latitude: number;
        longitude: number;
        type: string;
        approvalStatus: ApprovalStatus;
        rejectionReason: string | null;
    }>;
}

/**
 * Danh sách bãi cho Admin (mọi trạng thái hoặc lọc theo trạng thái), tối đa 100 bãi một lần gọi.
 * Endpoint: GET /api/parking/admin/lots?status=...&page=1&pageSize=100
 */
export async function getAdminParkingLots(
    status?: ApprovalStatus | "All",
): Promise<AdminParkingLot[]> {
    const query = new URLSearchParams({ page: "1", pageSize: "100" });
    if (status && status !== "All") query.set("status", status);
    const response = await gatewayFetch<AdminLotListResponse>(
        `/api/parking/admin/lots?${query.toString()}`,
        { method: "GET" },
    );
    return response.items.map((item) => ({
        id: item.id,
        name: item.name,
        address: item.address,
        type: item.type,
        ownerUserId: item.ownerUserId,
        status: item.approvalStatus,
        rejectionReason: item.rejectionReason,
        latitude: item.latitude,
        longitude: item.longitude,
    }));
}

/** Chi tiết bãi ở mọi trạng thái duyệt (#128). Endpoint: GET /api/parking/admin/lots/{id} */
export async function getAdminParkingLotDetail(
    parkingLotId: string,
): Promise<AdminParkingLotDetail> {
    return await gatewayFetch<AdminParkingLotDetail>(
        `/api/parking/admin/lots/${encodeURIComponent(parkingLotId)}`,
        { method: "GET" },
    );
}

/** Tầng, khu và chỗ của bãi (#128). Endpoint: GET /api/parking/admin/lots/{id}/layout */
export async function getAdminParkingLotLayout(
    parkingLotId: string,
): Promise<AdminLotLayout> {
    return await gatewayFetch<AdminLotLayout>(
        `/api/parking/admin/lots/${encodeURIComponent(parkingLotId)}/layout`,
        { method: "GET" },
    );
}

/**
 * Bảng giá hiện hành của một loại xe (#128); null khi chủ bãi chưa đặt giá.
 * Endpoint: GET /api/parking/admin/lots/{id}/pricing/{vehicleType}
 */
export async function getAdminParkingLotPricing(
    parkingLotId: string,
    vehicleType: "Car" | "Motorbike",
): Promise<AdminLotPricing | null> {
    try {
        return await gatewayFetch<AdminLotPricing>(
            `/api/parking/admin/lots/${encodeURIComponent(parkingLotId)}/pricing/${vehicleType}`,
            { method: "GET" },
        );
    } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null;
        throw error;
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
