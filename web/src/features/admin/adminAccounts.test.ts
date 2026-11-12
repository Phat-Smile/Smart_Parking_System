import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import {
    activateAccount,
    changeAccountRole,
    describeAccountError,
    searchAccounts,
    suspendAccount,
    unlockAccount,
} from "./adminAccounts";

describe("adminAccounts API integration", () => {
    const originalFetch = globalThis.fetch;

    beforeEach(() => {
        vi.restoreAllMocks();
    });

    afterEach(() => {
        globalThis.fetch = originalFetch;
    });

    describe("searchAccounts", () => {
        it("calls GET /api/identity/admin/accounts with proper query parameters", async () => {
            const mockPage = {
                items: [],
                page: 1,
                pageSize: 20,
                totalCount: 0,
            };
            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockPage), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await searchAccounts({
                search: "test@domain.com",
                role: "Customer",
                status: "EmailVerified",
                page: 2,
                pageSize: 10,
            });

            expect(result).toEqual(mockPage);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            const parsed = new URL(String(url));
            expect(parsed.pathname).toBe("/api/identity/admin/accounts");
            expect(parsed.searchParams.get("search")).toBe("test@domain.com");
            expect(parsed.searchParams.get("role")).toBe("Customer");
            expect(parsed.searchParams.get("status")).toBe("EmailVerified");
            expect(parsed.searchParams.get("page")).toBe("2");
            expect(parsed.searchParams.get("pageSize")).toBe("10");
            expect(init?.method).toBe("GET");
        });
    });

    describe("suspendAccount, activateAccount, unlockAccount", () => {
        const accountId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

        it("sends POST to /api/identity/admin/accounts/{id}/suspend", async () => {
            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify({ message: "Suspended" }), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            await suspendAccount(accountId);
            expect(fetchMock).toHaveBeenCalledTimes(1);
            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/identity/admin/accounts/${accountId}/suspend`,
            );
            expect(init?.method).toBe("POST");
        });

        it("sends POST to /api/identity/admin/accounts/{id}/activate", async () => {
            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify({ message: "Activated" }), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            await activateAccount(accountId);
            expect(fetchMock).toHaveBeenCalledTimes(1);
            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/identity/admin/accounts/${accountId}/activate`,
            );
            expect(init?.method).toBe("POST");
        });

        it("sends POST to /api/identity/admin/accounts/{id}/unlock", async () => {
            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify({ accountId, isLocked: false }), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            await unlockAccount(accountId);
            expect(fetchMock).toHaveBeenCalledTimes(1);
            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/identity/admin/accounts/${accountId}/unlock`,
            );
            expect(init?.method).toBe("POST");
        });
    });

    describe("changeAccountRole", () => {
        it("sends PUT to /api/identity/admin/accounts/{id}/role with role in body", async () => {
            const accountId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
            const mockResponse = {
                accountId,
                role: "Staff",
                message: "Role updated",
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockResponse), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await changeAccountRole(accountId, "Staff");
            expect(result).toEqual(mockResponse);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/identity/admin/accounts/${accountId}/role`,
            );
            expect(init?.method).toBe("PUT");
            expect(JSON.parse(init?.body as string)).toEqual({ role: "Staff" });
        });
    });

    describe("describeAccountError", () => {
        it("handles cannot_change_own_role", () => {
            const error = new ApiError(
                403,
                {
                    status: 403,
                    errorCode: "identity.cannot_change_own_role",
                },
                undefined,
            );
            expect(describeAccountError(error)).toContain(
                "cannot change your own role",
            );
        });

        it("handles cannot_change_own_status", () => {
            const error = new ApiError(
                400,
                {
                    status: 400,
                    errorCode: "identity.cannot_change_own_status",
                },
                undefined,
            );
            expect(describeAccountError(error)).toContain(
                "can't change your own account status",
            );
        });

        it("handles cannot_change_admin_status", () => {
            const error = new ApiError(
                403,
                {
                    status: 403,
                    errorCode: "identity.cannot_change_admin_status",
                },
                undefined,
            );
            expect(describeAccountError(error)).toContain(
                "Another administrator's account",
            );
        });

        it("handles account_not_found", () => {
            const error = new ApiError(
                404,
                {
                    status: 404,
                    errorCode: "identity.account_not_found",
                },
                undefined,
            );
            expect(describeAccountError(error)).toContain("no longer exists");
        });
    });
});
