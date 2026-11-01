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

export async function searchAccounts(
    query: AccountQuery = {},
): Promise<AccountPage> {
    const params = new URLSearchParams();
    if (query.search?.trim()) params.set("search", query.search.trim());
    if (query.role) params.set("role", query.role);
    if (query.status) params.set("status", query.status);
    params.set("page", String(query.page ?? 1));
    params.set("pageSize", String(query.pageSize ?? 20));
    return await gatewayFetch<AccountPage>(
        `/api/identity/admin/accounts?${params.toString()}`,
        { method: "GET" },
    );
}

async function post(
    accountId: string,
    action: "suspend" | "activate" | "unlock",
) {
    return await gatewayFetch<{ message?: string }>(
        `/api/identity/admin/accounts/${encodeURIComponent(accountId)}/${action}`,
        { method: "POST" },
    );
}

export const suspendAccount = (accountId: string) => post(accountId, "suspend");
export const activateAccount = (accountId: string) =>
    post(accountId, "activate");
export const unlockAccount = (accountId: string) => post(accountId, "unlock");

export async function changeAccountRole(accountId: string, role: AccountRole) {
    return await gatewayFetch<{
        accountId: string;
        role: AccountRole;
        message?: string;
    }>(`/api/identity/admin/accounts/${encodeURIComponent(accountId)}/role`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ role }),
    });
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
        case "identity.owner_role_requires_application":
            return isVi
                ? "Chỉ cấp vai trò Chủ bãi khi hồ sơ chủ bãi của tài khoản này đã được duyệt."
                : "The Parking Lot Owner role needs an approved owner application first.";
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
