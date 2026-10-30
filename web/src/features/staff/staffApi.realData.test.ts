import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import {
    cancelParkingSession,
    completeParkingSession,
    findSessionByQr,
    getAssignedLot,
    getParkingSessionFee,
    getStaffLots,
    openParkingSession,
} from "./staffApi";

// Demo 4 (D04.2, D04.3, D04.6): the staff screens show what Parking and Payment return. A failed call
// is an error, never a sample lot, a made-up session, QR or fee, or a completed exit.
const LOT = "10000000-0000-0000-0000-000000000002";
const SESSION = "30000000-0000-0000-0000-000000000001";

function json(body: unknown, status = 200) {
    return new Response(JSON.stringify(body), {
        status,
        headers: { "Content-Type": "application/json" },
    });
}

function problem(status: number, errorCode: string) {
    return new Response(JSON.stringify({ status, errorCode }), {
        status,
        headers: { "Content-Type": "application/problem+json" },
    });
}

function stub(...responses: Array<Response | Error>) {
    const fetchMock = vi.fn();
    for (const r of responses) {
        if (r instanceof Error) fetchMock.mockRejectedValueOnce(r);
        else fetchMock.mockResolvedValueOnce(r);
    }
    vi.stubGlobal("fetch", fetchMock);
    return fetchMock;
}

afterEach(() => {
    vi.unstubAllGlobals();
});

describe("staff lots and free spaces", () => {
    it("no assigned lot is an empty list, not sample lots", async () => {
        stub(json([]));
        await expect(getStaffLots()).resolves.toEqual([]);
    });

    it("an unreachable gateway is an error", async () => {
        stub(new TypeError("Failed to fetch"));
        await expect(getStaffLots()).rejects.toBeInstanceOf(TypeError);
    });

    it("the assigned lot carries the free spaces Parking counts", async () => {
        const fetchMock = stub(
            json([
                {
                    id: LOT,
                    name: "Bãi B",
                    address: "2 Đường Mẫu",
                    type: "Outdoor",
                },
            ]),
            json({
                lotId: LOT,
                totalCapacity: 60,
                occupied: 2,
                available: 58,
                isFull: false,
                zones: [],
            }),
            json({
                lotId: LOT,
                asOfUtc: "2026-10-30T01:00:00Z",
                car: { available: 7 },
                motorbike: null,
            }),
        );

        const lot = await getAssignedLot();

        expect(lot).toMatchObject({
            lotId: LOT,
            lotName: "Bãi B",
            availableMotorbikeSlots: 58,
            motorbikeCapacity: 60,
            availableCarSlots: 7,
        });
        expect(String(fetchMock.mock.calls[1][0])).toContain(
            `/api/parking/lots/${LOT}/motorbike-availability`,
        );
        expect(String(fetchMock.mock.calls[2][0])).toContain(
            `/api/parking/lots/${LOT}/availability`,
        );
    });

    it("counts Parking cannot give are unknown, not invented", async () => {
        stub(
            json([{ id: LOT, name: "Bãi B", address: "", type: "Outdoor" }]),
            problem(404, "parking.motorbike_zone.not_configured"),
            problem(404, "parking.lot_not_found"),
        );

        const lot = await getAssignedLot();

        expect(lot?.availableMotorbikeSlots).toBeNull();
        expect(lot?.availableCarSlots).toBeNull();
    });

    it("a staff account without a lot has no assigned lot", async () => {
        stub(json([]));
        await expect(getAssignedLot()).resolves.toBeNull();
    });
});

describe("sessions", () => {
    it("opening a session reads the zone, slot and free motorbike spaces Parking returns", async () => {
        stub(
            json(
                {
                    sessionId: SESSION,
                    lotId: LOT,
                    plateNumber: "59A-12345",
                    vehicleType: "Motorbike",
                    customerType: "Registered",
                    status: "Active",
                    entryTime: "2026-10-30T01:00:00Z",
                    isAccountLinked: true,
                    reservationId: null,
                    qrToken: "qr-1",
                    zoneId: "z1",
                    zoneCode: "MOTO-B",
                    floorName: "Tầng trệt",
                    availableMotorbikeSpaces: 57,
                    slotCode: null,
                },
                201,
            ),
        );

        const opened = await openParkingSession({
            lotId: LOT,
            plateNumber: "59a-12345",
            vehicleType: "Motorbike",
        });

        expect(opened.availableMotorbikeSlots).toBe(57);
        expect(opened.zoneCode).toBe("MOTO-B");
        expect(opened.floorName).toBe("Tầng trệt");
    });

    it("opening or cancelling without a connection is an error, not a made-up session", async () => {
        stub(new TypeError("Failed to fetch"));
        await expect(
            openParkingSession({
                lotId: LOT,
                plateNumber: "59A-12345",
                vehicleType: "Motorbike",
            }),
        ).rejects.toBeInstanceOf(TypeError);

        stub(new TypeError("Failed to fetch"));
        await expect(
            cancelParkingSession(SESSION, "Nhập nhầm"),
        ).rejects.toBeInstanceOf(TypeError);
    });

    it("a missing completion route is an error, not a completed exit", async () => {
        stub(problem(404, "not_found"));
        await expect(
            completeParkingSession(SESSION, { paymentMethod: "Cash" }),
        ).rejects.toBeInstanceOf(ApiError);
    });
});

describe("fees", () => {
    it("the fee of a scanned session comes from Payment", async () => {
        const fetchMock = stub(
            json({
                sessionId: SESSION,
                lotId: LOT,
                plateNumber: "59A-12345",
                vehicleType: "Motorbike",
                customerType: "Guest",
                status: "Active",
                entryTime: "2026-10-30T01:00:00Z",
            }),
            json({
                sessionId: SESSION,
                totalFee: 6000,
                depositAmount: 0,
                amountDue: 6000,
            }),
        );

        const scanned = await findSessionByQr("qr-1");

        expect(scanned.totalFeeVnd).toBe(6000);
        expect(scanned.amountDueVnd).toBe(6000);
        expect(String(fetchMock.mock.calls[1][0])).toContain(
            `/api/payment/parking-sessions/${SESSION}/fee`,
        );
    });

    it("a fee Payment cannot give is unknown, not computed in the browser", async () => {
        stub(
            json({
                sessionId: SESSION,
                lotId: LOT,
                plateNumber: "59A-12345",
                vehicleType: "Motorbike",
                customerType: "Guest",
                status: "Active",
                entryTime: "2026-10-30T01:00:00Z",
            }),
            problem(503, "payment.parking_service_unavailable"),
        );

        const scanned = await findSessionByQr("qr-1");

        expect(scanned.amountDueVnd).toBeUndefined();
        expect(scanned.totalFeeVnd).toBeUndefined();
    });

    it("an unreachable gateway while scanning is an error, not a sample session", async () => {
        stub(new TypeError("Failed to fetch"));
        await expect(findSessionByQr("qr-1")).rejects.toBeInstanceOf(TypeError);
    });

    it("reads the fee preview route", async () => {
        stub(
            json({
                sessionId: SESSION,
                totalFee: 30000,
                depositAmount: 0,
                amountDue: 30000,
            }),
        );
        await expect(getParkingSessionFee(SESSION)).resolves.toMatchObject({
            amountDue: 30000,
        });
    });
});
