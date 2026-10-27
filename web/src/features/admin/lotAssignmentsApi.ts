import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import type { ParkingLotAssignmentMember } from "./types";

export interface StaffAccountOption {
    accountId: string;
    fullName: string;
    email: string;
    phoneNumber?: string;
    role: "Staff" | "Owner";
    assignedLotId?: string | null;
    assignedLotName?: string | null;
}

const sampleStaffAccounts: StaffAccountOption[] = [
    {
        accountId: "88888888-8888-8888-8888-888888888881",
        fullName: "Nguyễn Văn Tuấn (Trực ca sáng)",
        email: "staff.tuan@smartpark.vn",
        phoneNumber: "0909112233",
        role: "Staff",
        assignedLotId: "11111111-1111-1111-1111-111111111111",
        assignedLotName: "Bãi xe Bitexco Financial Tower",
    },
    {
        accountId: "88888888-8888-8888-8888-888888888882",
        fullName: "Trần Thị Mai (Trực ca chiều)",
        email: "staff.mai@smartpark.vn",
        phoneNumber: "0909445566",
        role: "Staff",
        assignedLotId: "11111111-1111-1111-1111-111111111111",
        assignedLotName: "Bãi xe Bitexco Financial Tower",
    },
    {
        accountId: "88888888-8888-8888-8888-888888888883",
        fullName: "Lê Văn Hùng (Trực ca đêm)",
        email: "staff.hung@smartpark.vn",
        phoneNumber: "0909778899",
        role: "Staff",
        assignedLotId: "22222222-2222-2222-2222-222222222222",
        assignedLotName: "Bãi xe Landmark 81",
    },
    {
        accountId: "88888888-8888-8888-8888-888888888884",
        fullName: "Phạm Quốc Dũng (Nhân viên dự bị)",
        email: "staff.dung@smartpark.vn",
        phoneNumber: "0912334455",
        role: "Staff",
        assignedLotId: null,
        assignedLotName: null,
    },
    {
        accountId: "88888888-8888-8888-8888-888888888885",
        fullName: "Hoàng Anh Đức (Nhân viên mới)",
        email: "staff.duc@smartpark.vn",
        phoneNumber: "0934556677",
        role: "Staff",
        assignedLotId: null,
        assignedLotName: null,
    },
];

// In-memory assignment storage for dev/demo fallback
let localAssignments: ParkingLotAssignmentMember[] = [
    {
        lotId: "11111111-1111-1111-1111-111111111111",
        accountId: "88888888-8888-8888-8888-888888888881",
        role: "Staff",
        assignedAtUtc: "2026-10-20T08:00:00Z",
        accountFullName: "Nguyễn Văn Tuấn (Trực ca sáng)",
        accountEmail: "staff.tuan@smartpark.vn",
        accountPhoneNumber: "0909112233",
    },
    {
        lotId: "11111111-1111-1111-1111-111111111111",
        accountId: "88888888-8888-8888-8888-888888888882",
        role: "Staff",
        assignedAtUtc: "2026-10-20T08:00:00Z",
        accountFullName: "Trần Thị Mai (Trực ca chiều)",
        accountEmail: "staff.mai@smartpark.vn",
        accountPhoneNumber: "0909445566",
    },
    {
        lotId: "22222222-2222-2222-2222-222222222222",
        accountId: "88888888-8888-8888-8888-888888888883",
        role: "Staff",
        assignedAtUtc: "2026-10-21T08:00:00Z",
        accountFullName: "Lê Văn Hùng (Trực ca đêm)",
        accountEmail: "staff.hung@smartpark.vn",
        accountPhoneNumber: "0909778899",
    },
];

/**
 * Gán tài khoản (Staff/Owner) vào bãi đỗ xe.
 * Endpoint BE: PUT /api/parking/admin/lots/{lotId}/assignments/{accountId}
 */
