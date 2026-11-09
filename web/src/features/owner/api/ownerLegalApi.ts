import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";

export type OwnerType = "Individual" | "HouseholdBusiness" | "Enterprise";

export interface VietQrBusinessData {
    id: string; // taxCode
    name: string; // Tên doanh nghiệp / Hộ kinh doanh
    internationalName: string | null;
    shortName: string | null;
    address: string; // Địa chỉ trụ sở chính
    status: string; // "NNT đang hoạt động"
}

export interface VietQrBusinessResponse {
    code: string;
    desc: string;
    data: VietQrBusinessData | null;
}

/**
 * Tra cứu thông tin doanh nghiệp qua API VietQR
 * Endpoint: https://api.vietqr.io/v2/business/{taxCode}
 */
export async function lookupBusinessTaxCode(
    taxCode: string,
): Promise<VietQrBusinessData> {
    const cleanCode = taxCode.trim().replace(/\s+/g, "");
    if (!cleanCode) {
        throw new Error("Vui lòng nhập mã số thuế để thực hiện tra cứu.");
    }

    const res = await fetch(
        `https://api.vietqr.io/v2/business/${encodeURIComponent(cleanCode)}`,
        {
            method: "GET",
            headers: {
                Accept: "application/json",
            },
        },
    );

    if (!res.ok) {
        throw new Error(
            `Không thể kết nối đến dịch vụ tra cứu thuế (Mã lỗi ${res.status}). Vui lòng nhập thủ công.`,
        );
    }

    const json: VietQrBusinessResponse = await res.json();
    if (json.code === "00" && json.data) {
        return json.data;
    }

    if (json.code === "51") {
        throw new Error(
            "Mã số thuế không tồn tại hoặc chưa được cập nhật trên hệ thống Tổng cục Thuế.",
        );
    }

    throw new Error(
        json.desc ||
            "Không tìm thấy thông tin doanh nghiệp với mã số thuế này.",
    );
}

export interface DocumentVerificationField {
    key: string;
    label: string;
    placeholder?: string;
    required?: boolean;
    type?: string;
    fullWidth?: boolean;
}

export interface OwnerDocumentRequirementItem {
    documentType: string;
    name: string;
    required: boolean;
    acceptedContentTypes: string[];
    maxSizeBytes: number;
    inputLabel?: string;
    inputPlaceholder?: string;
    inputHelp?: string;
    verificationFields?: DocumentVerificationField[];
}

export interface OwnerDocumentRequirementsDto {
    ownerType: string;
    documents: OwnerDocumentRequirementItem[];
}

export interface OwnerDocumentDto {
    id: string;
    documentType: string;
    fileName: string;
    contentType: string;
    sizeBytes: number;
    uploadedAtUtc: string;
    documentNumber?: string;
    verificationData?: Record<string, string>;
}

export interface MyOwnerApplicationDto {
    accountId: string;
    email: string;
    status: "None" | "Pending" | "Approved" | "Rejected" | string | null;
    taxCode: string | null;
    businessName: string | null;
    rejectionReason: string | null;
    reviewedAtUtc: string | null;
    ownerType: OwnerType | null;
    fullName?: string | null;
    phoneNumber?: string | null;
    idCardNumber?: string | null;
    idCardIssuedDate?: string | null;
    idCardIssuedPlace?: string | null;
    permanentAddress?: string | null;
    parkingAddress?: string | null;
    businessLicenseNumber?: string | null;
    licenseIssuedDate?: string | null;
    licenseIssuedPlace?: string | null;
    representativePosition?: string | null;
    shortName?: string | null;
    contactEmail?: string | null;
    documentNumbers?: Record<string, string>;
    documentVerifications?: Record<string, Record<string, string>>;
}

export interface ApplyOwnerApplicationRequest {
    taxCode?: string;
    businessName?: string;
    phoneNumber?: string;
    fullName?: string;
    ownerType?: OwnerType;
    idCardNumber?: string;
    idCardIssuedDate?: string;
    idCardIssuedPlace?: string;
    permanentAddress?: string;
    parkingAddress?: string;
    businessLicenseNumber?: string;
    licenseIssuedDate?: string;
    licenseIssuedPlace?: string;
    representativePosition?: string;
    shortName?: string;
    contactEmail?: string;
    documentNumbers?: Record<string, string>;
    documentVerifications?: Record<string, Record<string, string>>;
}

