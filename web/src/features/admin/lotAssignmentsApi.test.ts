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
        it("sends PUT to /api/parking/admin/lots/{lotId}/assignments/{accountId} with role Staff", async () => {
            const lotId = "11111111-1111-1111-1111-111111111111";
            const accountId = "88888888-8888-8888-8888-888888888884";
            const mockRes = {
                lotId,
                accountId,
                role: "Staff",
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockRes), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await assignLotMember(lotId, accountId, "Staff");
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/parking/admin/lots/${lotId}/assignments/${accountId}`,
            );
            expect(init?.method).toBe("PUT");
            expect(JSON.parse(init?.body as string)).toEqual({ role: "Staff" });
            expect(result.lotId).toBe(lotId);
            expect(result.accountId).toBe(accountId);
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
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/parking/admin/lots/${lotId}/assignments/${accountId}`,
            );
            expect(init?.method).toBe("DELETE");
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
