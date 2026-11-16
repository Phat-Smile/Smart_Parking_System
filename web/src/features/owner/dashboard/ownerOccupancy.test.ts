import { describe, expect, it } from "vitest";
import {
    countLot,
    mergeFloors,
    type PublicLayout,
    type PublicUtilization,
} from "./ownerOccupancy";
import {
    loadAggregatedOwnerStats,
    getLotStatisticsApi,
} from "./ownerStatisticsApi";
import { vi } from "vitest";

const layout: PublicLayout = {
    floors: [
        {
            id: "floor-1",
            name: "Tầng 1",
            zones: [
                {
                    id: "zone-car-1",
                    capacity: 10,
                    slots: [
                        { effectiveStatus: "Available" },
                        { effectiveStatus: "Available" },
                        { effectiveStatus: "Reserved" },
                        { effectiveStatus: "Reserved" },
                        { effectiveStatus: "Occupied" },
                    ],
                },
                {
                    id: "zone-moto-1",
                    capacity: 20,
                    slots: [],
                },
            ],
        },
        {
            id: "floor-2",
            name: "Tầng 2",
            zones: [
                {
                    id: "zone-car-2",
                    capacity: 15,
                    slots: [],
                },
            ],
        },
    ],
};

const usage: PublicUtilization = {
    zones: [
        {
            zoneId: "zone-car-1",
            usableCapacity: 10,
            inUse: 5,
            available: 5,
        },
        {
            zoneId: "zone-moto-1",
            usableCapacity: 18, // 2 under maintenance
            inUse: 8,
            available: 10,
        },
        {
            zoneId: "zone-car-2",
            usableCapacity: 15,
            inUse: 3,
            available: 12,
        },
    ],
};

describe("Owner Dashboard Space Occupancy & Statistics", () => {
    it("folds layout and zone utilization into floor counts correctly", () => {
        const floors = countLot(layout, usage);

        expect(floors).toHaveLength(2);

        // Floor 1: zone-car-1 (10) + zone-moto-1 (20) = 30 total
        const f1 = floors[0];
        expect(f1.name).toBe("Tầng 1");
        expect(f1.total).toBe(30);
        expect(f1.available).toBe(15);
        expect(f1.maintenance).toBe(2);
        expect(f1.reserved).toBe(2);
        expect(f1.occupied).toBe(11); // inUse: 5 (car) + 8 (moto) = 13. 13 - 2 reserved = 11

        // Floor 2
        const f2 = floors[1];
        expect(f2.name).toBe("Tầng 2");
        expect(f2.total).toBe(15);
        expect(f2.available).toBe(12);
        expect(f2.occupied).toBe(3);
    });

    it("merges floors from multiple lots", () => {
        const lot1Floors = countLot(layout, usage);
        const lot2Floors = countLot(layout, usage);

        const merged = mergeFloors([lot1Floors, lot2Floors]);

        expect(merged.total).toBe(90);
        expect(merged.available).toBe(54);
        expect(merged.floors).toHaveLength(2);
        expect(merged.floors[0].total).toBe(60);
    });

    it("aggregates dashboard statistics with fallback to empty state when no activity", async () => {
        // Mock fetch returning 404 for statistics
        const originalFetch = globalThis.fetch;
        globalThis.fetch = vi.fn().mockImplementation((url: string) => {
            if (url.includes("/statistics")) {
                return Promise.resolve(new Response(null, { status: 404 }));
            }
            if (url.includes("/layout")) {
                return Promise.resolve(new Response(JSON.stringify(layout), { status: 200 }));
            }
            if (url.includes("/zone-utilization")) {
                return Promise.resolve(new Response(JSON.stringify(usage), { status: 200 }));
            }
            return Promise.reject(new Error("Unknown route"));
        });

        try {
            const stats = await loadAggregatedOwnerStats(["lot-uuid-1"]);

            expect(stats.hasRealActivity).toBe(false);
            expect(stats.totalRevenue).toBe(0);
            expect(stats.todaySessionsCount).toBe(0);
            expect(stats.occupancy.total).toBe(45);
            expect(stats.occupancy.available).toBe(27);
            expect(stats.recentSessions).toHaveLength(0);
        } finally {
            globalThis.fetch = originalFetch;
        }
    });

    it("aggregates real statistics when endpoint returns live session data", async () => {
        const mockStats = {
            lotId: "lot-uuid-1",
            totalRevenue: 450000,
            netRevenue: 400000,
            todaySessionsCount: 15,
            activeReservationsCount: 4,
            recentSessions: [
                {
                    sessionId: "01931b67-e984-7a1b-bc54-68f44d5c8001",
                    plateNumber: "51K-123.45",
                    vehicleType: "Car",
                    slotCode: "A-01",
                    entryTime: "08:15",
                    durationMinutes: 45,
                    interimFee: 30000,
                    status: "Active",
                },
            ],
            hourlyTraffic: [
                { time: "08:00", inCount: 5, outCount: 1 },
                { time: "09:00", inCount: 10, outCount: 4 },
            ],
        };

        const originalFetch = globalThis.fetch;
        globalThis.fetch = vi.fn().mockImplementation((url: string) => {
            if (url.includes("/statistics")) {
                return Promise.resolve(new Response(JSON.stringify(mockStats), { status: 200 }));
            }
            if (url.includes("/layout")) {
                return Promise.resolve(new Response(JSON.stringify(layout), { status: 200 }));
            }
            if (url.includes("/zone-utilization")) {
                return Promise.resolve(new Response(JSON.stringify(usage), { status: 200 }));
            }
            return Promise.reject(new Error("Unknown route"));
        });

        try {
            const stats = await loadAggregatedOwnerStats(["lot-uuid-1"]);

            expect(stats.hasRealActivity).toBe(true);
            expect(stats.totalRevenue).toBe(450000);
            expect(stats.netRevenue).toBe(400000);
            expect(stats.todaySessionsCount).toBe(15);
            expect(stats.activeReservationsCount).toBe(4);
            expect(stats.recentSessions).toHaveLength(1);
            expect(stats.recentSessions[0].plateNumber).toBe("51K-123.45");
            expect(stats.hourlyTraffic).toHaveLength(2);
        } finally {
            globalThis.fetch = originalFetch;
        }
    });
});
