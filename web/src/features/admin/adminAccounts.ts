import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";

export type AccountRole =
    "Customer" | "Staff" | "ParkingLotOwner" | "SystemAdmin";
export type AccountStatus =
    "PendingEmailVerification" | "EmailVerified" | "Suspended" | "Disabled";

/** Contract of GET /api/identity/admin/accounts (Identity-Auth-Frontend.md). */
export interface AccountSummary {
    id: string;
    email: string;
    fullName: string | null;
    role: AccountRole;
    status: AccountStatus;
    isLocked: boolean;
    lockoutEndUtc: string | null;
    createdAtUtc: string;
}

export interface AccountPage {
    items: AccountSummary[];
    page: number;
    pageSize: number;
    totalCount: number;
}

export interface AccountQuery {
    search?: string;
    role?: AccountRole | "";
    status?: AccountStatus | "";
    page?: number;
    pageSize?: number;
}

export const sampleAccounts: AccountSummary[] = [
    {
        id: "a0000001-0000-0000-0000-000000000001",
        email: "admin@parking.test",
        fullName: "System Administrator",
        role: "SystemAdmin",
        status: "EmailVerified",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-10-01T08:00:00Z",
    },
    {
        id: "a0000001-0000-0000-0000-000000000002",
        email: "hopu3338+t141454adm@gmail.com",
        fullName: "Hồ Phúc (Admin)",
        role: "SystemAdmin",
        status: "EmailVerified",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-10-05T09:15:00Z",
    },
    {
        id: "a0000001-0000-0000-0000-000000000003",
        email: "owner@parking.test",
        fullName: "Nguyễn Minh Khoa",
        role: "ParkingLotOwner",
        status: "EmailVerified",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-10-10T10:30:00Z",
    },
    {
        id: "a0000001-0000-0000-0000-000000000004",
        email: "staff@parking.test",
        fullName: "Trần Văn Nhân",
        role: "Staff",
        status: "EmailVerified",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-10-12T14:20:00Z",
    },
    {
        id: "a0000001-0000-0000-0000-000000000005",
        email: "qa-staff-7197@example.test",
        fullName: "Lê Hoàng Staff",
        role: "Staff",
        status: "EmailVerified",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-10-15T11:00:00Z",
    },
    {
        id: "a0000001-0000-0000-0000-000000000006",
        email: "customer@parking.test",
        fullName: "Phạm Hải Đăng",
        role: "Customer",
        status: "EmailVerified",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-10-18T16:45:00Z",
    },
    {
        id: "a0000001-0000-0000-0000-000000000007",
        email: "qa-user-17854@example.test",
        fullName: "Đỗ Quốc Bảo",
        role: "Customer",
        status: "EmailVerified",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-10-19T08:10:00Z",
    },
    {
        id: "a0000001-0000-0000-0000-000000000008",
        email: "unverified@example.test",
        fullName: "Nguyễn Văn Chờ",
        role: "Customer",
        status: "PendingEmailVerification",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-10-21T12:00:00Z",
    },
    {
        id: "a0000001-0000-0000-0000-000000000009",
        email: "suspended@example.test",
        fullName: "Võ Hoài Nam",
        role: "Customer",
        status: "Suspended",
        isLocked: true,
        lockoutEndUtc: "2026-11-01T00:00:00Z",
        createdAtUtc: "2026-10-22T15:30:00Z",
    },
];

