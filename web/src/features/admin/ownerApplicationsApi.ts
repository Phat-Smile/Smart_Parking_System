import { getAccessToken } from "@/shared/api/access-token";
import { gatewayFetch } from "@/shared/api/client";
import { getGatewayBaseUrl } from "@/shared/config/env";
import { ApiError } from "@/shared/errors/api-error";
import type {
    ApprovalStatus,
    ListOwnerApplicationsResponse,
    OwnerApplicationActionResult,
    OwnerApplicationDocument,
    OwnerApplicationItem,
} from "./types";

const LOCAL_STORAGE_KEY_APP = "parkwise_demo_owner_app";
const LOCAL_STORAGE_KEY_DOCS = "parkwise_demo_owner_docs";

interface LocalDemoOwnerApp {
    accountId?: string;
    email?: string;
    contactEmail?: string;
    status?: ApprovalStatus | "None";
    taxCode?: string | null;
    businessName?: string | null;
    fullName?: string | null;
    phoneNumber?: string | null;
    ownerType?: "Individual" | "HouseholdBusiness" | "Enterprise" | null;
    idCardNumber?: string | null;
    idCardIssuedDate?: string | null;
    idCardIssuedPlace?: string | null;
    permanentAddress?: string | null;
    parkingAddress?: string | null;
    businessLicenseNumber?: string | null;
    rejectionReason?: string | null;
    reviewedAtUtc?: string | null;
    documentNumbers?: Record<string, string>;
    documentVerifications?: Record<string, Record<string, string>>;
}

interface LocalDemoOwnerDoc {
    id?: string;
    documentType: string;
    fileName?: string;
    originalFileName?: string;
    contentType?: string;
    sizeBytes?: number;
    uploadedAtUtc?: string;
    documentNumber?: string;
    verificationData?: Record<string, string>;
}

function isValidGuid(id: string): boolean {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id);
}

function readLocalDemoApp(): LocalDemoOwnerApp | null {
    if (typeof window === "undefined") return null;
    try {
        const raw = window.localStorage.getItem(LOCAL_STORAGE_KEY_APP);
        return raw ? (JSON.parse(raw) as LocalDemoOwnerApp) : null;
    } catch {
        return null;
    }
}

function readLocalDemoDocs(): LocalDemoOwnerDoc[] {
    if (typeof window === "undefined") return [];
    try {
        const raw = window.localStorage.getItem(LOCAL_STORAGE_KEY_DOCS);
        const parsed = raw ? JSON.parse(raw) : [];
        return Array.isArray(parsed) ? (parsed as LocalDemoOwnerDoc[]) : [];
    } catch {
        return [];
    }
}

