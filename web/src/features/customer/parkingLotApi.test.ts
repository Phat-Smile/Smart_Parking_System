import { afterEach, describe, expect, it, vi } from "vitest";
import {
    describeLotAvailability,
    describeLotPricing,
    searchNearbyParkingLots,
} from "./parkingLotApi";

// Demo 3 step D03.1 and Demo 2 steps D02.8, D02.9: search shows approved lots near the destination,
// and the detail shows the lot's prices and free spaces as Parking returns them.
afterEach(() => {
    vi.unstubAllGlobals();
});

describe("searchNearbyParkingLots", () => {
    it("asks Parking for approved lots around the point", async () => {
        const lots = [
            {
                id: "10000000-0000-0000-0000-000000000001",
                name: "Bãi Lê Lợi",
                address: "1 Lê Lợi",
                latitude: 10.7765,
                longitude: 106.701,
                type: "Outdoor",
                distanceKm: 0.3,
            },
        ];
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify(lots), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await searchNearbyParkingLots(10.775, 106.703, 5);

        expect(result).toEqual(lots);
        const url = new URL(String(fetchMock.mock.calls[0][0]));
        expect(url.pathname).toBe("/api/parking/lots/nearby");
        expect(url.searchParams.get("latitude")).toBe("10.775");
        expect(url.searchParams.get("longitude")).toBe("106.703");
        expect(url.searchParams.get("radiusKm")).toBe("5");
    });

    it("a failed search is an error, not example lots", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn().mockResolvedValue(new Response("", { status: 400 })),
        );
        await expect(searchNearbyParkingLots(10, 106, 5)).rejects.toThrow();
    });
});

describe("lot detail texts", () => {
    it("lists each vehicle type's current price", () => {
        expect(
            describeLotPricing([
                {
                    vehicleType: "CAR",
                    billingMode: "Block",
                    blockMinutes: 60,
                    price: 30000,
                    overnightFee: 0,
                },
                {
                    vehicleType: "MOTORBIKE",
                    billingMode: "PerEntry",
                    blockMinutes: null,
                    price: 5000,
                    overnightFee: 0,
                },
            ]),
        ).toBe("Car 30.000 ₫/60 min · Motorbike 5.000 ₫/visit");
    });

    it("says there is no price when the owner has not set one", () => {
        expect(describeLotPricing([])).toBeNull();
        expect(describeLotPricing(undefined)).toBeNull();
    });

    it("lists free car slots and motorbike spaces", () => {
        expect(
            describeLotAvailability({
                car: { available: 7 },
                motorbike: { capacity: 40, available: 30 },
            }),
        ).toBe("7 cars · 30 motorbikes");
        expect(
            describeLotAvailability({ car: { available: 3 }, motorbike: null }),
        ).toBe("3 cars");
        expect(describeLotAvailability(null)).toBeNull();
    });
});