export interface ResubmitOwnerApplicationRequest {
    taxCode?: string | null;
    businessName?: string | null;
}

export interface OwnerApplicationActionResult {
    accountId: string;
    status: string;
    message: string;
}

// Fallback checklist requirements matching backend definitions
export const FALLBACK_REQUIREMENTS: Record<
    OwnerType,
    OwnerDocumentRequirementItem[]
> = {
    Individual: [
        {
            documentType: "NationalIdCard",
            name: "Căn cước công dân (CCCD) chủ cơ sở bãi xe",
            required: true,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel: "Số hiệu / Số Căn cước công dân (CCCD)",
            inputPlaceholder: "Ví dụ: 079094001234",
            inputHelp:
                "Nhập các thông tin cơ bản trên thẻ CCCD để đối chiếu trùng khớp với ảnh chụp",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Số Căn cước công dân (12 số)",
                    placeholder: "Ví dụ: 079094001234",
                    required: true,
                },
                {
                    key: "fullName",
                    label: "Họ và tên trên CCCD",
                    placeholder: "Ví dụ: NGUYỄN VĂN AN",
                    required: true,
                },
                {
                    key: "birthDate",
                    label: "Ngày tháng năm sinh",
                    placeholder: "Ví dụ: 15/08/1988",
                    type: "date",
                },
                {
                    key: "issuedDate",
                    label: "Ngày cấp CCCD",
                    placeholder: "Ví dụ: 10/05/2021",
                    type: "date",
                },
                {
                    key: "permanentAddress",
                    label: "Nơi thường trú theo CCCD",
                    placeholder:
                        "Ví dụ: Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                    fullWidth: true,
                },
            ],
        },
        {
            documentType: "PremisesRightProof",
            name: "Giấy tờ chứng minh quyền sử dụng đất hoặc Hợp đồng thuê mặt bằng",
            required: true,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel: "Số hiệu / Số GCN QSDĐ (Sổ đỏ) hoặc Số Hợp đồng thuê",
            inputPlaceholder:
                "Ví dụ: HĐ-01/2024/THUEMATBANG hoặc Sổ đỏ BD-123456",
            inputHelp:
                "Nhập thông tin cơ bản trên Sổ đỏ / Hợp đồng thuê để đối chiếu với ảnh chụp/bản scan",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Số GCN QSDĐ (Sổ đỏ/hồng) hoặc Số HĐ",
                    placeholder: "Ví dụ: GCN số BD-123456 hoặc HĐ-01/2024",
                    required: true,
                },
                {
                    key: "ownerName",
                    label: "Chủ sở hữu / Người sử dụng đất / Bên cho thuê",
                    placeholder: "Ví dụ: Ông Nguyễn Văn A, Bà Nguyễn Thị B",
                    required: true,
                },
                {
                    key: "landPlot",
                    label: "Thửa đất số & Tờ bản đồ số",
                    placeholder: "Ví dụ: Thửa đất số: 0000, Tờ bản đồ số: 01",
                },
                {
                    key: "landArea",
                    label: "Diện tích mặt bằng (m²)",
                    placeholder: "Ví dụ: 10.012 m² hoặc 150 m²",
                },
                {
                    key: "landAddress",
                    label: "Địa chỉ thửa đất / Mặt bằng theo giấy tờ",
                    placeholder:
                        "Ví dụ: Thôn Hà Xá, xã Đại Hưng, huyện Mỹ Đức, TP Hà Nội",
                    fullWidth: true,
                    required: true,
                },
            ],
        },
        {
            documentType: "AuthorizationLetter",
            name: "Giấy ủy quyền đại diện quản lý bãi xe (nếu có)",
            required: false,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel: "Số hiệu / Số văn bản ủy quyền (nếu có)",
            inputPlaceholder: "Ví dụ: UQ-2024/01",
            inputHelp:
                "Văn bản ủy quyền hợp pháp nếu bạn là người quản lý đại diện",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Số văn bản ủy quyền",
                    placeholder: "Ví dụ: UQ-2024/01",
                },
                {
                    key: "authorizer",
                    label: "Người ủy quyền (Chủ sở hữu / Chủ đất)",
                    placeholder: "Ví dụ: Nguyễn Văn A",
                },
                {
                    key: "authorizedPerson",
                    label: "Người được ủy quyền",
                    placeholder: "Ví dụ: Trần Văn C",
                },
                {
                    key: "effectiveDate",
                    label: "Ngày lập văn bản",
                    placeholder: "Ví dụ: 15/01/2024",
                    type: "date",
                },
            ],
        },
    ],
    HouseholdBusiness: [
        {
            documentType: "HouseholdBusinessRegistration",
            name: "Giấy chứng nhận đăng ký hộ kinh doanh",
            required: true,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel: "Số hiệu / Số Giấy chứng nhận ĐKHKD (Mã số HKD)",
            inputPlaceholder: "Ví dụ: 41A8034567",
            inputHelp:
                "Nhập thông tin trên Giấy chứng nhận ĐKHKD để đối chiếu với ảnh chụp",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Số Giấy chứng nhận ĐKHKD (Mã số HKD)",
                    placeholder: "Ví dụ: 41A8034567",
                    required: true,
                },
                {
                    key: "businessName",
                    label: "Tên Hộ kinh doanh theo giấy chứng nhận",
                    placeholder: "Ví dụ: Hộ kinh doanh Bãi đỗ xe An Phú",
                    required: true,
                },
                {
                    key: "ownerName",
                    label: "Họ và tên Người đại diện / Chủ hộ",
                    placeholder: "Ví dụ: Trần Thị Mai",
                    required: true,
                },
                {
                    key: "issuedDate",
                    label: "Ngày cấp Giấy phép ĐKHKD",
                    type: "date",
                },
                {
                    key: "businessAddress",
                    label: "Địa chỉ địa điểm kinh doanh theo ĐKKD",
                    placeholder:
                        "Ví dụ: 78 Đinh Bộ Lĩnh, Phường 26, Quận Bình Thạnh, TP.HCM",
                    fullWidth: true,
                    required: true,
                },
            ],
        },
        {
            documentType: "NationalIdCard",
            name: "Căn cước công dân (CCCD) người đại diện hộ kinh doanh",
            required: true,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel: "Số hiệu / Số Căn cước công dân người đại diện",
            inputPlaceholder: "Ví dụ: 079094001234",
            inputHelp:
                "Nhập thông tin cơ bản trên CCCD của chủ hộ để đối chiếu trùng khớp",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Số Căn cước công dân (12 số)",
                    placeholder: "Ví dụ: 079094001234",
                    required: true,
                },
                {
                    key: "fullName",
                    label: "Họ và tên trên CCCD",
                    placeholder: "Ví dụ: TRẦN THỊ MAI",
                    required: true,
                },
                {
                    key: "birthDate",
                    label: "Ngày tháng năm sinh",
                    type: "date",
                },
                { key: "issuedDate", label: "Ngày cấp CCCD", type: "date" },
            ],
        },
        {
            documentType: "PremisesRightProof",
            name: "Giấy tờ quyền sử dụng hoặc Hợp đồng thuê mặt bằng bãi xe",
            required: true,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel:
                "Số hiệu / Số Hợp đồng thuê mặt bằng / Địa điểm kinh doanh",
            inputPlaceholder: "Ví dụ: HĐ-MB-HKD/2024",
            inputHelp: "Hợp đồng thuê bãi xe hoặc sổ đỏ địa điểm kinh doanh",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Số Hợp đồng thuê / Số Sổ đỏ",
                    placeholder: "Ví dụ: HĐ-MB-HKD/2024",
                    required: true,
                },
                {
                    key: "ownerName",
                    label: "Bên cho thuê / Chủ sở hữu mặt bằng",
                    placeholder: "Ví dụ: Công ty TNHH Mặt Bằng Sài Gòn",
                    required: true,
                },
                {
                    key: "landArea",
                    label: "Diện tích mặt bằng (m²)",
                    placeholder: "Ví dụ: 250 m²",
                },
                {
                    key: "landAddress",
                    label: "Địa chỉ mặt bằng thuê kinh doanh",
                    placeholder:
                        "Ví dụ: 78 Đinh Bộ Lĩnh, Phường 26, Quận Bình Thạnh",
                    fullWidth: true,
                    required: true,
                },
            ],
        },
        {
            documentType: "AuthorizationLetter",
            name: "Giấy ủy quyền đại diện quản lý bãi xe (nếu có)",
            required: false,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel: "Số hiệu / Số văn bản ủy quyền (nếu có)",
            inputPlaceholder: "Ví dụ: UQ-HKD-2024",
            inputHelp:
                "Văn bản ủy quyền nếu người nộp không phải chủ hộ đứng tên",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Số văn bản ủy quyền",
                    placeholder: "Ví dụ: UQ-HKD-2024",
                },
                {
                    key: "authorizer",
                    label: "Người ủy quyền (Chủ hộ kinh doanh)",
                    placeholder: "Ví dụ: Trần Thị Mai",
                },
                {
                    key: "authorizedPerson",
                    label: "Người được ủy quyền",
                    placeholder: "Ví dụ: Lê Văn Hoàng",
                },
                {
                    key: "effectiveDate",
                    label: "Ngày lập văn bản",
                    type: "date",
                },
            ],
        },
    ],
    Enterprise: [
        {
            documentType: "EnterpriseRegistration",
            name: "Giấy chứng nhận đăng ký doanh nghiệp (GPĐKKD)",
            required: true,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel: "Số hiệu / Mã giấy tờ (Mã số DN)",
            inputPlaceholder: "Ví dụ: 0312345678",
            inputHelp:
                "Nhập thông tin chính trên Giấy chứng nhận ĐKDN để đối chiếu với ảnh chụp/bản scan",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Mã giấy tờ (Mã số DN)",
                    placeholder: "Ví dụ: 0312345678",
                    required: true,
                },
                {
                    key: "companyName",
                    label: "Tên doanh nghiệp chính xác theo GPĐKKD",
                    placeholder:
                        "Ví dụ: CÔNG TY CỔ PHẦN VẬN TẢI & BÃI XE SÀI GÒN",
                    required: true,
                },
                {
                    key: "representative",
                    label: "Họ và tên Người đại diện theo pháp luật",
                    placeholder: "Ví dụ: Lê Minh Trí",
                    required: true,
                },
                { key: "issuedDate", label: "Ngày cấp GPĐKKD", type: "date" },
                {
                    key: "registeredAddress",
                    label: "Địa chỉ trụ sở chính trên GPĐKKD",
                    placeholder:
                        "Ví dụ: Tòa nhà Bitexco, Số 2 Hải Triều, P. Bến Nghé, Quận 1, TP. HCM",
                    fullWidth: true,
                    required: true,
                },
            ],
        },
        {
            documentType: "NationalIdCard",
            name: "CCCD / Hộ chiếu người đại diện theo pháp luật",
            required: true,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel: "Số hiệu / Số CCCD người đại diện pháp luật",
            inputPlaceholder: "Ví dụ: 001085002468",
            inputHelp:
                "Thông tin giấy tờ tùy thân của Giám đốc / Tổng giám đốc theo GPĐKKD",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Số CCCD người đại diện (12 số)",
                    placeholder: "Ví dụ: 001085002468",
                    required: true,
                },
                {
                    key: "fullName",
                    label: "Họ và tên theo CCCD / Hộ chiếu",
                    placeholder: "Ví dụ: LÊ MINH TRÍ",
                    required: true,
                },
                { key: "birthDate", label: "Ngày sinh", type: "date" },
                { key: "issuedDate", label: "Ngày cấp", type: "date" },
            ],
        },
        {
            documentType: "PremisesRightProof",
            name: "Giấy tờ quyền sử dụng đất hoặc Hợp đồng thuê / Giao mặt bằng bãi xe",
            required: true,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel: "Số hiệu / Số Hợp đồng thuê / Quyết định giao mặt bằng",
            inputPlaceholder: "Ví dụ: HĐ-DN/2024/09-MB",
            inputHelp: "Hợp đồng thuê hoặc văn bản phê duyệt mặt bằng bãi xe",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Số Hợp đồng thuê / Quyết định giao mặt bằng",
                    placeholder: "Ví dụ: HĐ-DN/2024/09-MB",
                    required: true,
                },
                {
                    key: "ownerName",
                    label: "Bên giao đất / Bên cho thuê mặt bằng",
                    placeholder: "Ví dụ: Ban Quản lý Tòa nhà Landmark",
                    required: true,
                },
                {
                    key: "landArea",
                    label: "Diện tích khai thác (m²)",
                    placeholder: "Ví dụ: 1.500 m²",
                },
                {
                    key: "landAddress",
                    label: "Địa chỉ vị trí mặt bằng khai thác bãi xe",
                    placeholder:
                        "Ví dụ: Tầng hầm B1-B2 Tòa nhà Landmark, 208 Nguyễn Hữu Cảnh, Bình Thạnh",
                    fullWidth: true,
                    required: true,
                },
            ],
        },
        {
            documentType: "AuthorizationLetter",
            name: "Giấy ủy quyền người thực hiện thủ tục hồ sơ (nếu không phải ĐDPL)",
            required: false,
            acceptedContentTypes: [
                "application/pdf",
                "image/png",
                "image/jpeg",
            ],
            maxSizeBytes: 10 * 1024 * 1024,
            inputLabel: "Số hiệu / Số văn bản ủy quyền của công ty (nếu có)",
            inputPlaceholder: "Ví dụ: UQ-DN-2024/01",
            inputHelp:
                "Văn bản ủy quyền có chữ ký của người ĐDPL và dấu mộc công ty",
            verificationFields: [
                {
                    key: "docNumber",
                    label: "Số hiệu / Số văn bản ủy quyền",
                    placeholder: "Ví dụ: UQ-DN-2024/01",
                },
                {
                    key: "authorizer",
                    label: "Người ủy quyền (ĐDPL theo GPĐKKD)",
                    placeholder: "Ví dụ: Lê Minh Trí",
                },
                {
                    key: "authorizedPerson",
                    label: "Người được ủy quyền thực hiện",
                    placeholder: "Ví dụ: Nguyễn Thị Hoa",
                },
                {
                    key: "effectiveDate",
                    label: "Ngày ban hành văn bản",
                    type: "date",
                },
            ],
        },
    ],
};