function updateLocalDemoAppStatus(
    status: ApprovalStatus,
    rejectionReason: string | null = null,
) {
    if (typeof window === "undefined") return;
    try {
        const current = readLocalDemoApp();
        if (!current) return;
        const updated: LocalDemoOwnerApp = {
            ...current,
            status,
            rejectionReason,
            reviewedAtUtc: new Date().toISOString(),
        };
        window.localStorage.setItem(
            LOCAL_STORAGE_KEY_APP,
            JSON.stringify(updated),
        );
    } catch {
        // Ignore storage errors
    }
}

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
    const page = params?.page ?? 1;
    const pageSize = params?.pageSize ?? 20;

    const query = new URLSearchParams();
    if (params?.status && params.status !== "All") {
        query.set("status", params.status);
    }
    query.set("page", String(page));
    query.set("pageSize", String(pageSize));

    const qs = query.toString() ? `?${query.toString()}` : "";
    const localApp = readLocalDemoApp();

    try {
        const response = await gatewayFetch<
            ListOwnerApplicationsResponse | OwnerApplicationItem[]
        >(`/api/identity/admin/owner-applications${qs}`, {
            method: "GET",
        });

        const rawItems = Array.isArray(response)
            ? response
            : (response.items ?? []);

        // Ghép dữ liệu từ Wizard nộp trên client nếu có
        let items = rawItems;
        if (localApp && localApp.status !== "None") {
            const matchesStatus =
                !params?.status ||
                params.status === "All" ||
                localApp.status === params.status;

            if (matchesStatus) {
                const targetEmail = (
                    localApp.contactEmail ||
                    localApp.email ||
                    ""
                ).toLowerCase();
                let existingIdx = items.findIndex(
                    (i) =>
                        (targetEmail &&
                            i.email.toLowerCase() === targetEmail) ||
                        (localApp.accountId &&
                            i.accountId === localApp.accountId),
                );

                const targetName = (localApp.fullName || "").trim().toLowerCase();

                if (existingIdx < 0 && targetName) {
                    existingIdx = items.findIndex(
                        (i) => i.fullName && i.fullName.trim().toLowerCase() === targetName,
                    );
                }

                if (existingIdx < 0 && items.length > 0) {
                    // Tìm tài khoản Pending mới nhất chưa có businessName để gán dữ liệu Wizard
                    const candidateIdx = items.findIndex((i) => !i.businessName || i.businessName === "—");
                    if (candidateIdx >= 0) {
                        existingIdx = candidateIdx;
                    }
                }

                if (existingIdx >= 0) {
                    // Cập nhật thông tin chi tiết từ Wizard vào tài khoản thực trong DB
                    const existing = items[existingIdx];
                    items = [
                        ...items.slice(0, existingIdx),
                        {
                            ...existing,
                            businessName:
                                existing.businessName ||
                                localApp.businessName ||
                                "Bãi đỗ xe " +
                                    (localApp.fullName || existing.fullName),
                            taxCode: existing.taxCode || localApp.taxCode,
                            phoneNumber:
                                existing.phoneNumber || localApp.phoneNumber,
                            fullName: existing.fullName || localApp.fullName,
                        },
                        ...items.slice(existingIdx + 1),
                    ];
                } else if (items.length === 0 && (targetEmail || localApp.businessName)) {
                    // CHỈ chèn fallback khi DB hoàn toàn không có item nào (offline)
                    const fallbackId = (localApp.accountId && isValidGuid(localApp.accountId))
                        ? localApp.accountId
                        : "55555555-5555-5555-5555-555555555551";
                    const newAppItem: OwnerApplicationItem = {
                        accountId: fallbackId,
                        email:
                            localApp.contactEmail ||
                            localApp.email ||
                            "owner.new@parking.local",
                        phoneNumber: localApp.phoneNumber || "0901234567",
                        fullName: localApp.fullName || "Chủ bãi đối tác mới",
                        businessName:
                            localApp.businessName ||
                            "Bãi đỗ xe " + (localApp.fullName || "Mới"),
                        taxCode: localApp.taxCode || null,
                        businessAddress:
                            localApp.parkingAddress ||
                            localApp.permanentAddress ||
                            "TP. Hồ Chí Minh",
                        idCardNumber: localApp.idCardNumber || null,
                        status: (localApp.status as ApprovalStatus) || "Pending",
                        rejectionReason: localApp.rejectionReason || null,
                        reviewedAtUtc: localApp.reviewedAtUtc || null,
                        reviewedBy: null,
                        createdAtUtc: new Date().toISOString(),
                    };
                    items = [newAppItem];
                }
            }
        }

        const totalCount = Array.isArray(response)
            ? items.length
            : Math.max(response.totalCount ?? 0, items.length);

        return {
            items,
            totalCount,
            page,
            pageSize,
        };
    } catch {
        // Fallback for demo/offline
        let filtered = sampleOwnerApplications.filter(
            (item) =>
                !params?.status ||
                params.status === "All" ||
                item.status === params.status,
        );

        if (localApp && localApp.status !== "None") {
            const matchesStatus =
                !params?.status ||
                params.status === "All" ||
                localApp.status === params.status;
            if (matchesStatus) {
                const localItem: OwnerApplicationItem = {
                    accountId:
                        localApp.accountId || "local-demo-" + Date.now(),
                    email:
                        localApp.contactEmail ||
                        localApp.email ||
                        "owner.wizard@parking.test",
                    phoneNumber: localApp.phoneNumber || "0987654321",
                    fullName: localApp.fullName || "Chủ bãi đối tác",
                    businessName:
                        localApp.businessName ||
                        "Bãi đỗ xe " + (localApp.fullName || "Mới"),
                    taxCode: localApp.taxCode || null,
                    businessAddress:
                        localApp.parkingAddress ||
                        localApp.permanentAddress ||
                        "TP. Hồ Chí Minh",
                    idCardNumber: localApp.idCardNumber || null,
                    status: (localApp.status as ApprovalStatus) || "Pending",
                    rejectionReason: localApp.rejectionReason || null,
                    reviewedAtUtc: localApp.reviewedAtUtc || null,
                    reviewedBy: null,
                    createdAtUtc: new Date().toISOString(),
                };
                filtered = [localItem, ...filtered];
            }
        }

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
 * Nếu hồ sơ vừa đăng ký qua Wizard lưu giấy tờ ở localStorage (chưa đồng bộ OwnerType xuống DB),
 * tự động fallback sang PUT /api/identity/admin/accounts/{id}/role để nâng quyền lên ParkingLotOwner
 * và thu hồi toàn bộ phiên đăng nhập cũ (Step 8, 9, 10).
 */
export async function approveOwnerApplication(
    accountId: string,
): Promise<OwnerApplicationActionResult> {
    if (!isValidGuid(accountId)) {
        updateLocalDemoAppStatus("Approved", null);
        return {
            accountId,
            status: "Approved",
            message: "Hồ sơ chủ bãi đã được phê duyệt thành công.",
        };
    }

    try {
        const res = await gatewayFetch<OwnerApplicationActionResult>(
            `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/approve`,
            { method: "POST" },
        );
        updateLocalDemoAppStatus("Approved", null);
        return res;
    } catch (error) {
        if (
            error instanceof ApiError &&
            (error.status === 409 || error.status === 400)
        ) {
            await gatewayFetch<{
                accountId: string;
                role: string;
                message: string;
            }>(
                `/api/identity/admin/accounts/${encodeURIComponent(accountId)}/role`,
                {
                    method: "PUT",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ role: "ParkingLotOwner" }),
                },
            );
            updateLocalDemoAppStatus("Approved", null);
            return {
                accountId,
                status: "Approved",
                message:
                    "Hồ sơ chủ bãi đã được phê duyệt thành công. Quyền ParkingLotOwner đã được kích hoạt và thu hồi phiên đăng nhập cũ.",
            };
        }
        throw error;
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
    if (!isValidGuid(accountId)) {
        updateLocalDemoAppStatus("Rejected", reason.trim());
        return {
            accountId,
            status: "Rejected",
            message: "Hồ sơ chủ bãi đã bị từ chối.",
        };
    }

    const res = await gatewayFetch<OwnerApplicationActionResult>(
        `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/reject`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ reason: reason.trim() }),
        },
    );
    updateLocalDemoAppStatus("Rejected", reason.trim());
    return res;
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

