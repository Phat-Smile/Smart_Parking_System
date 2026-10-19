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

export function searchAccounts(query: AccountQuery = {}) {
    const params = new URLSearchParams();
    if (query.search?.trim()) params.set("search", query.search.trim());
    if (query.role) params.set("role", query.role);
    if (query.status) params.set("status", query.status);
    params.set("page", String(query.page ?? 1));
    params.set("pageSize", String(query.pageSize ?? 20));
    return gatewayFetch<AccountPage>(
        `/api/identity/admin/accounts?${params.toString()}`,
        { method: "GET" },
    );
}

function post(accountId: string, action: "suspend" | "activate" | "unlock") {
    return gatewayFetch<{ message?: string }>(
        `/api/identity/admin/accounts/${encodeURIComponent(accountId)}/${action}`,
        { method: "POST" },
    );
}

export const suspendAccount = (accountId: string) => post(accountId, "suspend");
export const activateAccount = (accountId: string) =>
    post(accountId, "activate");
export const unlockAccount = (accountId: string) => post(accountId, "unlock");

export function changeAccountRole(accountId: string, role: AccountRole) {
    return gatewayFetch<{ accountId: string; role: AccountRole; message?: string }>(
        `/api/identity/admin/accounts/${encodeURIComponent(accountId)}/role`,
        {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ role }),
        },
    );
}

export function describeAccountError(error: unknown) {
    if (!(error instanceof ApiError))
        return "We couldn't reach the server. Try again.";
    switch (error.problem?.errorCode) {
        case "identity.cannot_change_own_role":
            return "You cannot change your own role.";
        case "identity.cannot_change_own_status":
            return "You can't change your own account status.";
        case "identity.cannot_change_admin_status":
            return "Another administrator's account can't be suspended.";
        case "identity.account_not_verified":
            return "This account hasn't verified its email yet.";
        case "identity.account_not_found":
            return "This account no longer exists. The list was refreshed.";
    }
    if (error.status === 400)
        return error.problem?.detail || "The request was invalid. Check the role and try again.";
    if (error.status === 401) return "Your session expired. Sign in again.";
    if (error.status === 403) return "You don't have permission to do that.";
    return "The change could not be saved. Please try again.";
}