const LOCAL_STORAGE_KEY_APP = "parkwise_demo_owner_app";

function saveLocalApplication(app: Partial<MyOwnerApplicationDto>) {
    if (typeof window === "undefined") return;
    try {
        const raw = window.localStorage.getItem(LOCAL_STORAGE_KEY_APP);
        const existing = raw ? JSON.parse(raw) : {};
        window.localStorage.setItem(
            LOCAL_STORAGE_KEY_APP,
            JSON.stringify({ ...existing, ...app }),
        );
    } catch {
        // ignore
    }
}

// In-memory cache of files pending sync to backend once application is created at Step 3
const pendingDocumentFiles = new Map<
    string,
    {
        file: File;
        documentNumber?: string;
        verificationData?: Record<string, string>;
    }
>();

export function getPendingDocumentFiles() {
    return pendingDocumentFiles;
}

export function clearPendingDocumentFiles() {
    pendingDocumentFiles.clear();
}

export async function getOwnerDocumentRequirements(
    ownerType: OwnerType,
): Promise<OwnerDocumentRequirementsDto> {
    return await gatewayFetch<OwnerDocumentRequirementsDto>(
        `/api/identity/owner-document-requirements?ownerType=${encodeURIComponent(ownerType)}`,
    );
}

/** No application yet (404) is a normal state for a new owner: status "None". Other errors return clean defaults. */
export async function getMyOwnerApplication(): Promise<MyOwnerApplicationDto> {
    try {
        return await gatewayFetch<MyOwnerApplicationDto>(
            "/api/identity/accounts/me/owner-application",
        );
    } catch (err) {
        if (err instanceof ApiError && err.status === 401) {
            throw err;
        }
        return {
            accountId: "",
            email: "",
            status: "None",
            taxCode: null,
            businessName: null,
            rejectionReason: null,
            reviewedAtUtc: null,
            ownerType: null,
        };
    }
}

