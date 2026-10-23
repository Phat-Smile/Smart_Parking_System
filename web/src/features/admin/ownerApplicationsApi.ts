import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import type {
    ApprovalStatus,
    ListOwnerApplicationsResponse,
    OwnerApplicationActionResult,
    OwnerApplicationItem,
} from "./types";

const sampleOwnerApplications: OwnerApplicationItem[] = [
    {
        accountId: "55555555-5555-5555-5555-555555555551",
        email: "owner.lethanh@parkwise.vn",
        phoneNumber: "0901234567",
        fullName: "Lê Thành Công",
        businessName: "Công ty TNHH Vận Tải & Dịch Vụ Đỗ Xe Sài Gòn",
        taxCode: "0316789123",
        status: "Pending",
        rejectionReason: null,
        reviewedAtUtc: null,
        reviewedBy: null,
        createdAtUtc: "2026-10-21T09:15:00Z",
    },
    {
        accountId: "55555555-5555-5555-5555-555555555552",
        email: "pham.hoang@smartparking.vn",
        phoneNumber: "0918765432",
        fullName: "Phạm Hoàng Sơn",
        businessName: "Hộ kinh doanh Bãi giữ xe Thông Minh Hoàng Sơn",
        taxCode: "8412345678",
        status: "Pending",
        rejectionReason: null,
        reviewedAtUtc: null,
        reviewedBy: null,
        createdAtUtc: "2026-10-22T03:40:00Z",
    },
    {
        accountId: "55555555-5555-5555-5555-555555555553",
        email: "tran.duc@parkwise.vn",
        phoneNumber: "0933456789",
        fullName: "Trần Đức Minh",
        businessName: "Công ty Cổ phần Quản lý Bãi đỗ xe Miền Nam",
        taxCode: "0319876543-001",
        status: "Approved",
        rejectionReason: null,
        reviewedAtUtc: "2026-10-20T14:20:00Z",
        reviewedBy: "Admin System",
        createdAtUtc: "2026-10-19T08:00:00Z",
    },
];

export async function getOwnerApplications(params?: {
    status?: ApprovalStatus | "All";
    page?: number;
    pageSize?: number;
}): Promise<ListOwnerApplicationsResponse> {
    const query = new URLSearchParams();
    if (params?.status && params.status !== "All") {
        query.set("status", params.status);
    }
    if (params?.page) {
        query.set("page", String(params.page));
    }
    if (params?.pageSize) {
        query.set("pageSize", String(params.pageSize));
    }

    const qs = query.toString() ? `?${query.toString()}` : "";

    try {
        const response = await gatewayFetch<
            ListOwnerApplicationsResponse | OwnerApplicationItem[]
        >(`/api/identity/admin/owner-applications${qs}`, {
            method: "GET",
        });

        if (Array.isArray(response)) {
            return {
                items: response,
                totalCount: response.length,
                page: params?.page ?? 1,
                pageSize: params?.pageSize ?? 20,
            };
        }

        return response;
    } catch {
        // Fallback for demo/offline
        const filtered = sampleOwnerApplications.filter(
            (item) =>
                !params?.status ||
                params.status === "All" ||
                item.status === params.status,
        );
        return {
            items: filtered,
            totalCount: filtered.length,
            page: 1,
            pageSize: 20,
        };
    }
}

/**
 * Duyệt hồ sơ chủ bãi
 * Endpoint: PUT /api/identity/admin/owner-applications/{id}/approve (với fallback POST)
 */
export async function approveOwnerApplication(
    accountId: string,
): Promise<OwnerApplicationActionResult> {
    try {
        return await gatewayFetch<OwnerApplicationActionResult>(
            `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/approve`,
            { method: "PUT" },
        );
    } catch (error) {
        if (
            error instanceof ApiError &&
            (error.status === 405 || error.status === 404)
        ) {
            return await gatewayFetch<OwnerApplicationActionResult>(
                `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/approve`,
                { method: "POST" },
            );
        }
        throw error;
    }
}

/**
 * Từ chối hồ sơ chủ bãi
 * Endpoint: PUT /api/identity/admin/owner-applications/{id}/reject (với fallback POST)
 */
export async function rejectOwnerApplication(
    accountId: string,
    reason: string,
): Promise<OwnerApplicationActionResult> {
    try {
        return await gatewayFetch<OwnerApplicationActionResult>(
            `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/reject`,
            {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ reason: reason.trim() }),
            },
        );
    } catch (error) {
        if (
            error instanceof ApiError &&
            (error.status === 405 || error.status === 404)
        ) {
            return await gatewayFetch<OwnerApplicationActionResult>(
                `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/reject`,
                {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ reason: reason.trim() }),
                },
            );
        }
        throw error;
    }
}

export function describeOwnerAppError(error: unknown): string {
    if (!(error instanceof ApiError)) {
        return "Không thể kết nối đến máy chủ. Vui lòng thử lại sau.";
    }
    if (error.problem?.errorCode === "identity.owner_application_not_pending") {
        return "Hồ sơ này không ở trạng thái Chờ duyệt hoặc đã được xử lý.";
    }
    if (error.problem?.errorCode === "identity.owner_application_not_found") {
        return "Không tìm thấy hồ sơ đăng ký chủ bãi.";
    }
    if (error.status === 403) {
        return "Chỉ quản trị viên hệ thống (SystemAdmin) mới có quyền duyệt hồ sơ.";
    }
    return (
        error.problem?.detail || "Không thể xử lý yêu cầu. Vui lòng thử lại."
    );
}