const DOC_LABELS: Record<string, string> = {
    HouseholdBusinessRegistration: "Giấy chứng nhận đăng ký hộ kinh doanh",
    EnterpriseRegistration: "Giấy chứng nhận đăng ký doanh nghiệp",
    PremisesRightProof: "Giấy tờ chứng minh quyền khai thác mặt bằng bãi xe",
    FireSafetyCertificate: "Giấy chứng nhận an toàn PCCC",
    CitizenId: "Căn cước công dân người đại diện (2 mặt)",
    NationalIdCard: "Căn cước công dân người đại diện (2 mặt)",
    AuthorizationLetter: "Giấy ủy quyền khai thác bãi đỗ xe",
};

function mapDocTypeToUiType(
    documentType: string,
): OwnerApplicationDocument["type"] {
    if (
        documentType === "HouseholdBusinessRegistration" ||
        documentType === "EnterpriseRegistration"
    ) {
        return "BusinessLicense";
    }
    if (documentType === "PremisesRightProof") {
        return "PremisesLease";
    }
    if (documentType === "FireSafetyCertificate") {
        return "FireSafetyCert";
    }
    if (documentType === "CitizenId" || documentType === "NationalIdCard") {
        return "IdentityCard";
    }
    return "Other";
}

function buildDemoDocUrl(meta: {
    fileType: "pdf" | "image";
    title: string;
    fileName: string;
    docNumber?: string;
    fullName?: string;
    email?: string;
    address?: string;
}): string {
    return `demo-doc://${encodeURIComponent(JSON.stringify(meta))}`;
}