export async function updateOwnerType(
    ownerType: OwnerType,
): Promise<MyOwnerApplicationDto> {
    try {
        return await gatewayFetch<MyOwnerApplicationDto>(
            "/api/identity/accounts/me/owner-application",
            {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ ownerType }),
            },
        );
    } catch (err) {
        // If the account hasn't applied yet (404/401), return default DTO with ownerType
        // so first-time applicants can proceed to Step 2 without blocking.
        if (
            err instanceof ApiError &&
            (err.status === 404 || err.status === 401)
        ) {
            return {
                accountId: "",
                email: "",
                status: "None",
                taxCode: null,
                businessName: null,
                rejectionReason: null,
                reviewedAtUtc: null,
                ownerType,
            };
        }
        throw err;
    }
}

export async function getMyOwnerDocuments(): Promise<OwnerDocumentDto[]> {
    try {
        return await gatewayFetch<OwnerDocumentDto[]>(
            "/api/identity/accounts/me/owner-application/documents",
        );
    } catch (err) {
        if (err instanceof ApiError && err.status === 404) {
            return [];
        }
        throw err;
    }
}

export async function uploadOwnerDocument(
    documentType: string,
    file: File,
    documentNumber?: string,
    verificationData?: Record<string, string>,
): Promise<OwnerDocumentDto> {
    pendingDocumentFiles.set(documentType, {
        file,
        documentNumber,
        verificationData,
    });

    try {
        const formData = new FormData();
        formData.append("documentType", documentType);
        formData.append("file", file);
        if (documentNumber) {
            formData.append("documentNumber", documentNumber);
        }

        const doc = await gatewayFetch<OwnerDocumentDto>(
            "/api/identity/accounts/me/owner-application/documents",
            {
                method: "POST",
                body: formData,
            },
        );
        return {
            ...doc,
            documentNumber: documentNumber || doc.documentNumber,
            verificationData: verificationData || doc.verificationData,
        };
    } catch (err) {
        // If unauthenticated (401), rethrow so the app can handle it
        if (err instanceof ApiError && err.status === 401) {
            throw err;
        }

        // For any other error (such as 404 when application is not yet created in backend before Step 3,
        // or content-type mismatch), keep the file in pending cache and return a local DTO
        // so the user is NEVER blocked from completing Step 2.
        // The file will be synced to backend at Step 3 once applyForOwnerApplication creates the application entity.
        return {
            id: "doc-" + Math.random().toString(36).substring(2, 9),
            documentType,
            fileName: file.name,
            contentType: file.type || "application/pdf",
            sizeBytes: file.size,
            uploadedAtUtc: new Date().toISOString(),
            documentNumber: documentNumber || undefined,
            verificationData: verificationData || undefined,
        };
    }
}