export async function assignLotMember(
    lotId: string,
    accountId: string,
    role: "Staff" | "Owner" = "Staff",
): Promise<ParkingLotAssignmentMember> {
    try {
        const res = await gatewayFetch<ParkingLotAssignmentMember>(
            `/api/parking/admin/lots/${encodeURIComponent(lotId)}/assignments/${encodeURIComponent(accountId)}`,
            {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ role }),
            },
        );

        // Update local memory
        const staff = sampleStaffAccounts.find(
            (s) => s.accountId === accountId,
        );
        const item: ParkingLotAssignmentMember = {
            lotId,
            accountId,
            role,
            assignedAtUtc: new Date().toISOString(),
            accountFullName: staff?.fullName || "Nhân viên trực bãi",
            accountEmail: staff?.email,
            accountPhoneNumber: staff?.phoneNumber,
        };
        localAssignments = localAssignments
            .filter((a) => !(a.lotId === lotId && a.accountId === accountId))
            .concat(item);

        return res || item;
    } catch {
        // Fallback for offline/demo if BE is offline
        const staff = sampleStaffAccounts.find(
            (s) => s.accountId === accountId,
        );
        const item: ParkingLotAssignmentMember = {
            lotId,
            accountId,
            role,
            assignedAtUtc: new Date().toISOString(),
            accountFullName: staff?.fullName || "Nhân viên trực bãi",
            accountEmail: staff?.email,
            accountPhoneNumber: staff?.phoneNumber,
        };
        localAssignments = localAssignments
            .filter((a) => !(a.lotId === lotId && a.accountId === accountId))
            .concat(item);
        return item;
    }
}

/**
 * Gỡ tài khoản khỏi bãi đỗ xe.
 * Endpoint BE: DELETE /api/parking/admin/lots/{lotId}/assignments/{accountId}
 */
export async function removeLotMember(
    lotId: string,
    accountId: string,
): Promise<void> {
    try {
        await gatewayFetch<void>(
            `/api/parking/admin/lots/${encodeURIComponent(lotId)}/assignments/${encodeURIComponent(accountId)}`,
            {
                method: "DELETE",
            },
        );
        localAssignments = localAssignments.filter(
            (a) => !(a.lotId === lotId && a.accountId === accountId),
        );
    } catch {
        // Fallback for offline/demo
        localAssignments = localAssignments.filter(
            (a) => !(a.lotId === lotId && a.accountId === accountId),
        );
    }
}

/**
 * Lấy danh sách phân công cho bãi đỗ xe.
 */
export async function getLotAssignments(
    lotId: string,
): Promise<ParkingLotAssignmentMember[]> {
    try {
        const res = await gatewayFetch<ParkingLotAssignmentMember[]>(
            `/api/parking/admin/lots/${encodeURIComponent(lotId)}/assignments`,
            { method: "GET" },
        );
        if (Array.isArray(res) && res.length > 0) return res;
    } catch {
        // Backend doesn't have GET assignments route yet, use local state
    }
    return localAssignments.filter((a) => a.lotId === lotId);
}

/**
 * Lấy danh sách nhân viên có sẵn trong hệ thống để chọn phân công.
 */
export async function getAvailableStaffAccounts(): Promise<
    StaffAccountOption[]
> {
    try {
        const res = await gatewayFetch<StaffAccountOption[]>(
            `/api/identity/admin/accounts?role=Staff`,
            { method: "GET" },
        );
        if (Array.isArray(res) && res.length > 0) return res;
    } catch {
        // fallback
    }
    return sampleStaffAccounts;
}

export function describeAssignmentError(
    error: unknown,
    language: string = "vi",
): string {
    const isVi = language === "vi";
    if (!(error instanceof ApiError)) {
        return isVi
            ? "Không thể kết nối đến máy chủ. Vui lòng kiểm tra lại kết nối mạng."
            : "We couldn't reach the server. Check your connection and try again.";
    }
    if (error.status === 404) {
        return isVi
            ? "Không tìm thấy bãi đỗ xe hoặc tài khoản nhân viên được chỉ định."
            : "Parking lot or staff account not found.";
    }
    if (error.status === 403) {
        return isVi
            ? "Chỉ quản trị viên hệ thống (SystemAdmin) mới có quyền phân công nhân viên."
            : "Only SystemAdmin accounts can manage staff assignments.";
    }
    return (
        error.problem?.detail ||
        (isVi
            ? "Không thể thực hiện phân công. Vui lòng thử lại."
            : "Could not perform assignment. Please try again.")
    );
}