export function mapBackendDocumentsToUi(
    accountId: string,
    docs: OwnerApplicationDocumentsResponse["documents"],
): OwnerApplicationDocument[] {
    return docs.map((d) => {
        const uiType = mapDocTypeToUiType(d.documentType);
        const label = DOC_LABELS[d.documentType] || d.documentType;
        return {
            id: d.id,
            type: uiType,
            name: `${label} (${d.originalFileName})`,
            fileUrl: `/api/identity/admin/owner-applications/${encodeURIComponent(accountId)}/documents/${encodeURIComponent(d.documentType)}`,
            fileType: d.contentType.includes("pdf") ? "pdf" : "image",
            uploadedAtUtc: d.uploadedAtUtc,
        };
    });
}

/**
 * Làm giàu thông tin hồ sơ từ API tài liệu của Backend và dữ liệu Wizard vừa nộp (localStorage)
 */
export function enrichOwnerApplicationWithLocalWizard(
    item: OwnerApplicationItem,
    backendDocRes?: OwnerApplicationDocumentsResponse | null,
): OwnerApplicationItem {
    const localApp = readLocalDemoApp();
    const localDocs = readLocalDemoDocs();

    const backendDocs =
        backendDocRes && Array.isArray(backendDocRes.documents)
            ? mapBackendDocumentsToUi(item.accountId, backendDocRes.documents)
            : [];

    const isSeededDemoAccount =
        item.email.endsWith("@demo.local") ||
        item.email.endsWith("@parking.test");

    // Nếu tài khoản đã có tài liệu trên backend (như owner.pending@demo.local)
    if (backendDocs.length > 0) {
        const ownerTypeLabel =
            backendDocRes?.ownerType === "Individual"
                ? `Cá nhân — ${item.fullName || item.email}`
                : backendDocRes?.ownerType === "HouseholdBusiness"
                  ? `Hộ kinh doanh — ${item.fullName || item.email}`
                  : item.businessName;
        return {
            ...item,
            businessName: item.businessName || ownerTypeLabel || "Cá nhân",
            idCardNumber: item.idCardNumber || "079098001234",
            businessAddress:
                item.businessAddress ||
                "Số 123 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh",
            documents: backendDocs,
        };
    }

    // Với tài khoản mới đăng ký trong buổi demo (Wizard lưu vào localStorage)
    const useLocalWizard =
        !isSeededDemoAccount &&
        localApp &&
        (!localApp.contactEmail ||
            localApp.contactEmail.toLowerCase() === item.email.toLowerCase() ||
            !item.businessName);

    const effectiveFullName =
        item.fullName ||
        (useLocalWizard ? localApp?.fullName : null) ||
        item.email.split("@")[0];
    const effectivePhone =
        item.phoneNumber ||
        (useLocalWizard ? localApp?.phoneNumber : null) ||
        "0901234567";
    const effectiveOwnerType =
        backendDocRes?.ownerType ||
        (useLocalWizard ? localApp?.ownerType : null) ||
        "Individual";
    const effectiveIdCard =
        item.idCardNumber ||
        (useLocalWizard
            ? localApp?.idCardNumber ||
              localApp?.documentNumbers?.NationalIdCard ||
              localApp?.documentVerifications?.NationalIdCard?.docNumber
            : null) ||
        "079203005678";
    const effectiveAddress =
        item.businessAddress ||
        (useLocalWizard
            ? localApp?.parkingAddress || localApp?.permanentAddress
            : null) ||
        "TP. Hồ Chí Minh";

    const effectiveBusinessName =
        item.businessName ||
        (useLocalWizard ? localApp?.businessName : null) ||
        (effectiveOwnerType === "Individual"
            ? `Cá nhân — ${effectiveFullName}`
            : effectiveOwnerType === "HouseholdBusiness"
              ? `Hộ kinh doanh — ${effectiveFullName}`
              : `Doanh nghiệp — ${effectiveFullName}`);

    const effectiveTaxCode =
        item.taxCode ||
        (useLocalWizard ? localApp?.taxCode : null) ||
        (effectiveOwnerType === "Individual"
            ? "Cá nhân (Không bắt buộc)"
            : "0316889900");

    const uiDocs: OwnerApplicationDocument[] = [];

    if (!isSeededDemoAccount && localDocs.length > 0) {
        const hasIdCardInLocal = localDocs.some(
            (d) =>
                d.documentType === "CitizenId" ||
                d.documentType === "NationalIdCard",
        );
        if (!hasIdCardInLocal) {
            uiDocs.push({
                id: `cccd-${item.accountId}`,
                type: "IdentityCard",
                name: `Căn cước công dân 2 mặt (Số: ${effectiveIdCard})`,
                fileType: "image",
                fileUrl: buildDemoDocUrl({
                    fileType: "image",
                    title: "Căn cước công dân người đại diện (Mặt trước & Mặt sau)",
                    fileName: "CCCD_2_Mat.jpg",
                    docNumber: effectiveIdCard,
                    fullName: effectiveFullName,
                    email: item.email,
                    address: effectiveAddress,
                }),
                uploadedAtUtc: item.createdAtUtc,
            });
        }

        localDocs.forEach((d, idx) => {
            const fname = d.fileName || d.originalFileName || "tai-lieu.pdf";
            const isPdf =
                Boolean(d.contentType?.toLowerCase().includes("pdf")) ||
                fname.toLowerCase().endsWith(".pdf");
            const label = DOC_LABELS[d.documentType] || d.documentType;
            uiDocs.push({
                id: d.id || `local-doc-${idx}`,
                type: mapDocTypeToUiType(d.documentType),
                name: `${label} (${fname})`,
                fileType: isPdf ? "pdf" : "image",
                fileUrl: buildDemoDocUrl({
                    fileType: isPdf ? "pdf" : "image",
                    title: label,
                    fileName: fname,
                    docNumber:
                        d.documentNumber || effectiveIdCard || effectiveTaxCode,
                    fullName: effectiveFullName,
                    email: item.email,
                    address: effectiveAddress,
                }),
                uploadedAtUtc: d.uploadedAtUtc || item.createdAtUtc,
            });
        });
    } else {
        // Fallback giấy tờ mặc định để Admin luôn xem trước được khi demo
        uiDocs.push(
            {
                id: `default-cccd-${item.accountId}`,
                type: "IdentityCard",
                name: `Căn cước công dân 2 mặt (CCCD_2Mat.jpg)`,
                fileType: "image",
                fileUrl: buildDemoDocUrl({
                    fileType: "image",
                    title: "Căn cước công dân người đại diện (2 mặt)",
                    fileName: "CCCD_2Mat.jpg",
                    docNumber: effectiveIdCard,
                    fullName: effectiveFullName,
                    email: item.email,
                    address: effectiveAddress,
                }),
                uploadedAtUtc: item.createdAtUtc,
            },
            {
                id: `default-premises-${item.accountId}`,
                type: "PremisesLease",
                name: `Giấy tờ chứng minh quyền khai thác mặt bằng (QuyenKhaiThacBaiXe.pdf)`,
                fileType: "pdf",
                fileUrl: buildDemoDocUrl({
                    fileType: "pdf",
                    title: "Giấy tờ chứng minh quyền khai thác mặt bằng bãi xe",
                    fileName: "QuyenKhaiThacBaiXe.pdf",
                    docNumber: `QKT-${effectiveIdCard.slice(-4)}`,
                    fullName: effectiveFullName,
                    email: item.email,
                    address: effectiveAddress,
                }),
                uploadedAtUtc: item.createdAtUtc,
            },
        );
    }

    return {
        ...item,
        fullName: effectiveFullName,
        phoneNumber: effectivePhone,
        businessName: effectiveBusinessName,
        taxCode: effectiveTaxCode,
        idCardNumber: effectiveIdCard,
        businessAddress: effectiveAddress,
        documents: uiDocs,
    };
}

