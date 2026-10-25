import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
    approveOwnerApplication,
    getOwnerApplications,
    rejectOwnerApplication,
} from "./ownerApplicationsApi";

describe("ownerApplicationsApi integration", () => {
    const originalFetch = globalThis.fetch;

    beforeEach(() => {
        vi.restoreAllMocks();
    });

    afterEach(() => {
        globalThis.fetch = originalFetch;
    });

    describe("getOwnerApplications", () => {
        it("sends GET to /api/identity/admin/owner-applications?status=Pending", async () => {
            const mockData = {
                items: [
                    {
                        accountId: "55555555-5555-5555-5555-555555555551",
                        email: "owner.lethanh@parkwise.vn",
                        fullName: "Lê Thành Công",
                        businessName: "Công ty TNHH Vận Tải Sài Gòn",
                        taxCode: "0316789123",
                        status: "Pending",
                        createdAtUtc: "2026-10-21T09:15:00Z",
                    },
                ],
                totalCount: 1,
                page: 1,
                pageSize: 20,
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockData), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await getOwnerApplications({ status: "Pending" });
            expect(result).toEqual(mockData);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                "http://localhost:5000/api/identity/admin/owner-applications?status=Pending",
            );
            expect(init?.method).toBe("GET");
        });
    });

    describe("approveOwnerApplication", () => {
        it("sends POST to /api/identity/admin/owner-applications/{id}/approve", async () => {
            const accountId = "55555555-5555-5555-5555-555555555551";
            const mockRes = {
                accountId,
                status: "Approved",
                message: "Hồ sơ chủ bãi đã được phê duyệt thành công.",
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockRes), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await approveOwnerApplication(accountId);
            expect(result).toEqual(mockRes);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/identity/admin/owner-applications/${accountId}/approve`,
            );
            expect(init?.method).toBe("POST");
        });
    });

    describe("rejectOwnerApplication", () => {
        it("sends POST to /api/identity/admin/owner-applications/{id}/reject with reason", async () => {
            const accountId = "55555555-5555-5555-5555-555555555551";
            const reason = "Thiếu giấy chứng nhận kinh doanh";
            const mockRes = {
                accountId,
                status: "Rejected",
                message: "Hồ sơ chủ bãi đã bị từ chối.",
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockRes), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await rejectOwnerApplication(accountId, reason);
            expect(result).toEqual(mockRes);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/identity/admin/owner-applications/${accountId}/reject`,
            );
            expect(init?.method).toBe("POST");
            expect(JSON.parse(init?.body as string)).toEqual({ reason });
        });
    });
});
