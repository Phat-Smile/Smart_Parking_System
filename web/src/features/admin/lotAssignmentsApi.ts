import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import type { ParkingLotAssignmentMember } from "./types";

export type ShiftType = "morning" | "afternoon" | "night" | "allday";

export const SHIFT_DETAILS: Record<
    ShiftType,
    { shiftName: string; hours: string }
> = {
    morning: { shiftName: "Ca 1 - Sáng", hours: "06:00 - 14:00" },
    afternoon: { shiftName: "Ca 2 - Chiều", hours: "14:00 - 22:00" },
    night: { shiftName: "Ca 3 - Đêm", hours: "22:00 - 06:00" },
    allday: { shiftName: "Cả ngày", hours: "00:00 - 23:59" },
};

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

async function fetchShiftMap(
    lotId: string,
): Promise<
    Map<string, { shift: ShiftType; shiftName: string; hours: string }>
> {
    const shiftMap = new Map<
        string,
        { shift: ShiftType; shiftName: string; hours: string }
    >();
    if (typeof window === "undefined") return shiftMap;
    try {
        const res = await fetch(
            `/api/staff-shifts?lotId=${encodeURIComponent(lotId)}`,
        );
        if (res.ok) {
            const list = await res.json();
            if (Array.isArray(list)) {
                list.forEach(
                    (s: {
                        accountId: string;
                        shift: ShiftType;
                        shiftName: string;
                        hours: string;
                    }) => {
                        shiftMap.set(s.accountId.toLowerCase(), {
                            shift: s.shift,
                            shiftName: s.shiftName,
                            hours: s.hours,
                        });
                    },
                );
            }
        }
    } catch {
        // Ignore internal shift fetch errors
    }
    return shiftMap;
}

/**
 * Gán tài khoản (Staff/Owner) vào bãi đỗ xe và lưu ca trực.
 * Endpoint BE: PUT /api/parking/admin/lots/{lotId}/assignments/{accountId}
 */
export async function assignLotMember(
    lotId: string,
    accountId: string,
    role: "Staff" | "Owner" = "Staff",
    shift: ShiftType = "morning",
): Promise<ParkingLotAssignmentMember> {
    const member = await gatewayFetch<LotAssignmentResponse>(
        assignmentRoute(lotId, accountId),
        {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ role }),
        },
    );

    if (typeof window !== "undefined") {
        try {
            await fetch("/api/staff-shifts", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    lotId,
                    accountId,
                    shift,
                    ...SHIFT_DETAILS[shift],
                }),
            });
        } catch {
            // Ignore shift storage failure
        }
    }

    return {
        lotId,
        accountId: member.accountId,
        role: member.role,
        assignedAtUtc: member.assignedAtUtc,
        shift,
        shiftName: SHIFT_DETAILS[shift]?.shiftName,
        hours: SHIFT_DETAILS[shift]?.hours,
    };
}

/**
 * Gỡ tài khoản khỏi bãi đỗ xe và xóa ca trực tương ứng.
 * Endpoint BE: DELETE /api/parking/admin/lots/{lotId}/assignments/{accountId}
 */
export async function removeLotMember(
    lotId: string,
    accountId: string,
): Promise<void> {
    await gatewayFetch<void>(assignmentRoute(lotId, accountId), {
        method: "DELETE",
    });

    if (typeof window !== "undefined") {
        try {
            await fetch(
                `/api/staff-shifts?lotId=${encodeURIComponent(lotId)}&accountId=${encodeURIComponent(accountId)}`,
                { method: "DELETE" },
            );
        } catch {
            // Ignore shift removal failure
        }
    }
}

/**
 * Danh sách phân công của bãi kèm ca trực (nếu có).
 * Endpoint BE: GET /api/parking/admin/lots/{lotId}/assignments
 */
export async function getLotAssignments(
    lotId: string,
): Promise<ParkingLotAssignmentMember[]> {
    const [rows, shiftMap] = await Promise.all([
        gatewayFetch<LotAssignmentResponse[]>(assignmentRoute(lotId), {
            method: "GET",
        }),
        fetchShiftMap(lotId),
    ]);

    return rows.map((row) => {
        const shiftRecord = shiftMap.get(row.accountId.toLowerCase());
        return {
            lotId,
            accountId: row.accountId,
            role: row.role,
            assignedAtUtc: row.assignedAtUtc,
            shift: shiftRecord?.shift || "morning",
            shiftName: shiftRecord?.shiftName || "Ca 1 - Sáng",
            hours: shiftRecord?.hours || "06:00 - 14:00",
        };
    });
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