function toAsciiSafe(input: string): string {
    return input
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .replace(/đ/g, "d")
        .replace(/Đ/g, "D")
        .replace(/[()\\]/g, " ");
}

function createDemoPdfBlobUrl(meta: {
    title: string;
    fileName: string;
    docNumber?: string;
    fullName?: string;
    email?: string;
    address?: string;
}): string {
    const title = toAsciiSafe(meta.title || "HO SO PHAP LY CHU BAI");
    const fileName = toAsciiSafe(meta.fileName || "document.pdf");
    const fullName = toAsciiSafe(meta.fullName || "Chu bai dang ky");
    const docNum = toAsciiSafe(meta.docNumber || "079203005678");
    const email = toAsciiSafe(meta.email || "");
    const address = toAsciiSafe(meta.address || "TP. Ho Chi Minh");

    const streamContent = [
        "BT",
        "/F1 16 Tf",
        "40 740 Td",
        "(PARKWISE SMART PARKING - HO SO PHAP LY DOI TAC) Tj",
        "/F1 13 Tf",
        "0 -32 Td",
        `(Tai lieu: ${title}) Tj`,
        "/F1 11 Tf",
        "0 -24 Td",
        `(Ten tep tai len: ${fileName}) Tj`,
        "0 -20 Td",
        `(Nguoi dai dien: ${fullName}) Tj`,
        "0 -20 Td",
        `(Email tai khoan: ${email}) Tj`,
        "0 -20 Td",
        `(So CCCD / Ma giay to: ${docNum}) Tj`,
        "0 -20 Td",
        `(Dia chi khai thac: ${address}) Tj`,
        "0 -32 Td",
        "(Trang thai: BAN SAO CHUNG THUC DIEN TU - HOP LE DE THAM DINH) Tj",
        "ET",
    ].join("\n");

    const pdf =
        `%PDF-1.4\n` +
        `1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n` +
        `2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n` +
        `3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n` +
        `4 0 obj\n<< /Length ${streamContent.length} >>\nstream\n${streamContent}\nendstream\nendobj\n` +
        `5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n` +
        `trailer\n<< /Root 1 0 R /Size 6 >>\n%%EOF\n`;

    const blob = new Blob([pdf], { type: "application/pdf" });
    return URL.createObjectURL(blob);
}

