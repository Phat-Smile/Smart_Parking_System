import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
    assignLotMember,
    getLotAssignments,
    removeLotMember,
} from "./lotAssignmentsApi";

describe("lotAssignmentsApi integration", () => {
    const originalFetch = globalThis.fetch;

    beforeEach(() => {
        vi.restoreAllMocks();
    });

    afterEach(() => {
        globalThis.fetch = originalFetch;
    });

    describe("assignLotMember", () => {
        it("sends PUT to /api/parking/admin/lots/{lotId}/assignments/{accountId} with role Staff and persists shift", async () => {
            const lotId = "11111111-1111-1111-1111-111111111111";
            const accountId = "88888888-8888-8888-8888-888888888884";
            const mockRes = {
                lotId,
                accountId,
                role: "Staff",
            };

            const fetchMock = vi.fn().mockImplementation((url: string | URL) => {
                const urlStr = String(url);
                if (urlStr.includes("/api/staff-shifts")) {
                    return Promise.resolve(
                        new Response(JSON.stringify({ success: true }), {
                            status: 200,
                            headers: { "Content-Type": "application/json" },
                        }),
                    );
                }
                return Promise.resolve(
                    new Response(JSON.stringify(mockRes), {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    }),
                );
            });
            vi.stubGlobal("fetch", fetchMock);

            const result = await assignLotMember(lotId, accountId, "Staff", "morning");
            expect(fetchMock).toHaveBeenCalled();

            const putCall = fetchMock.mock.calls.find(([url]) =>
                String(url).includes(`/api/parking/admin/lots/${lotId}/assignments/${accountId}`),
            );
            expect(putCall).toBeDefined();
            expect(putCall![1]?.method).toBe("PUT");
            expect(JSON.parse(putCall![1]?.body as string)).toEqual({ role: "Staff" });

            const shiftCall = fetchMock.mock.calls.find(([url]) =>
                String(url).includes("/api/staff-shifts"),
            );
            expect(shiftCall).toBeDefined();
            expect(shiftCall![1]?.method).toBe("POST");

            expect(result.lotId).toBe(lotId);
            expect(result.accountId).toBe(accountId);
            expect(result.shift).toBe("morning");
        });
    });

    describe("removeLotMember", () => {
        it("sends DELETE to /api/parking/admin/lots/{lotId}/assignments/{accountId}", async () => {
            const lotId = "11111111-1111-1111-1111-111111111111";
            const accountId = "88888888-8888-8888-8888-888888888881";

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(null, {
                    status: 204,
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            await removeLotMember(lotId, accountId);
            expect(fetchMock).toHaveBeenCalled();

            const deleteCall = fetchMock.mock.calls.find(([url]) =>
                String(url).includes(`/api/parking/admin/lots/${lotId}/assignments/${accountId}`),
            );
            expect(deleteCall).toBeDefined();
            expect(deleteCall![1]?.method).toBe("DELETE");
        });
    });

    describe("getLotAssignments", () => {
        it("returns assignments for lotId", async () => {
            const lotId = "11111111-1111-1111-1111-111111111111";
            const list = await getLotAssignments(lotId);
            expect(Array.isArray(list)).toBe(true);
        });
    });
});
