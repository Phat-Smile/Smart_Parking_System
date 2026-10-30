import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import {
    approveParkingLot,
    describeReviewError,
    getAdminParkingLots,
    isParkingLotId,
    rejectParkingLot,
    validateRejectionReason,
} from "./reviewParkingLot";

describe("reviewParkingLot API integration", () => {
    const originalFetch = globalThis.fetch;

    beforeEach(() => {
        vi.restoreAllMocks();
    });

    afterEach(() => {
        globalThis.fetch = originalFetch;
    });

    describe("isParkingLotId", () => {
        it("accepts valid UUIDs", () => {
            expect(isParkingLotId("11111111-1111-1111-1111-111111111111")).toBe(
                true,
            );
            expect(
                isParkingLotId(" 22222222-2222-2222-2222-222222222222 "),
            ).toBe(true);
        });

        it("rejects invalid or empty UUIDs", () => {
            expect(isParkingLotId("")).toBe(false);
            expect(isParkingLotId("not-a-uuid")).toBe(false);
            expect(isParkingLotId("00000000-0000-0000-0000-000000000000")).toBe(
                false,
            );
        });
    });

    describe("validateRejectionReason", () => {
        it("returns error message when reason is empty", () => {
            expect(validateRejectionReason("")).toBeTruthy();
            expect(validateRejectionReason("   ")).toBeTruthy();
        });

        it("returns empty string when reason is valid (1-500 chars)", () => {
            expect(
                validateRejectionReason("Missing fire safety certificate"),
            ).toBe("");
        });

        it("returns error message when reason exceeds 500 chars", () => {
            const longReason = "a".repeat(501);
            expect(validateRejectionReason(longReason)).toContain(
                "at most 500 characters",
            );
        });
    });

    describe("getAdminParkingLots", () => {
        it("sends GET to /api/parking/admin/lots with status Pending and reads the page", async () => {
            // Shape documented in docs/api/Parking-Owner-Admin-Lots.md (Admin list).
            const mockLots = [
                {
                    id: "11111111-1111-1111-1111-111111111111",
                    ownerUserId: "22222222-2222-2222-2222-222222222222",
                    name: "Vincom Center Basement",
                    address: "72 Lê Thánh Tôn",
                    latitude: 10.77,
                    longitude: 106.7,
                    type: "Underground",
                    approvalStatus: "Pending",
                    rejectionReason: null,
                },
            ];

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(
                    JSON.stringify({
                        items: mockLots,
                        totalCount: 1,
                        page: 1,
                        pageSize: 100,
                    }),
                    {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    },
                ),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await getAdminParkingLots("Pending");
            expect(result).toHaveLength(1);
            expect(result[0].id).toBe(mockLots[0].id);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                "http://localhost:5000/api/parking/admin/lots?page=1&pageSize=100&status=Pending",
            );
            expect(result[0].status).toBe("Pending");
            expect(init?.method).toBe("GET");
        });
    });

    describe("approveParkingLot", () => {
        it("sends POST to /api/parking/admin/lots/{id}/approve without request body", async () => {
            const lotId = "11111111-1111-1111-1111-111111111111";
            const mockResponse = {
                parkingLotId: lotId,
                approvalStatus: "Approved",
                rejectionReason: null,
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockResponse), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await approveParkingLot(lotId);
            expect(result).toEqual(mockResponse);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/parking/admin/lots/${lotId}/approve`,
            );
            expect(init?.method).toBe("POST");
            expect(init?.body).toBeUndefined();
        });
    });

    describe("rejectParkingLot", () => {
        it("sends POST to /api/parking/admin/lots/{id}/reject with reason in json body", async () => {
            const lotId = "11111111-1111-1111-1111-111111111111";
            const reason = "Missing required fire safety documentation";
            const mockResponse = {
                parkingLotId: lotId,
                approvalStatus: "Rejected",
                rejectionReason: reason,
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockResponse), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await rejectParkingLot(lotId, `  ${reason}  `);
            expect(result).toEqual(mockResponse);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/parking/admin/lots/${lotId}/reject`,
            );
            expect(init?.method).toBe("POST");
            expect(JSON.parse(init?.body as string)).toEqual({ reason });
        });
    });

    describe("describeReviewError", () => {
        it("handles 409 already reviewed", () => {
            const error = new ApiError(
                409,
                {
                    status: 409,
                    errorCode: "parking.lot_already_reviewed",
                },
                undefined,
            );
            expect(describeReviewError(error)).toContain("already reviewed");
        });

        it("handles 404 lot not found", () => {
            const error = new ApiError(
                404,
                {
                    status: 404,
                    errorCode: "parking.lot_not_found",
                },
                undefined,
            );
            expect(describeReviewError(error)).toContain("no longer exists");
        });

        it("handles 403 not SystemAdmin", () => {
            const error = new ApiError(403, undefined, undefined);
            expect(describeReviewError(error)).toContain(
                "Only SystemAdmin accounts",
            );
        });

        it("handles 401 unauthenticated", () => {
            const error = new ApiError(401, undefined, undefined);
            expect(describeReviewError(error)).toContain("session expired");
        });

        it("handles generic or network errors", () => {
            expect(describeReviewError(new Error("network failure"))).toContain(
                "couldn't reach the server",
            );
        });

        it("handles Vietnamese localization when language is vi", () => {
            expect(
                describeReviewError(new Error("network failure"), "vi"),
            ).toContain("Không thể kết nối đến máy chủ");
        });
    });
});
