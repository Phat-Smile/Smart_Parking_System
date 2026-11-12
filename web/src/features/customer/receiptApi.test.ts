import { afterEach, describe, expect, it, vi } from "vitest";
import {
    getParkingFeeReceiptApi,
    adaptBackendReceipt,
    type ParkingFeeReceipt,
} from "./receiptApi";

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

    it("does not send credentials: the Gateway CORS policy has no Access-Control-Allow-Credentials", async () => {
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({}), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        await getParkingFeeReceiptApi("sess-456");

        // credentials "include" makes the browser refuse the response (net::ERR_FAILED) because the Gateway
        // answers without Allow-Credentials; the call authenticates with the Bearer header instead.
        const init = fetchMock.mock.calls[0][1] as RequestInit;
        expect(init.credentials).not.toBe("include");
    });

    it("correctly adapts backend ParkingFeeReceiptResponse contract via adaptBackendReceipt", () => {
        const backendPayload = {
            paymentId: "b3f6e07a-e455-4422-9cb5-cf12e584f22a",
            sessionId: "a1a2a3a4-b1b2-c1c2-d1d2-e1e2e3e4e5e6",
            parkingLotId: "lot-1111",
            plateNumber: "51K-123.45",
            vehicleType: "Car",
            paymentMethod: "Cash",
            totalFee: 35000,
            prepaidAmount: 15000,
            amountPaid: 20000,
            paymentTimeUtc: "2026-11-10T09:45:00Z",
            exitWindowExpiresUtc: "2026-11-10T10:00:00Z",
            status: "Success",
            entryTimeUtc: "2026-11-10T08:00:00Z",
            durationMinutes: 105,
            transactionReference: "VNP-998877",
        };

        const result = adaptBackendReceipt(backendPayload);

        expect(result.receiptId).toBe("b3f6e07a-e455-4422-9cb5-cf12e584f22a");
        expect(result.paymentId).toBe("b3f6e07a-e455-4422-9cb5-cf12e584f22a");
        expect(result.licensePlate).toBe("51K-123.45");
        expect(result.totalDurationHours).toBe(1.8);
        expect(result.standardFee).toBe(35000);
        expect(result.prepaidDepositOffset).toBe(15000);
        expect(result.netPaidAmount).toBe(20000);
        expect(result.ledgerTransactionReference).toBe("VNP-998877");
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