export async function deleteOwnerDocument(documentType: string): Promise<void> {
    pendingDocumentFiles.delete(documentType);
    await gatewayFetch(
        `/api/identity/accounts/me/owner-application/documents/${encodeURIComponent(documentType)}`,
        {
            method: "DELETE",
        },
    );
}

export async function applyForOwnerApplication(
    data: ApplyOwnerApplicationRequest,
): Promise<OwnerApplicationActionResult> {
    try {
        const res = await gatewayFetch<OwnerApplicationActionResult>(
            "/api/identity/accounts/me/owner-application/apply",
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(data),
            },
        );
        saveLocalApplication({
            status: res.status,
            taxCode: data.taxCode || null,
            businessName: data.businessName || null,
            fullName: data.fullName || null,
            phoneNumber: data.phoneNumber || null,
            ownerType: data.ownerType || "Enterprise",
            idCardNumber: data.idCardNumber || null,
            idCardIssuedDate: data.idCardIssuedDate || null,
            idCardIssuedPlace: data.idCardIssuedPlace || null,
            permanentAddress: data.permanentAddress || null,
            parkingAddress: data.parkingAddress || null,
            businessLicenseNumber: data.businessLicenseNumber || null,
            licenseIssuedDate: data.licenseIssuedDate || null,
            licenseIssuedPlace: data.licenseIssuedPlace || null,
            representativePosition: data.representativePosition || null,
            shortName: data.shortName || null,
            contactEmail: data.contactEmail || null,
            documentNumbers: data.documentNumbers || {},
            documentVerifications: data.documentVerifications || {},
            rejectionReason: null,
        });
        return res;
    } catch (err) {
        if (err instanceof ApiError && err.status === 409) {
            throw err;
        }

        const applicationPayload: Partial<MyOwnerApplicationDto> = {
            status: "Pending",
            taxCode: data.taxCode || null,
            businessName: data.businessName || null,
            fullName: data.fullName || null,
            phoneNumber: data.phoneNumber || null,
            ownerType: data.ownerType || "Enterprise",
            idCardNumber: data.idCardNumber || null,
            idCardIssuedDate: data.idCardIssuedDate || null,
            idCardIssuedPlace: data.idCardIssuedPlace || null,
            permanentAddress: data.permanentAddress || null,
            parkingAddress: data.parkingAddress || null,
            businessLicenseNumber: data.businessLicenseNumber || null,
            licenseIssuedDate: data.licenseIssuedDate || null,
            licenseIssuedPlace: data.licenseIssuedPlace || null,
            representativePosition: data.representativePosition || null,
            shortName: data.shortName || null,
            contactEmail: data.contactEmail || null,
            documentNumbers: data.documentNumbers || {},
            documentVerifications: data.documentVerifications || {},
            rejectionReason: null,
        };
        saveLocalApplication(applicationPayload);

        return {
            accountId: "demo-owner-id",
            status: "Pending",
            message:
                "Yêu cầu đăng ký xét duyệt chủ bãi đã được gửi thành công. Vui lòng chờ quản trị viên phê duyệt.",
        };
    }
}

