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
        businessAddress:
            "Số 123 Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
        idCardNumber: "079088001234",
        status: "Pending",
        rejectionReason: null,
        reviewedAtUtc: null,
        reviewedBy: null,
        createdAtUtc: "2026-10-21T09:15:00Z",
        documents: [
            {
                id: "doc-101",
                type: "BusinessLicense",
                name: "Giấy chứng nhận đăng ký doanh nghiệp",
                fileUrl:
                    "https://images.unsplash.com/photo-1554415707-9e49017aed81?auto=format&fit=crop&w=1200&q=80",
                fileType: "image",
                uploadedAtUtc: "2026-10-21T09:10:00Z",
            },
            {
                id: "doc-102",
                type: "IdentityCard",
                name: "Căn cước công dân người đại diện pháp luật (2 mặt)",
                fileUrl:
                    "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?auto=format&fit=crop&w=1200&q=80",
                fileType: "image",
                uploadedAtUtc: "2026-10-21T09:11:00Z",
            },
            {
                id: "doc-103",
                type: "FireSafetyCert",
                name: "Giấy chứng nhận thẩm duyệt thiết kế PCCC",
                fileUrl:
                    "https://images.unsplash.com/photo-1589829545856-d10d557cf95f?auto=format&fit=crop&w=1200&q=80",
                fileType: "image",
                uploadedAtUtc: "2026-10-21T09:12:00Z",
            },
            {
                id: "doc-104",
                type: "PremisesLease",
                name: "Hợp đồng thuê mặt bằng bãi đỗ xe",
                fileUrl:
                    "https://images.unsplash.com/photo-1450133064473-71024230f91b?auto=format&fit=crop&w=1200&q=80",
                fileType: "image",
                uploadedAtUtc: "2026-10-21T09:13:00Z",
            },
        ],
    },
    {
        accountId: "55555555-5555-5555-5555-555555555552",
        email: "pham.hoang@smartparking.vn",
        phoneNumber: "0918765432",
        fullName: "Phạm Hoàng Sơn",
        businessName: "Hộ kinh doanh Bãi giữ xe Thông Minh Hoàng Sơn",
        taxCode: "8412345678",
        businessAddress: "45 Lê Duẩn, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
        idCardNumber: "079090005678",
        status: "Pending",
        rejectionReason: null,
        reviewedAtUtc: null,
        reviewedBy: null,
        createdAtUtc: "2026-10-22T03:40:00Z",
        documents: [
            {
                id: "doc-201",
                type: "BusinessLicense",
                name: "Giấy chứng nhận đăng ký hộ kinh doanh cá thể",
                fileUrl:
                    "https://images.unsplash.com/photo-1554415707-9e49017aed81?auto=format&fit=crop&w=1200&q=80",
                fileType: "image",
                uploadedAtUtc: "2026-10-22T03:30:00Z",
            },
            {
                id: "doc-202",
                type: "IdentityCard",
                name: "CCCD chủ hộ kinh doanh",
                fileUrl:
                    "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?auto=format&fit=crop&w=1200&q=80",
                fileType: "image",
                uploadedAtUtc: "2026-10-22T03:32:00Z",
            },
        ],
    },
    {
        accountId: "55555555-5555-5555-5555-555555555553",
        email: "tran.duc@parkwise.vn",
        phoneNumber: "0933456789",
        fullName: "Trần Đức Minh",
        businessName: "Công ty Cổ phần Quản lý Bãi đỗ xe Miền Nam",
        taxCode: "0319876543-001",
        businessAddress: "78 Võ Văn Tần, Phường 6, Quận 3, TP. Hồ Chí Minh",
        idCardNumber: "079085009999",
        status: "Approved",
        rejectionReason: null,
        reviewedAtUtc: "2026-10-20T14:20:00Z",
        reviewedBy: "Admin System",
        createdAtUtc: "2026-10-19T08:00:00Z",
        documents: [
            {
                id: "doc-301",
                type: "BusinessLicense",
                name: "Giấy chứng nhận ĐKKD công ty CP",
                fileUrl:
                    "https://images.unsplash.com/photo-1554415707-9e49017aed81?auto=format&fit=crop&w=1200&q=80",
                fileType: "image",
                uploadedAtUtc: "2026-10-19T07:50:00Z",
            },
            {
                id: "doc-302",
                type: "FireSafetyCert",
                name: "Giấy nghiệm thu an toàn PCCC",
                fileUrl:
                    "https://images.unsplash.com/photo-1589829545856-d10d557cf95f?auto=format&fit=crop&w=1200&q=80",
                fileType: "image",
                uploadedAtUtc: "2026-10-19T07:55:00Z",
            },
        ],
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
