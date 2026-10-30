import { afterEach, describe, expect, it, vi } from "vitest";
import * as ownerApi from "@/features/owner/api/ownerApi";
import { useOwnerStore } from "@/store/useOwnerStore";
import { useOwnerMapStore } from "./useOwnerMapStore";
import { backendLotId, loadOwnerLots, localLotId } from "./ownerLotsSync";

const LOT = "10000000-0000-0000-0000-00000000000a";

// Demo 2 steps D02.1–D02.5: the owner portal lists the owner's lots as Parking stores them and
// calls Parking with the lot's GUID, never with a screen-only number.
describe("ownerLotsSync", () => {
    afterEach(() => {
        vi.restoreAllMocks();
    });

    it("replaces the design lots with the owner's lots from Parking", async () => {
        vi.spyOn(ownerApi, "getOwnerParkingLotsApi").mockResolvedValue({
            items: [
                {
                    id: LOT,
                    name: "Bãi Lê Lợi",
                    address: "1 Lê Lợi",
                    latitude: 10.77,
                    longitude: 106.7,
                    type: "Outdoor",
                    approvalStatus: "Pending",
                    rejectionReason: null,
                },
            ],
            totalCount: 1,
            page: 1,
            pageSize: 100,
        });

        await loadOwnerLots();

        const lots = useOwnerStore.getState().lots;
        expect(lots).toHaveLength(1);
        expect(lots[0]).toMatchObject({
            id: localLotId(LOT),
            backendId: LOT,
            name: "Bãi Lê Lợi",
            addr: "1 Lê Lợi",
            gps: "10.77, 106.7",
            ap: "pending",
        });
        expect(useOwnerMapStore.getState().overlayLots).toEqual([]);
        expect(backendLotId(lots[0])).toBe(LOT);
    });

    it("a design lot has no backend id, so nothing can be saved for it", () => {
        expect(backendLotId({ id: 1 })).toBeNull();
        expect(backendLotId(undefined)).toBeNull();
    });

    it("a failed load is reported and leaves no design lots on screen", async () => {
        vi.spyOn(ownerApi, "getOwnerParkingLotsApi").mockRejectedValue(
            new TypeError("Failed to fetch"),
        );

        await expect(loadOwnerLots()).rejects.toBeInstanceOf(TypeError);
        expect(useOwnerStore.getState().lots).toEqual([]);
        expect(useOwnerMapStore.getState().overlayLots).toEqual([]);
    });
});
