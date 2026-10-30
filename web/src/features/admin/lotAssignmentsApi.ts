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

interface AccountPageResponse {
    items: Array<{
        id: string;
        email: string;
        fullName: string | null;
        phoneNumber?: string | null;
    }>;
}

interface LotAssignmentResponse {
    accountId: string;
    role: "Staff" | "Owner";
    assignedAtUtc: string;
}

function assignmentRoute(lotId: string, accountId?: string) {
    const base = `/api/parking/admin/lots/${encodeURIComponent(lotId)}/assignments`;
    return accountId ? `${base}/${encodeURIComponent(accountId)}` : base;
}

/**
 * Gán tài khoản (Staff/Owner) vào bãi đỗ xe.
 * Endpoint BE: PUT /api/parking/admin/lots/{lotId}/assignments/{accountId}
 */
export async function assignLotMember(
    lotId: string,
    accountId: string,
    role: "Staff" | "Owner" = "Staff",
): Promise<ParkingLotAssignmentMember> {
    const member = await gatewayFetch<LotAssignmentResponse>(
        assignmentRoute(lotId, accountId),
        {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ role }),
        },
    );
    return {
        lotId,
        accountId: member.accountId,
        role: member.role,
        assignedAtUtc: member.assignedAtUtc,
    };
}

/**
 * Gỡ tài khoản khỏi bãi đỗ xe.
 * Endpoint BE: DELETE /api/parking/admin/lots/{lotId}/assignments/{accountId}
 */
export async function removeLotMember(
    lotId: string,
    accountId: string,
): Promise<void> {
    await gatewayFetch<void>(assignmentRoute(lotId, accountId), {
        method: "DELETE",
    });
}

/**
 * Danh sách phân công của bãi. Parking chỉ trả mã tài khoản, vai trò và thời điểm gán;
 * tên và email lấy từ danh sách tài khoản của Identity ở màn hình.
 * Endpoint BE: GET /api/parking/admin/lots/{lotId}/assignments
 */
export async function getLotAssignments(
    lotId: string,
): Promise<ParkingLotAssignmentMember[]> {
    const rows = await gatewayFetch<LotAssignmentResponse[]>(
        assignmentRoute(lotId),
        { method: "GET" },
    );
    return rows.map((row) => ({
        lotId,
        accountId: row.accountId,
        role: row.role,
        assignedAtUtc: row.assignedAtUtc,
    }));
}

async function listAccountsByRole(
    role: "Staff" | "ParkingLotOwner",
): Promise<StaffAccountOption[]> {
    const page = await gatewayFetch<AccountPageResponse>(
        `/api/identity/admin/accounts?role=${role}&page=1&pageSize=100`,
        { method: "GET" },
    );
    return page.items.map((account) => ({
        accountId: account.id,
        fullName: account.fullName || account.email,
        email: account.email,
        ...(account.phoneNumber ? { phoneNumber: account.phoneNumber } : {}),
        role: role === "Staff" ? ("Staff" as const) : ("Owner" as const),
    }));
}

/**
 * Tài khoản Nhân viên và Chủ bãi có thể gán vào bãi (tối đa 100 mỗi vai trò).
 * Endpoint BE: GET /api/identity/admin/accounts?role=...
 */
export async function getAvailableStaffAccounts(): Promise<
    StaffAccountOption[]
> {
    const [staff, owners] = await Promise.all([
        listAccountsByRole("Staff"),
        listAccountsByRole("ParkingLotOwner"),
    ]);
    return [...staff, ...owners];
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