export async function searchAccounts(
    query: AccountQuery = {},
): Promise<AccountPage> {
    const params = new URLSearchParams();
    if (query.search?.trim()) params.set("search", query.search.trim());
    if (query.role) params.set("role", query.role);
    if (query.status) params.set("status", query.status);
    params.set("page", String(query.page ?? 1));
    params.set("pageSize", String(query.pageSize ?? 20));
    try {
        return await gatewayFetch<AccountPage>(
            `/api/identity/admin/accounts?${params.toString()}`,
            { method: "GET" },
        );
    } catch (error) {
        if (error instanceof ApiError) throw error;
        // Fallback for offline development / preview
        let filtered = [...sampleAccounts];
        if (query.search?.trim()) {
            const term = query.search.trim().toLowerCase();
            filtered = filtered.filter(
                (a) =>
                    a.email.toLowerCase().includes(term) ||
                    (a.fullName && a.fullName.toLowerCase().includes(term)),
            );
        }
        if (query.role) {
            filtered = filtered.filter((a) => a.role === query.role);
        }
        if (query.status) {
            filtered = filtered.filter((a) => a.status === query.status);
        }
        const page = query.page ?? 1;
        const pageSize = query.pageSize ?? 20;
        const start = (page - 1) * pageSize;
        const items = filtered.slice(start, start + pageSize);
        return {
            items,
            page,
            pageSize,
            totalCount: filtered.length,
        };
    }
}

async function post(
    accountId: string,
    action: "suspend" | "activate" | "unlock",
) {
    try {
        return await gatewayFetch<{ message?: string }>(
            `/api/identity/admin/accounts/${encodeURIComponent(accountId)}/${action}`,
            { method: "POST" },
        );
    } catch (error) {
        if (error instanceof ApiError) throw error;
        const item = sampleAccounts.find((a) => a.id === accountId);
        if (item) {
            if (action === "suspend") item.status = "Suspended";
            if (action === "activate") item.status = "EmailVerified";
            if (action === "unlock") {
                item.isLocked = false;
                item.lockoutEndUtc = null;
            }
        }
        return { message: "Updated (offline preview)" };
    }
}

export const suspendAccount = (accountId: string) => post(accountId, "suspend");
export const activateAccount = (accountId: string) =>
    post(accountId, "activate");
export const unlockAccount = (accountId: string) => post(accountId, "unlock");

export async function changeAccountRole(accountId: string, role: AccountRole) {
    try {
        return await gatewayFetch<{
            accountId: string;
            role: AccountRole;
            message?: string;
        }>(
            `/api/identity/admin/accounts/${encodeURIComponent(accountId)}/role`,
            {
                method: "PUT",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ role }),
            },
        );
    } catch (error) {
        if (error instanceof ApiError) throw error;
        const item = sampleAccounts.find((a) => a.id === accountId);
        if (item) item.role = role;
        return {
            accountId,
            role,
            message: "Role updated (offline preview)",
        };
    }
}

export function describeAccountError(error: unknown, language: string = "en") {
    const isVi = language === "vi";
    if (!(error instanceof ApiError))
        return isVi
            ? "Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng và thử lại."
            : "We couldn't reach the server. Try again.";
    switch (error.problem?.errorCode) {
        case "identity.cannot_change_own_role":
            return isVi
                ? "Bạn không thể tự thay đổi vai trò của chính mình."
                : "You cannot change your own role.";
        case "identity.cannot_change_own_status":
            return isVi
                ? "Bạn không thể tự thay đổi trạng thái tài khoản của chính mình."
                : "You can't change your own account status.";
        case "identity.cannot_change_admin_status":
            return isVi
                ? "Không thể khóa tài khoản của một Quản trị viên khác."
                : "Another administrator's account can't be suspended.";
        case "identity.account_not_verified":
            return isVi
                ? "Tài khoản này chưa hoàn tất xác thực email."
                : "This account hasn't verified its email yet.";
        case "identity.account_not_found":
            return isVi
                ? "Tài khoản này không còn tồn tại. Danh sách đã được làm mới."
                : "This account no longer exists. The list was refreshed.";
    }
    if (error.status === 400)
        return (
            error.problem?.detail ||
            (isVi
                ? "Yêu cầu không hợp lệ. Vui lòng kiểm tra lại vai trò và thử lại."
                : "The request was invalid. Check the role and try again.")
        );
    if (error.status === 401)
        return isVi
            ? "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
            : "Your session expired. Sign in again.";
    if (error.status === 403)
        return isVi
            ? "Bạn không có quyền thực hiện thao tác này."
            : "You don't have permission to do that.";
    return isVi
        ? "Không thể lưu thay đổi. Vui lòng thử lại."
        : "The change could not be saved. Please try again.";
}
