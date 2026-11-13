import { beforeEach, describe, expect, it, vi } from "vitest";

const api = vi.hoisted(() => ({
    configureDepositPolicyApi: vi.fn(),
    configureOpeningHoursApi: vi.fn(),
    configurePricingApi: vi.fn(),
}));
vi.mock("@/features/owner/api/ownerApi", () => api);
const layoutApi = vi.hoisted(() => ({ saveOwnerParkingLayout: vi.fn() }));
vi.mock("@/features/owner/layout/ownerLayoutApi", async (importOriginal) => ({
    ...(await importOriginal<object>()),
    ...layoutApi,
}));

import { saveInitialLotSetup, weeklyHours } from "./initialLotSetup";

const setup = {
    openAt: "06:00",
    closeAt: "22:00",
    acceptsCars: true,
    acceptsMotorbikes: true,
    carPricePerHour: 30000,
    motorbikePricePerHour: 5000,
    depositVnd: 20000,
    capacity: 100,
    floors: 2,
    rowsPerFloor: 4,
};

beforeEach(() => {
    Object.values({ ...api, ...layoutApi }).forEach((fn) =>
        fn.mockReset().mockResolvedValue({}),
    );
});

describe("weeklyHours", () => {
    it("repeats the times on all seven days", () => {
        const { days } = weeklyHours("06:00", "22:00");
        expect(days).toHaveLength(7);
        expect(days[3]).toMatchObject({
            opensAt: "06:00",
            closesAt: "22:00",
            is24Hours: false,
        });
    });

    it("opens all day when the times are equal or invalid", () => {
        expect(weeklyHours("08:00", "08:00").days[0].is24Hours).toBe(true);
        expect(weeklyHours("", "22:00").days[0].is24Hours).toBe(true);
    });
});

describe("saveInitialLotSetup", () => {
    it("saves hours, both prices and the deposit", async () => {
        expect(await saveInitialLotSetup("lot-1", setup)).toEqual([]);
        expect(api.configureOpeningHoursApi).toHaveBeenCalledOnce();
        expect(layoutApi.saveOwnerParkingLayout).toHaveBeenCalledWith(
            "lot-1",
            expect.objectContaining({ expectedVersion: 0 }),
        );
        expect(api.configurePricingApi).toHaveBeenCalledWith(
            "lot-1",
            "CAR",
            expect.objectContaining({ price: 30000, blockMinutes: 60 }),
        );
        expect(api.configurePricingApi).toHaveBeenCalledWith(
            "lot-1",
            "MOTORBIKE",
            expect.objectContaining({ price: 5000 }),
        );
        expect(api.configureDepositPolicyApi).toHaveBeenCalledWith("lot-1", {
            holdDepositVnd: 20000,
        });
    });

    it("skips a vehicle type the lot does not accept", async () => {
        await saveInitialLotSetup("lot-1", {
            ...setup,
            acceptsMotorbikes: false,
        });
        expect(api.configurePricingApi).toHaveBeenCalledTimes(1);
    });

    it("keeps going and names the parts that failed", async () => {
        api.configurePricingApi.mockRejectedValueOnce(new Error("x"));
        expect(await saveInitialLotSetup("lot-1", setup)).toEqual(["giá ô tô"]);
        expect(api.configureDepositPolicyApi).toHaveBeenCalled();
    });
});
