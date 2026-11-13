import { describe, expect, it } from "vitest";
import { buildInitialLayout, isLayoutValid } from "./initialLayout";

const input = {
    capacity: 100,
    floors: 3,
    rowsPerFloor: 4,
    acceptsCars: true,
    acceptsMotorbikes: true,
};

describe("buildInitialLayout", () => {
    it("shares the capacity between floors and passes the server checks", () => {
        const floors = buildInitialLayout(input);
        expect(floors).toHaveLength(3);
        const total = floors
            .flatMap((floor) => floor.zones)
            .reduce((sum, zone) => sum + zone.capacity, 0);
        expect(total).toBe(100);
        expect(isLayoutValid(floors)).toBe(true);
    });

    it("gives cars one slot each and motorbikes no slots", () => {
        const [floor] = buildInitialLayout(input);
        const car = floor.zones.find((zone) => zone.vehicleGroup === "Car")!;
        const moto = floor.zones.find(
            (zone) => zone.vehicleGroup === "Motorbike",
        )!;
        expect(car.slots).toHaveLength(car.capacity);
        expect(moto.slots).toHaveLength(0);
        expect(moto.bounds).toBeDefined();
    });

    it("uses one zone type when the lot takes one vehicle type", () => {
        const floors = buildInitialLayout({
            ...input,
            acceptsMotorbikes: false,
        });
        expect(
            floors.every((floor) =>
                floor.zones.every((zone) => zone.vehicleGroup === "Car"),
            ),
        ).toBe(true);
        expect(isLayoutValid(floors)).toBe(true);
    });

    it("has unique slot codes across floors", () => {
        const codes = buildInitialLayout(input)
            .flatMap((floor) => floor.zones)
            .flatMap((zone) => zone.slots.map((slot) => slot.slotCode));
        expect(new Set(codes).size).toBe(codes.length);
    });
});
