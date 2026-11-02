import { afterEach, describe, expect, it, vi } from "vitest";
import {
    createReservationHold,
    confirmReservation,
    getMyReservations,
    cancelReservation,
} from "./bookingApi";

afterEach(() => {
    vi.unstubAllGlobals();
});

describe("bookingApi", () => {
    it("createReservationHold calls POST /api/booking/holds with JSON body", async () => {
        const mockResponse = {
            reservationId: "res-1234",
            lotId: "lot-5678",
            vehicleId: "veh-999",
            plateNumber: "51H-999.88",
            vehicleGroup: "Car",
            reservationType: "Scheduled",
            status: "TemporaryHold",
            appointmentTime: "2026-10-30T14:30:00Z",
            durationMinutes: 120,
            tempHoldExpiresAt: "2026-10-30T14:45:00Z",
            windowStartAt: "2026-10-30T14:15:00Z",
            windowEndAt: "2026-10-30T16:30:00Z",
            prepaidAmount: 50000,
            priceSnapshot: null,
            createdAt: "2026-10-30T14:30:00Z",
        };

        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify(mockResponse), {
                status: 201,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await createReservationHold({
            lotId: "lot-5678",
            vehicleId: "veh-999",
            plateNumber: "51H-999.88",
            appointmentTime: "2026-10-30T14:30:00Z",
            durationMinutes: 120,
            prepaidAmount: 50000,
        });

        expect(result).toEqual(mockResponse);
        const [url, options] = fetchMock.mock.calls[0];
        expect(String(url)).toContain("/api/booking/holds");
        expect(options.method).toBe("POST");
        const body = JSON.parse(options.body as string);
        expect(body.lotId).toBe("lot-5678");
        expect(body.plateNumber).toBe("51H-999.88");
        expect(body.prepaidAmount).toBe(50000);
    });

    it("confirmReservation calls POST /api/booking/reservations/{id}/confirm", async () => {
        const mockConfirm = {
            reservationId: "res-1234",
            status: "Confirmed",
            confirmedAt: "2026-10-30T14:32:00Z",
            qrCodeToken: "RES-QR-RES-1234",
            prepaidAmount: 50000,
        };

        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify(mockConfirm), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await confirmReservation("res-1234", {
            paymentMethod: "Wallet",
            depositAmount: 50000,
        });

        expect(result).toEqual(mockConfirm);
        const [url, options] = fetchMock.mock.calls[0];
        expect(String(url)).toContain(
            "/api/booking/reservations/res-1234/confirm",
        );
        expect(options.method).toBe("POST");
        const body = JSON.parse(options.body as string);
        expect(body.depositAmount).toBe(50000);
    });

    it("getMyReservations calls GET /api/booking/reservations/me with status query", async () => {
        const mockList = [
            {
                reservationId: "res-1",
                lotId: "lot-1",
                vehicleId: "v-1",
                plateNumber: "51H-111.11",
                vehicleGroup: "Car",
                reservationType: "Scheduled",
                status: "Confirmed",
                appointmentTime: "2026-10-30T15:00:00Z",
                durationMinutes: 60,
                tempHoldExpiresAt: null,
                windowStartAt: "2026-10-30T14:45:00Z",
                windowEndAt: "2026-10-30T16:00:00Z",
                prepaidAmount: 50000,
                qrCodeToken: "RES-QR-001",
                priceSnapshot: null,
                createdAt: "2026-10-30T14:00:00Z",
                confirmedAt: "2026-10-30T14:02:00Z",
            },
        ];

        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify(mockList), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await getMyReservations("Confirmed");

        expect(result).toEqual(mockList);
        const [url] = fetchMock.mock.calls[0];
        expect(String(url)).toContain(
            "/api/booking/reservations/me?status=Confirmed",
        );
    });

    it("cancelReservation calls POST /api/booking/reservations/{id}/cancel", async () => {
        const mockCancel = {
            reservationId: "res-1234",
            status: "Cancelled",
            refundAmount: 50000,
            message: "Hoàn cọc thành công",
        };

        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify(mockCancel), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await cancelReservation("res-1234", {
            reason: "Đổi lịch",
        });

        expect(result).toEqual(mockCancel);
        const [url, options] = fetchMock.mock.calls[0];
        expect(String(url)).toContain(
            "/api/booking/reservations/res-1234/cancel",
        );
        expect(options.method).toBe("POST");
        const body = JSON.parse(options.body as string);
        expect(body.reason).toBe("Đổi lịch");
    });
});