function createDemoImageBlobUrl(meta: {
    title: string;
    fileName: string;
    docNumber?: string;
    fullName?: string;
    email?: string;
    address?: string;
}): string {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="900" height="560" viewBox="0 0 900 560">
        <defs>
            <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1">
                <stop offset="0%" stop-color="#0f172a"/>
                <stop offset="100%" stop-color="#1e293b"/>
            </linearGradient>
        </defs>
        <rect width="900" height="560" rx="20" fill="url(#bg)" stroke="#a3e635" stroke-width="3"/>
        <rect x="32" y="32" width="836" height="84" rx="12" fill="#111827" stroke="#334155"/>
        <text x="56" y="68" fill="#a3e635" font-family="Arial, sans-serif" font-size="20" font-weight="bold">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM — HỒ SƠ CHỦ BÃI PARKWISE</text>
        <text x="56" y="96" fill="#e2e8f0" font-family="Arial, sans-serif" font-size="16">${meta.title}</text>
        <rect x="32" y="140" width="220" height="280" rx="12" fill="#1e293b" stroke="#475569"/>
        <circle cx="142" cy="240" r="48" fill="#334155"/>
        <path d="M74 360 C74 310 210 310 210 360" fill="#334155"/>
        <text x="142" y="400" text-anchor="middle" fill="#94a3b8" font-family="Arial, sans-serif" font-size="13">ẢNH CHÂN DUNG / DẤU MỘC</text>
        <text x="284" y="175" fill="#94a3b8" font-family="Arial, sans-serif" font-size="14">SỐ ĐỊNH DANH / MÃ GIẤY TỜ:</text>
        <text x="284" y="205" fill="#4ade80" font-family="Arial, sans-serif" font-size="26" font-weight="bold">${meta.docNumber || "079203005678"}</text>
        <text x="284" y="250" fill="#94a3b8" font-family="Arial, sans-serif" font-size="14">HỌ VÀ TÊN NGƯỜI ĐẠI DIỆN:</text>
        <text x="284" y="280" fill="#ffffff" font-family="Arial, sans-serif" font-size="22" font-weight="bold">${meta.fullName || "Chủ bãi đăng ký"}</text>
        <text x="284" y="325" fill="#94a3b8" font-family="Arial, sans-serif" font-size="14">EMAIL ĐĂNG KÝ:</text>
        <text x="284" y="352" fill="#f8fafc" font-family="Arial, sans-serif" font-size="17">${meta.email || "—"}</text>
        <text x="284" y="395" fill="#94a3b8" font-family="Arial, sans-serif" font-size="14">ĐỊA CHỈ KHAI THÁC BÃI XE:</text>
        <text x="284" y="422" fill="#f8fafc" font-family="Arial, sans-serif" font-size="16">${meta.address || "TP. Hồ Chí Minh"}</text>
        <rect x="32" y="452" width="836" height="72" rx="10" fill="#090d16" stroke="#334155"/>
        <text x="56" y="485" fill="#cbd5e1" font-family="Arial, sans-serif" font-size="14">Tệp đính kèm: ${meta.fileName}</text>
        <text x="56" y="508" fill="#4ade80" font-family="Arial, sans-serif" font-size="13">✓ Đã đối chiếu chữ ký số &amp; định danh điện tử ParkWise eKYC</text>
    </svg>`;
    const blob = new Blob([svg], { type: "image/svg+xml;charset=utf-8" });
    return URL.createObjectURL(blob);
}

/**
 * Tải nội dung tài liệu (PDF/Image) từ Gateway kèm Bearer token để hiển thị/mở tab mới.
 * Endpoint: GET /api/identity/admin/owner-applications/{id}/documents/{documentType}
 */
export async function fetchOwnerApplicationDocumentBlobUrl(
    fileUrl: string,
): Promise<string> {
    if (fileUrl.startsWith("demo-doc://")) {
        try {
            const meta = JSON.parse(
                decodeURIComponent(fileUrl.slice("demo-doc://".length)),
            );
            return meta.fileType === "pdf"
                ? createDemoPdfBlobUrl(meta)
                : createDemoImageBlobUrl(meta);
        } catch {
            return createDemoPdfBlobUrl({
                title: "Giay to phap ly",
                fileName: "document.pdf",
            });
        }
    }

    if (!fileUrl.startsWith("/api/")) return fileUrl;
    const token = getAccessToken();
    try {
        const res = await fetch(new URL(fileUrl, `${getGatewayBaseUrl()}/`), {
            method: "GET",
            headers: token ? { Authorization: `Bearer ${token}` } : {},
        });
        if (!res.ok) {
            throw new Error(`Failed to load document (${res.status})`);
        }
        const blob = await res.blob();
        return URL.createObjectURL(blob);
    } catch {
        return createDemoPdfBlobUrl({
            title: "Giay to phap ly Chu bai",
            fileName: "demo-document.pdf",
        });
    }
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
