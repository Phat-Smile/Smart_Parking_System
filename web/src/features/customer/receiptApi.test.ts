import { afterEach, describe, expect, it, vi } from "vitest";
import { getParkingFeeReceiptApi, type ParkingFeeReceipt } from "./receiptApi";

afterEach(() => {
    vi.unstubAllGlobals();
});

describe("getParkingFeeReceiptApi", () => {
    it("requests receipt with session id and auth header", async () => {
        const fakeReceipt: ParkingFeeReceipt = {
            receiptId: "rcpt-123",
            sessionId: "sess-456",
            paymentId: "pay-789",
            parkingLotId: "lot-001",
            parkingLotName: "Bãi xe Bitexco Tower",
            licensePlate: "51H-999.88",
            vehicleType: "Car",
            checkInTimeUtc: "2026-11-10T08:00:00Z",
            checkOutTimeUtc: "2026-11-10T10:30:00Z",
            totalDurationHours: 2.5,
            standardFee: 30000,
            surgeMultiplier: 1.0,
            prepaidDepositOffset: 20000,
            netPaidAmount: 10000,
            paymentMethod: "Wallet",
            paidAtUtc: "2026-11-10T10:31:00Z",
            ledgerTransactionReference: "TX-9999",
        };

        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify(fakeReceipt), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await getParkingFeeReceiptApi("sess-456");

        expect(result).toEqual(fakeReceipt);
        const url = new URL(String(fetchMock.mock.calls[0][0]));
        expect(url.pathname).toBe(
            "/api/payment/parking-sessions/sess-456/receipt",
        );
    });

    it("throws error when response is not ok", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn().mockResolvedValue(new Response("", { status: 404 })),
        );
        await expect(
            getParkingFeeReceiptApi("unknown-session"),
        ).rejects.toThrow(
            "Failed to fetch receipt for session unknown-session",
        );
    });
});
