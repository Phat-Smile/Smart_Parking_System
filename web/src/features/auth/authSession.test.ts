import { beforeEach, describe, expect, it, vi } from "vitest";
import {
    clearAuthSession,
    getAuthSessionSnapshot,
    setAuthSession,
} from "./authSession";
import { getAccessToken } from "@/shared/api/access-token";

describe("authSession and refresh token security", () => {
    beforeEach(() => {
        clearAuthSession();
        window.localStorage.clear();
        window.sessionStorage.clear();
    });

    it("does NOT persist refreshToken to localStorage or sessionStorage (XSS protection)", () => {
        setAuthSession({
            accessToken: "secret-access-token",
            tokenType: "Bearer",
            expiresIn: 3600,
            refreshToken: "leakable-refresh-token",
            account: {
                id: "user-123",
                email: "driver@example.com",
                phoneNumber: "0900000001",
                status: "Active",
                role: "Customer",
            },
        });

        expect(getAccessToken()).toBe("secret-access-token");

        const session = getAuthSessionSnapshot();
        expect(session).not.toBeNull();
        expect(session?.account.email).toBe("driver@example.com");
        expect(session?.refreshToken).toBeUndefined();

        // Check raw storage
        const localRaw = window.localStorage.getItem("parkwise:auth-session");
        const sessionRaw = window.sessionStorage.getItem(
            "parkwise:auth-session",
        );

        expect(localRaw).not.toBeNull();
        expect(sessionRaw).not.toBeNull();

        expect(localRaw).not.toContain("leakable-refresh-token");
        expect(sessionRaw).not.toContain("leakable-refresh-token");
        expect(localRaw).not.toContain("refreshToken");
        expect(sessionRaw).not.toContain("refreshToken");
    });

    it("clears storage and memory on clearAuthSession", () => {
        setAuthSession({
            accessToken: "token-to-clear",
            tokenType: "Bearer",
            expiresIn: 3600,
            account: {
                id: "user-456",
                email: "owner@example.com",
                phoneNumber: null,
                status: "Active",
                role: "ParkingLotOwner",
            },
        });

        expect(getAuthSessionSnapshot()).not.toBeNull();
        expect(getAccessToken()).toBe("token-to-clear");

        clearAuthSession();

        expect(getAuthSessionSnapshot()).toBeNull();
        expect(getAccessToken()).toBeUndefined();
        expect(window.localStorage.getItem("parkwise:auth-session")).toBeNull();
        expect(
            window.sessionStorage.getItem("parkwise:auth-session"),
        ).toBeNull();
    });

    it("strips legacy refreshToken when hydrating from storage", async () => {
        window.sessionStorage.setItem(
            "parkwise:auth-session",
            JSON.stringify({
                account: {
                    id: "user-789",
                    email: "legacy@example.com",
                    phoneNumber: null,
                    status: "Active",
                    role: "Customer",
                },
                refreshToken: "legacy-unwanted-refresh-token",
                displayName: "Legacy User",
            }),
        );

        vi.resetModules();
        // Re-importing authSession ensures both authSession and client are in the same module registry
        const { getAuthSessionSnapshot: getFreshSnapshot } =
            await import("./authSession");
        const snapshot = getFreshSnapshot();
        expect(snapshot).not.toBeNull();
        expect(snapshot?.account.email).toBe("legacy@example.com");
        expect(snapshot?.refreshToken).toBeUndefined();
    });

    it("triggers refresh with credentials: include and empty JSON body when gateway returns 401", async () => {
        vi.resetModules();
        const { setAuthSession: setSession } = await import("./authSession");
        const { gatewayFetch } = await import("@/shared/api/client");

        // Seed an active session
        setSession({
            accessToken: "expired-access-token",
            tokenType: "Bearer",
            expiresIn: 3600,
            account: {
                id: "user-101",
                email: "customer@example.com",
                phoneNumber: null,
                status: "Active",
                role: "Customer",
            },
        });

        const fetchCalls: Array<{ url: string; init?: RequestInit }> = [];
        const fetchMock = vi
            .fn()
            .mockImplementation(
                async (input: RequestInfo | URL, init?: RequestInit) => {
                    const url = String(input);
                    fetchCalls.push({ url, init });

                    if (
                        url.includes("/api/parking/test") &&
                        fetchCalls.length === 1
                    ) {
                        // Initial request returns 401
                        return new Response(
                            JSON.stringify({ title: "Unauthorized" }),
                            {
                                status: 401,
                                headers: {
                                    "Content-Type": "application/problem+json",
                                },
                            },
                        );
                    }

                    if (url.includes("/api/identity/accounts/refresh")) {
                        // Refresh endpoint returns 200 with new token
                        return new Response(
                            JSON.stringify({
                                accessToken: "new-fresh-access-token",
                                tokenType: "Bearer",
                                expiresIn: 3600,
                                account: {
                                    id: "user-101",
                                    email: "customer@example.com",
                                    phoneNumber: null,
                                    status: "Active",
                                    role: "Customer",
                                },
                            }),
                            {
                                status: 200,
                                headers: { "Content-Type": "application/json" },
                            },
                        );
                    }

                    // Retried request succeeds
                    return new Response(JSON.stringify({ success: true }), {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    });
                },
            );

        vi.stubGlobal("fetch", fetchMock);

        const result = await gatewayFetch<{ success: boolean }>(
            "/api/parking/test",
        );
        expect(result).toEqual({ success: true });

        // Assert refresh request was made with credentials: "include" and body: "{}"
        const refreshCall = fetchCalls.find((c) =>
            c.url.includes("/api/identity/accounts/refresh"),
        );
        expect(refreshCall).toBeDefined();
        expect(refreshCall?.init?.credentials).toBe("include");
        expect(refreshCall?.init?.body).toBe("{}");

        // Assert active access token updated
        expect(getAccessToken()).toBe("new-fresh-access-token");
    });

    it("clears session when refresh returns 401 or 403", async () => {
        vi.resetModules();
        const {
            setAuthSession: setSession,
            getAuthSessionSnapshot: getSnapshot,
        } = await import("./authSession");
        const { gatewayFetch } = await import("@/shared/api/client");

        setSession({
            accessToken: "expired-access-token",
            tokenType: "Bearer",
            expiresIn: 3600,
            account: {
                id: "user-102",
                email: "customer2@example.com",
                phoneNumber: null,
                status: "Active",
                role: "Customer",
            },
        });

        const fetchMock = vi
            .fn()
            .mockImplementation(async (input: RequestInfo | URL) => {
                const url = String(input);
                if (url.includes("/api/parking/test")) {
                    return new Response(
                        JSON.stringify({ title: "Unauthorized" }),
                        {
                            status: 401,
                            headers: {
                                "Content-Type": "application/problem+json",
                            },
                        },
                    );
                }

                if (url.includes("/api/identity/accounts/refresh")) {
                    return new Response(
                        JSON.stringify({ title: "Session Expired" }),
                        {
                            status: 401,
                            headers: {
                                "Content-Type": "application/problem+json",
                            },
                        },
                    );
                }

                return new Response(null, { status: 404 });
            });

        vi.stubGlobal("fetch", fetchMock);

        await expect(gatewayFetch("/api/parking/test")).rejects.toThrow();

        // Session must be cleared
        expect(getSnapshot()).toBeNull();
        expect(getAccessToken()).toBeUndefined();
    });
});
