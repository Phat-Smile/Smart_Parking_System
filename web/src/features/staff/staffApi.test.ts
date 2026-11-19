import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import {
    DEFAULT_STAFF_LOT_ID,
    cancelParkingSession,
    completeParkingSession,
    describeStaffError,
    findSessionByQr,
    getAssignedLot,
    listLotInsideSessions,
    openParkingSession,
    payParkingSessionCash,
} from "./staffApi";

describe("staffApi integration", () => {
    const originalFetch = globalThis.fetch;

    beforeEach(() => {
        vi.restoreAllMocks();
        try {
            sessionStorage.clear();
        } catch {
            // ignore
        }
    });

    afterEach(() => {
        globalThis.fetch = originalFetch;
        try {
            sessionStorage.clear();
        } catch {
            // ignore
        }
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

            // The lot list, then the motorbike and car free spaces Parking counts.
            const fetchMock = vi
                .fn()
                .mockResolvedValueOnce(
                    new Response(JSON.stringify(mockLots), {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    }),
                )
                .mockResolvedValue(
                    new Response(JSON.stringify({ status: 404 }), {
                        status: 404,
                        headers: { "Content-Type": "application/problem+json" },
                    }),
                );
            vi.stubGlobal("fetch", fetchMock);

            const result = await getAssignedLot();
            expect(result?.lotId).toBe("22222222-2222-2222-2222-222222222222");
            expect(result?.lotName).toBe("Bitexco Financial Tower Parking");
            expect(fetchMock).toHaveBeenCalledTimes(3);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                "http://localhost:5000/api/parking/staff/lots",
            );
            expect(init?.method).toBe("GET");
        });

        it("picks the first lot with car availability when multiple lots exist instead of DEFAULT_STAFF_LOT_ID", async () => {
            const mockLots = [
                {
                    id: DEFAULT_STAFF_LOT_ID,
                    name: "Bãi Xe Máy",
                    address: "1 Đường A",
                    type: "Outdoor",
                },
                {
                    id: "lot-car-available",
                    name: "Bãi Ô Tô",
                    address: "2 Đường B",
                    type: "Indoor",
                },
            ];

            const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
                const url = String(input);
                if (url.endsWith("/api/parking/staff/lots")) {
                    return new Response(JSON.stringify(mockLots), {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    });
                }
                if (
                    url.endsWith(
                        `/api/parking/lots/${DEFAULT_STAFF_LOT_ID}/availability`,
                    )
                ) {
                    return new Response(
                        JSON.stringify({ car: { available: 0 } }),
                        {
                            status: 200,
                            headers: { "Content-Type": "application/json" },
                        },
                    );
                }
                if (
                    url.endsWith(
                        "/api/parking/lots/lot-car-available/availability",
                    )
                ) {
                    return new Response(
                        JSON.stringify({ car: { available: 12 } }),
                        {
                            status: 200,
                            headers: { "Content-Type": "application/json" },
                        },
                    );
                }
                if (
                    url.endsWith(
                        "/api/parking/lots/lot-car-available/motorbike-availability",
                    )
                ) {
                    return new Response(
                        JSON.stringify({ available: 20, totalCapacity: 50 }),
                        {
                            status: 200,
                            headers: { "Content-Type": "application/json" },
                        },
                    );
                }
                return new Response(JSON.stringify({ status: 404 }), {
                    status: 404,
                    headers: { "Content-Type": "application/problem+json" },
                });
            });
            vi.stubGlobal("fetch", fetchMock);

            const result = await getAssignedLot();
            expect(result?.lotId).toBe("lot-car-available");
            expect(result?.availableCarSlots).toBe(12);
        });

        it("falls back to lots[0] when multiple lots exist and none have car availability", async () => {
            const mockLots = [
                {
                    id: "lot-first",
                    name: "First Lot",
                    address: "1 First",
                    type: "Outdoor",
                },
                {
                    id: DEFAULT_STAFF_LOT_ID,
                    name: "Hardcoded Lot",
                    address: "2 Second",
                    type: "Indoor",
                },
            ];

            const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
                const url = String(input);
                if (url.endsWith("/api/parking/staff/lots")) {
                    return new Response(JSON.stringify(mockLots), {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    });
                }
                if (url.endsWith("/availability")) {
                    return new Response(
                        JSON.stringify({ car: { available: 0 } }),
                        {
                            status: 200,
                            headers: { "Content-Type": "application/json" },
                        },
                    );
                }
                return new Response(JSON.stringify({ status: 404 }), {
                    status: 404,
                    headers: { "Content-Type": "application/problem+json" },
                });
            });
            vi.stubGlobal("fetch", fetchMock);

            const result = await getAssignedLot();
            expect(result?.lotId).toBe("lot-first");
        });

        it("uses lot saved in sessionStorage and ignores sessionStorage errors", async () => {
            const mockLots = [
                {
                    id: "lot-alpha",
                    name: "Lot Alpha",
                    address: "1 Alpha",
                    type: "Outdoor",
                },
                {
                    id: "lot-beta",
                    name: "Lot Beta",
                    address: "2 Beta",
                    type: "Indoor",
                },
            ];
            sessionStorage.setItem(
                "parkwise_staff_selected_lot_id",
                "lot-beta",
            );

            const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
                const url = String(input);
                if (url.endsWith("/api/parking/staff/lots")) {
                    return new Response(JSON.stringify(mockLots), {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    });
                }
                if (url.endsWith("/api/parking/lots/lot-alpha/availability")) {
                    return new Response(
                        JSON.stringify({ car: { available: 4 } }),
                        {
                            status: 200,
                            headers: { "Content-Type": "application/json" },
                        },
                    );
                }
                return new Response(JSON.stringify({ status: 404 }), {
                    status: 404,
                    headers: { "Content-Type": "application/problem+json" },
                });
            });
            vi.stubGlobal("fetch", fetchMock);

            const fromStorage = await getAssignedLot();
            expect(fromStorage?.lotId).toBe("lot-beta");

            vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
                throw new Error("SecurityError");
            });

            const afterStorageError = await getAssignedLot();
            expect(afterStorageError?.lotId).toBe("lot-alpha");
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
                availableMotorbikeSpaces: 50,
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

            expect(result.sessionId).toBe(mockRes.sessionId);
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

    describe("listLotInsideSessions", () => {
        it("loads Active and Paid sessions and merges them, never Completed or Cancelled", async () => {
            const row = (id: string, status: string) => ({
                sessionId: id,
                plateNumber: id,
                vehicleType: "Motorbike",
                status,
                entryTime: "2026-10-30T01:00:00Z",
                slotCode: null,
            });
            const page = (items: unknown[]) =>
                new Response(
                    JSON.stringify({
                        items,
                        totalCount: items.length,
                        page: 1,
                        pageSize: 100,
                    }),
                    {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    },
                );
            const fetchMock = vi
                .fn()
                .mockResolvedValueOnce(page([row("A", "Active")]))
                .mockResolvedValueOnce(page([row("P", "Paid")]));
            vi.stubGlobal("fetch", fetchMock);

            const result = await listLotInsideSessions("lot-1");

            expect(result.items.map((i) => i.status)).toEqual([
                "Active",
                "Paid",
            ]);
            expect(result.totalCount).toBe(2);
            const urls = fetchMock.mock.calls.map((c) => String(c[0]));
            expect(urls).toHaveLength(2);
            expect(urls[0]).toContain("status=Active");
            expect(urls[1]).toContain("status=Paid");
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

            const fetchMock = vi
                .fn()
                .mockResolvedValueOnce(
                    new Response(JSON.stringify(mockRes), {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    }),
                )
                .mockResolvedValueOnce(
                    new Response(
                        JSON.stringify({
                            sessionId: mockRes.sessionId,
                            totalFee: 30000,
                            depositAmount: 0,
                            amountDue: 30000,
                        }),
                        {
                            status: 200,
                            headers: { "Content-Type": "application/json" },
                        },
                    ),
                );
            vi.stubGlobal("fetch", fetchMock);

            const result = await findSessionByQr(qrToken);
            expect(result.sessionId).toBe(mockRes.sessionId);
            expect(result.plateNumber).toBe(mockRes.plateNumber);
            expect(result.amountDueVnd).toBe(30000);
            expect(fetchMock).toHaveBeenCalledTimes(2);

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

            // The server requires the plate and the session QR (CompleteParkingSessionRequest).
            const result = await completeParkingSession(sessionId, {
                plateNumber: "51B-999.88",
                qrToken: "qr-token-123",
            });

            expect(result).toEqual(mockRes);
            expect(fetchMock).toHaveBeenCalledTimes(1);

            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/parking/sessions/${sessionId}/complete`,
            );
            expect(init?.method).toBe("POST");
            expect(JSON.parse(init?.body as string)).toEqual({
                plateNumber: "51B-999.88",
                qrToken: "qr-token-123",
            });
        });
    });

    describe("payParkingSessionCash", () => {
        it("posts the expected amount with a GUID X-Idempotency-Key", async () => {
            const sessionId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
            const mockRes = {
                transactionId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
                sessionId,
                amountPaid: 15000,
                isReplayed: false,
            };
            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockRes), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const result = await payParkingSessionCash(sessionId, 15000);

            expect(result).toEqual(mockRes);
            const [url, init] = fetchMock.mock.calls[0];
            expect(String(url)).toBe(
                `http://localhost:5000/api/payment/parking-sessions/${sessionId}/pay/cash`,
            );
            expect(init?.method).toBe("POST");
            expect(JSON.parse(init?.body as string)).toEqual({
                expectedAmountDue: 15000,
            });
            const headers = new Headers(init?.headers as HeadersInit);
            expect(headers.get("X-Idempotency-Key")).toMatch(
                /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
            );
            expect(headers.get("Idempotency-Key")).toBeNull();
        });

        it("uses a fresh key for every collection", async () => {
            const fetchMock = vi.fn().mockImplementation(
                async () =>
                    new Response("{}", {
                        status: 200,
                        headers: { "Content-Type": "application/json" },
                    }),
            );
            vi.stubGlobal("fetch", fetchMock);

            await payParkingSessionCash("s1", 1000);
            await payParkingSessionCash("s1", 1000);

            const key = (n: number) =>
                new Headers(
                    fetchMock.mock.calls[n][1]?.headers as HeadersInit,
                ).get("X-Idempotency-Key");
            expect(key(0)).not.toEqual(key(1));
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

        it("handles walk-in full even when status is 409 (parking.walk_in.full)", () => {
            const err = new ApiError(
                409,
                { errorCode: "parking.walk_in.full" },
                undefined,
            );
            expect(describeStaffError(err)).toBe(
                "Bãi đỗ xe đã hết chỗ trống cho phương tiện này.",
            );
        });

        it("handles motorbike zone full even when status is 409 (parking.motorbike_zone.full)", () => {
            const err = new ApiError(
                409,
                { errorCode: "parking.motorbike_zone.full" },
                undefined,
            );
            expect(describeStaffError(err)).toBe(
                "Bãi đỗ xe đã hết chỗ trống cho phương tiện này.",
            );
        });

        it("handles reserved car at full lot even when status is 409 (parking.session.reserved_no_slot)", () => {
            const err = new ApiError(
                409,
                { errorCode: "parking.session.reserved_no_slot", detail: "Không còn chỗ phù hợp cho xe đã đặt chỗ. Nhân viên xếp khu khác hoặc hoàn tiền đặt trước." },
                undefined,
            );
            expect(describeStaffError(err)).toBe(
                "Không còn chỗ phù hợp cho xe đã đặt chỗ. Nhân viên xếp khu khác hoặc hoàn tiền đặt trước.",
            );
        });

        it("keeps already-open fallback message for 409 with unknown errorCode", () => {
            const err = new ApiError(
                409,
                { errorCode: "parking.unknown.conflict" },
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

        it("sends GET to /api/identity/accounts/me for staff profile", async () => {
            const mockProfile = {
                id: "account-123",
                email: "staff.tuan@smartpark.vn",
                fullName: "Nguyễn Văn Tuấn",
                phoneNumber: "0909112233",
                role: "Staff",
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockProfile), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const { getStaffProfile } = await import("./staffApi");
            const res = await getStaffProfile();
            expect(res?.fullName).toBe("Nguyễn Văn Tuấn");
            expect(res?.email).toBe("staff.tuan@smartpark.vn");
        });

        it("fetches assigned shift from /api/staff-shifts", async () => {
            const mockShift = {
                shift: "afternoon",
                shiftName: "Ca 2 - Chiều",
                hours: "14:00 - 22:00",
            };

            const fetchMock = vi.fn().mockResolvedValue(
                new Response(JSON.stringify(mockShift), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            );
            vi.stubGlobal("fetch", fetchMock);

            const { getStaffAssignedShift } = await import("./staffApi");
            const res = await getStaffAssignedShift("lot-1", "acc-1");
            expect(res.shift).toBe("afternoon");
            expect(res.shiftName).toBe("Ca 2 - Chiều");
            expect(res.hours).toBe("14:00 - 22:00");
        });
    });
});
