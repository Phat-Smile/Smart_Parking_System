import { describe, it, expect, vi, beforeEach } from "vitest";
import {
    registerParkingLotApi,
    configureReservationRatioPolicyApi,
    getReservationRatioPolicyApi,
} from "./ownerApi";

describe("ownerApi - ParkingLot and Policies", () => {
    beforeEach(() => {
        vi.restoreAllMocks();
    });

    it("registers parking lot with features payload", async () => {
        const mockResponse = {
            id: "lot-guid-123",
            ownerUserId: "owner-guid-456",
            name: "Bãi xe ParkWise EV",
            address: "123 Lê Lợi, Q1, TP.HCM",
            latitude: 10.7745,
            longitude: 106.7035,
            type: "Outdoor",
            approvalStatus: "Pending",
            features: {
                supportsElectricVehicles: true,
                hasEvCharging: true,
                hasCctv: true,
                isCovered: false,
                supportsLargeVehicles: false,
            },
        };

        global.fetch = vi.fn().mockResolvedValue({
            ok: true,
            status: 201,
            headers: new Headers({ "content-type": "application/json" }),
            json: () => Promise.resolve(mockResponse),
        });

        const result = await registerParkingLotApi({
            name: "Bãi xe ParkWise EV",
            address: "123 Lê Lợi, Q1, TP.HCM",
            latitude: 10.7745,
            longitude: 106.7035,
            type: "Outdoor",
            features: {
                supportsElectricVehicles: true,
                hasEvCharging: true,
                hasCctv: true,
                isCovered: false,
                supportsLargeVehicles: false,
            },
        });

        expect(result.id).toBe("lot-guid-123");
        expect(result.approvalStatus).toBe("Pending");
        const [callUrl, callOptions] = vi.mocked(global.fetch).mock.calls[0];
        expect(String(callUrl)).toContain("/api/parking/lots");
        expect(callOptions?.method).toBe("POST");
        expect(callOptions?.body).toContain('"hasEvCharging":true');
    });

    it("configures reservation ratio with both number and object parameter", async () => {
        const mockPolicy = {
            lotId: "lot-1",
            reservationRatioPercent: 35,
            isConfigured: true,
            updatedAtUtc: "2026-11-09T10:00:00Z",
        };

        global.fetch = vi.fn().mockResolvedValue({
            ok: true,
            status: 200,
            headers: new Headers({ "content-type": "application/json" }),
            json: () => Promise.resolve(mockPolicy),
        });

        // Test with number
        const resNum = await configureReservationRatioPolicyApi("lot-1", 35);
        expect(resNum.reservationRatioPercent).toBe(35);

        // Test with object
        const resObj = await configureReservationRatioPolicyApi("lot-1", {
            reservationRatioPercent: 40,
        });
        expect(resObj.reservationRatioPercent).toBe(35);
    });

    it("gets reservation ratio policy", async () => {
        const mockPolicy = {
            lotId: "lot-1",
            reservationRatioPercent: 30,
            isConfigured: true,
            updatedAtUtc: "2026-11-09T10:00:00Z",
        };

        global.fetch = vi.fn().mockResolvedValue({
            ok: true,
            status: 200,
            headers: new Headers({ "content-type": "application/json" }),
            json: () => Promise.resolve(mockPolicy),
        });

        const result = await getReservationRatioPolicyApi("lot-1");
        expect(result.reservationRatioPercent).toBe(30);
    });
});
