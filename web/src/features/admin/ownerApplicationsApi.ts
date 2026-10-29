import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import type {
    ApprovalStatus,
    ListOwnerApplicationsResponse,
    OwnerApplicationActionResult,
    OwnerApplicationItem,
} from "./types";

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
}

/**
 * Duyệt hồ sơ chủ bãi
 * Endpoint: POST /api/identity/admin/owner-applications/{id}/approve
 */
export async function approveOwnerApplication(
    accountId: string,
): Promise<OwnerApplicationActionResult> {
    return await gatewayFetch<OwnerApplicationActionResult>(
        `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/approve`,
        { method: "POST" },
    );
}

/**
 * Từ chối hồ sơ chủ bãi
 * Endpoint: POST /api/identity/admin/owner-applications/{id}/reject
 */
export async function rejectOwnerApplication(
    accountId: string,
    reason: string,
): Promise<OwnerApplicationActionResult> {
    return await gatewayFetch<OwnerApplicationActionResult>(
        `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/reject`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ reason: reason.trim() }),
        },
    );
}

export interface OwnerApplicationDocumentsResponse {
    accountId: string;
    status: string;
    ownerType?: string;
    isComplete: boolean;
    missingDocumentTypes: string[];
    documents: Array<{
        id: string;
        documentType: string;
        originalFileName: string;
        contentType: string;
        sizeBytes: number;
        uploadedAtUtc: string;
    }>;
}

/**
 * Lấy danh sách giấy tờ và kết quả đối chiếu checklist của hồ sơ chủ bãi (PR #121)
 * Endpoint: GET /api/identity/admin/owner-applications/{id}/documents
 */
export async function getOwnerApplicationDocuments(
    accountId: string,
): Promise<OwnerApplicationDocumentsResponse> {
    return await gatewayFetch<OwnerApplicationDocumentsResponse>(
        `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/documents`,
        { method: "GET" },
    );
}

export function describeOwnerAppError(
    error: unknown,
    language: string = "vi",
): string {
    const isVi = language === "vi";
    if (!(error instanceof ApiError)) {
        return isVi
            ? "Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng và thử lại sau."
            : "We couldn't reach the server. Check your connection and try again.";
    }
    if (error.problem?.errorCode === "identity.owner_application_not_pending") {
        return isVi
            ? "Hồ sơ này không ở trạng thái Chờ duyệt hoặc đã được xử lý."
            : "This application is not pending or has already been reviewed.";
    }
    if (error.problem?.errorCode === "identity.owner_application_not_found") {
        return isVi
            ? "Không tìm thấy hồ sơ đăng ký chủ bãi."
            : "Owner application not found.";
    }
    if (error.status === 403) {
        return isVi
            ? "Chỉ quản trị viên hệ thống (SystemAdmin) mới có quyền duyệt hồ sơ."
            : "Only SystemAdmin accounts can review owner applications.";
    }
    return (
        error.problem?.detail ||
        (isVi
            ? "Không thể xử lý yêu cầu. Vui lòng thử lại."
            : "Could not process request. Please try again.")
    );
}
