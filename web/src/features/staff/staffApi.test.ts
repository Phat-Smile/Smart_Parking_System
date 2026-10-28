import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import {
    DEFAULT_STAFF_LOT_ID,
    cancelParkingSession,
    completeParkingSession,
    describeStaffError,
    findSessionByQr,
    getAssignedLot,
    openParkingSession,
} from "./staffApi";

describe("staffApi integration", () => {
    const originalFetch = globalThis.fetch;

    beforeEach(() => {
        vi.restoreAllMocks();
    });

    afterEach(() => {
        globalThis.fetch = originalFetch;
    });

    describe("getAssignedLot", () => {
        it("sends GET to /api/parking/staff/lots", async () => {
            const mockLots = [
                {
                    id: "22222222-2222-2222-2222-222222222222",
                    name: "Bitexco Financial Tower Parking",
                    address: "2 Hải Triều, Bến Nghé, Quận 1",
                    type: "Outdoor",
                },
            ];

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockLots), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await getAssignedLot();
            expect(result.lotId).toBe("22222222-2222-2222-2222-222222222222");
            expect(result.lotName).toBe("Bitexco Financial Tower Parking");
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                "http://localhost:5000/api/parking/staff/lots",
            );
            expect(init?.method).toBe("GET");
        });
    });

    describe("openParkingSession", () => {
        it("sends POST to /api/parking/sessions with uppercase plate and lotId", async () => {
            const mockRes = {
                sessionId: "ses-test-123",
                lotId: DEFAULT_STAFF_LOT_ID,
                plateNumber: "51F-123.45",
                vehicleType: "Car",
                customerType: "Registered",
                status: "Active",
                entryTime: "2026-10-19T08:00:00Z",
                isAccountLinked: true,
                reservationId: null,
                qrToken: "QR-TEST-TOKEN",
                availableMotorbikeSlots: 50,
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockRes), {
                    status: 201,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await openParkingSession({
                lotId: DEFAULT_STAFF_LOT_ID,
                plateNumber: " 51f-123.45 ",
                vehicleType: "Car",
            });

            expect(result).toEqual(mockRes);
            expect(result.availableMotorbikeSlots).toBe(50);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                "http://localhost:5000/api/parking/sessions",
            );
            expect(init?.method).toBe("POST");
            expect(JSON.parse(init?.body as string)).toEqual({
                lotId: DEFAULT_STAFF_LOT_ID,
                plateNumber: "51F-123.45",
                vehicleType: "Car",
            });
        });
    });

    describe("cancelParkingSession", () => {
        it("sends POST to /api/parking/sessions/{id}/cancel with reason", async () => {
            const sessionId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
            const mockRes = {
                sessionId,
                cancelledAt: "2026-10-19T08:05:00Z",
                reason: "Driver changed mind",
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockRes), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await cancelParkingSession(
                sessionId,
                "  Driver changed mind  ",
            );
            expect(result).toEqual(mockRes);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/parking/sessions/${sessionId}/cancel`,
            );
            expect(init?.method).toBe("POST");
            expect(JSON.parse(init?.body as string)).toEqual({
                reason: "Driver changed mind",
            });
        });
    });

    describe("findSessionByQr", () => {
        it("sends GET to /api/parking/sessions/by-qr/{qrToken}", async () => {
            const qrToken = "QR-TEST-51K";
            const mockRes = {
                sessionId: "ses-456",
                lotId: DEFAULT_STAFF_LOT_ID,
                lotName: "Vincom Center Basement",
                plateNumber: "51K-892.41",
                vehicleType: "Car",
                customerType: "Guest",
                entryTime: "2026-10-19T06:00:00Z",
                status: "Active",
                qrToken,
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockRes), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await findSessionByQr(qrToken);
            expect(result.sessionId).toBe(mockRes.sessionId);
            expect(result.plateNumber).toBe(mockRes.plateNumber);
            expect(result.amountDueVnd).toBeDefined();
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/parking/sessions/by-qr/${qrToken}`,
            );
            expect(init?.method).toBe("GET");
        });
    });

    describe("completeParkingSession", () => {
        it("sends POST to /api/parking/sessions/{id}/complete", async () => {
            const sessionId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
            const mockRes = {
                sessionId,
                status: "Completed",
                completedAt: "2026-10-19T09:30:00Z",
                amountPaidVnd: 60000,
                paymentMethod: "Cash",
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockRes), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await completeParkingSession(sessionId, {
                paymentMethod: "Cash",
                amountPaidVnd: 60000,
            });

            expect(result).toEqual(mockRes);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/parking/sessions/${sessionId}/complete`,
            );
            expect(init?.method).toBe("POST");
            expect(JSON.parse(init?.body as string)).toEqual({
                paymentMethod: "Cash",
                amountPaidVnd: 60000,
            });
        });
    });

    describe("describeStaffError", () => {
        it("handles already open session (409)", () => {
            const err = new ApiError(
                409,
                { errorCode: "parking.session.already_open" },
                undefined,
            );
            expect(describeStaffError(err)).toContain(
                "already has an active session",
            );
        });

        it("handles lot access denied (403)", () => {
            const err = new ApiError(
                403,
                { errorCode: "parking.lot.access_denied" },
                undefined,
            );
            expect(describeStaffError(err)).toContain(
                "not assigned to operate",
            );
        });

        it("handles lot not found (404)", () => {
            const err = new ApiError(
                404,
                { errorCode: "parking.lot.not_found" },
                undefined,
            );
            expect(describeStaffError(err)).toContain(
                "not found or is not approved",
            );
        });

        it("handles 401 unauthenticated", () => {
            const err = new ApiError(401, undefined, undefined);
            expect(describeStaffError(err)).toContain("session has expired");
        });
    });
});
