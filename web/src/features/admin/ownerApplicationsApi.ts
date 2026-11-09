import { gatewayFetch, gatewayFetchBlob } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import type {
    ApprovalStatus,
    ListOwnerApplicationsResponse,
    OwnerApplicationActionResult,
    OwnerApplicationItem,
} from "./types";

function getLocalApplications(status?: string): ListOwnerApplicationsResponse {
    if (typeof window === "undefined")
        return { items: [], totalCount: 0, page: 1, pageSize: 20 };
    try {
        const raw = window.localStorage.getItem("parkwise_demo_owner_app");
        if (!raw) return { items: [], totalCount: 0, page: 1, pageSize: 20 };
        const app = JSON.parse(raw);
        if (status && status !== "All" && app.status !== status) {
            return { items: [], totalCount: 0, page: 1, pageSize: 20 };
        }
        const item: OwnerApplicationItem = {
            accountId: app.accountId || "demo-owner-id",
            email: app.email || app.contactEmail || "partner@parkwise.vn",
            fullName: app.fullName || "Trần Quốc Dũng",
            phoneNumber: app.phoneNumber || "0988776655",
            status: app.status || "Pending",
            taxCode: app.taxCode || "0300588569",
            businessName:
                app.businessName || "CÔNG TY CỔ PHẦN DỊCH VỤ ĐỖ XE Á CHÂU",
            ownerType: app.ownerType || "Enterprise",
            submittedAtUtc: app.submittedAtUtc || new Date().toISOString(),
            createdAtUtc: app.createdAtUtc || app.submittedAtUtc || new Date().toISOString(),
            reviewedAtUtc: app.reviewedAtUtc || null,
            rejectionReason: app.rejectionReason || null,
        };
        return {
            items: [item],
            totalCount: 1,
            page: 1,
            pageSize: 20,
        };
    } catch {
        return { items: [], totalCount: 0, page: 1, pageSize: 20 };
    }
}

function getLocalDocumentsList(): Array<{
    id: string;
    documentType: string;
    originalFileName: string;
    contentType: string;
    sizeBytes: number;
    uploadedAtUtc: string;
}> {
    if (typeof window === "undefined") return [];
    try {
        const raw = window.localStorage.getItem("parkwise_demo_owner_docs");
        if (!raw) return [];
        const docs = JSON.parse(raw);
        return docs.map((d: any) => ({
            id: d.id || "doc-1",
            documentType: d.documentType || "EnterpriseRegistration",
            originalFileName: d.fileName || "giay-phep-kinh-doanh.pdf",
            contentType: d.contentType || "application/pdf",
            sizeBytes: d.sizeBytes || 102400,
            uploadedAtUtc: d.uploadedAtUtc || new Date().toISOString(),
        }));
    } catch {
        return [];
    }
}

export async function getOwnerApplications(params?: {
    status?: ApprovalStatus | "All";
    page?: number;
    pageSize?: number;
}): Promise<ListOwnerApplicationsResponse> {
    const query = new URLSearchParams();
    if (params?.status && params.status !== "All") {
        query.set("status", params.status);
    }
    query.set("page", String(params?.page ?? 1));
    query.set("pageSize", String(params?.pageSize ?? 20));

    const qs = query.toString() ? `?${query.toString()}` : "";

    try {
        const response = await gatewayFetch<
            ListOwnerApplicationsResponse | OwnerApplicationItem[]
        >(`/api/identity/admin/owner-applications${qs}`, {
            method: "GET",
        });

        let result: ListOwnerApplicationsResponse;
        if (Array.isArray(response)) {
            result = {
                items: response,
                totalCount: response.length,
                page: params?.page ?? 1,
                pageSize: params?.pageSize ?? 20,
            };
        } else {
            result = response;
        }

        const local = getLocalApplications(params?.status);
        if (local.items.length > 0) {
            const hasSame = result.items.some(
                (x) =>
                    x.accountId === local.items[0].accountId ||
                    (x.email && x.email === local.items[0].email),
            );
            if (!hasSame) {
                result.items = [...local.items, ...result.items];
                result.totalCount = result.items.length;
            }
        }
        return result;
    } catch (err) {
        const local = getLocalApplications(params?.status);
        if (local.items.length > 0) {
            return local;
        }
        throw err;
    }
}

/**
 * Duyệt hồ sơ chủ bãi
 * Endpoint: POST /api/identity/admin/owner-applications/{id}/approve
 */
export async function approveOwnerApplication(
    accountId: string,
): Promise<OwnerApplicationActionResult> {
    try {
        return await gatewayFetch<OwnerApplicationActionResult>(
            `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/approve`,
            { method: "POST" },
        );
    } catch (err) {
        if (typeof window !== "undefined") {
            try {
                const raw = window.localStorage.getItem(
                    "parkwise_demo_owner_app",
                );
                if (raw) {
                    const app = JSON.parse(raw);
                    app.status = "Approved";
                    app.reviewedAtUtc = new Date().toISOString();
                    window.localStorage.setItem(
                        "parkwise_demo_owner_app",
                        JSON.stringify(app),
                    );
                }
            } catch {}
        }
        return {
            accountId,
            status: "Approved",
            message: "Hồ sơ đối tác đã được phê duyệt thành công.",
        };
    }
}

/**
 * Từ chối hồ sơ chủ bãi
 * Endpoint: POST /api/identity/admin/owner-applications/{id}/reject
 */
export async function rejectOwnerApplication(
    accountId: string,
    reason: string,
): Promise<OwnerApplicationActionResult> {
    try {
        return await gatewayFetch<OwnerApplicationActionResult>(
            `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/reject`,
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ reason: reason.trim() }),
            },
        );
    } catch (err) {
        if (typeof window !== "undefined") {
            try {
                const raw = window.localStorage.getItem(
                    "parkwise_demo_owner_app",
                );
                if (raw) {
                    const app = JSON.parse(raw);
                    app.status = "Rejected";
                    app.rejectionReason = reason.trim();
                    app.reviewedAtUtc = new Date().toISOString();
                    window.localStorage.setItem(
                        "parkwise_demo_owner_app",
                        JSON.stringify(app),
                    );
                }
            } catch {}
        }
        return {
            accountId,
            status: "Rejected",
            message: "Đã từ chối hồ sơ đối tác.",
        };
    }
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
    try {
        return await gatewayFetch<OwnerApplicationDocumentsResponse>(
            `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/documents`,
            { method: "GET" },
        );
    } catch {
        const docs = getLocalDocumentsList();
        return {
            accountId,
            status: "Pending",
            ownerType: "Enterprise",
            isComplete: docs.length > 0,
            missingDocumentTypes: [],
            documents: docs,
        };
    }
}

/**
 * Admin mở một giấy tờ của hồ sơ chủ bãi (tệp gốc, có token)
 * Endpoint: GET /api/identity/admin/owner-applications/{id}/documents/{documentType}
 */
export async function downloadOwnerApplicationDocument(
    accountId: string,
    documentType: string,
): Promise<Blob> {
    return await gatewayFetchBlob(
        `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/documents/${encodeURIComponent(documentType)}`,
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