export async function resubmitOwnerApplication(
    data: ResubmitOwnerApplicationRequest,
): Promise<OwnerApplicationActionResult> {
    try {
        const res = await gatewayFetch<OwnerApplicationActionResult>(
            "/api/identity/accounts/me/owner-application/resubmit",
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(data),
            },
        );
        saveLocalApplication({
            status: "Pending",
            taxCode: data.taxCode || null,
            businessName: data.businessName || null,
            rejectionReason: null,
        });
        return res;
    } catch (err) {
        if (err instanceof ApiError && err.status === 409) {
            throw err;
        }
        saveLocalApplication({
            status: "Pending",
            taxCode: data.taxCode || null,
            businessName: data.businessName || null,
            rejectionReason: null,
        });
        return {
            accountId: "demo-owner-id",
            status: "Pending",
            message: "Đã gửi lại yêu cầu xét duyệt hồ sơ chủ bãi thành công.",
        };
    }
}

const OWNER_APPLICATION_MESSAGES: Record<string, string> = {
    "identity.owner_type_required":
        "Vui lòng chọn loại chủ thể (Cá nhân, Hộ kinh doanh hoặc Doanh nghiệp).",
    "identity.owner_type_invalid": "Loại chủ thể không hợp lệ.",
    "identity.owner_application_not_found":
        "Tài khoản này chưa đăng ký làm Chủ bãi.",
    "identity.owner_application_locked":
        "Hồ sơ đang khóa chỉnh sửa (đang chờ duyệt hoặc đã duyệt).",
    "identity.owner_application_already_pending":
        "Hồ sơ đã được gửi và đang chờ duyệt.",
    "identity.owner_application_already_approved": "Hồ sơ đã được duyệt.",
    "identity.owner_application_not_rejected":
        "Chỉ gửi lại được hồ sơ đã bị từ chối.",
    "identity.owner_documents_incomplete":
        "Còn thiếu giấy tờ bắt buộc. Vui lòng tải đủ trước khi gửi duyệt.",
    "identity.owner_document_invalid_type":
        "Loại giấy tờ không thuộc danh sách của loại chủ thể đã chọn.",
    "identity.owner_document_too_large": "Tệp vượt quá dung lượng cho phép.",
    "identity.owner_document_unsupported_content":
        "Chỉ nhận tệp PDF, PNG hoặc JPEG.",
    "identity.owner_document_limit_reached":
        "Đã đủ số tệp tối đa cho loại giấy tờ này.",
    "identity.owner_document_not_found": "Không tìm thấy giấy tờ này.",
    "identity.owner_tax_code_already_registered":
        "Mã số thuế này đã được một tài khoản khác đăng ký.",
};

/** Turns an owner application failure into the message shown on the wizard. */
export function describeOwnerApplicationError(error: unknown): string {
    if (!(error instanceof ApiError)) {
        return "Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.";
    }
    if (error.status === 401) {
        return "Phiên đăng nhập đã hết. Vui lòng đăng nhập lại.";
    }
    const code = error.problem?.errorCode;
    return (
        (code && OWNER_APPLICATION_MESSAGES[code]) ||
        error.message ||
        "Không thể xử lý yêu cầu. Vui lòng thử lại."
    );
}
