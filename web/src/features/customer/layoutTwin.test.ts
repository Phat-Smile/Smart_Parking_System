import { describe, expect, it } from "vitest";
import {
    arrangeSlots,
    countGroups,
    project,
    slotGroup,
    zoneAvailability,
    type LayoutSlot,
    type LayoutZone,
} from "./layoutTwin";

const slot = (
    code: string,
    effectiveStatus = "Available",
    position: { x: number; y: number } | null = null,
): LayoutSlot => ({
    id: `id-${code}`,
    slotCode: code,
    effectiveStatus,
    position,
});

describe("slotGroup", () => {
    it("reduces the internal statuses to what a customer needs", () => {
        expect(slotGroup("Available")).toBe("free");
        expect(slotGroup("available")).toBe("free");
        expect(slotGroup("Occupied")).toBe("taken");
        expect(slotGroup("NeedsVerification")).toBe("taken");
        expect(slotGroup("Reserved")).toBe("held");
        expect(slotGroup("DispatchHold")).toBe("held");
        expect(slotGroup("Unavailable")).toBe("blocked");
        expect(slotGroup("Maintenance")).toBe("blocked");
    });
});

describe("arrangeSlots", () => {
    it("returns an empty arrangement without spaces", () => {
        expect(arrangeSlots([])).toEqual({ slots: [], cols: 0, rows: 0 });
    });

    it("lays out spaces without positions in two rows, ordered by code", () => {
        const result = arrangeSlots([
            slot("A-10"),
            slot("A-2"),
            slot("A-1"),
            slot("A-3"),
        ]);
        expect(result.cols).toBe(2);
        expect(result.rows).toBe(2);
        expect(result.slots.map((s) => s.code)).toEqual([
            "A-1",
            "A-2",
            "A-3",
            "A-10",
        ]);
        expect(result.slots.map((s) => [s.col, s.row])).toEqual([
            [0, 0],
            [1, 0],
            [0, 1],
            [1, 1],
        ]);
    });

    it("caps the automatic grid at ten columns", () => {
        const many = Array.from({ length: 30 }, (_, i) => slot(`B-${i + 1}`));
        const result = arrangeSlots(many);
        expect(result.cols).toBe(10);
        expect(result.rows).toBe(3);
    });

    it("uses saved positions, shifted to start at zero", () => {
        const result = arrangeSlots([
            slot("A-01", "Available", { x: 5, y: 7 }),
            slot("A-02", "Occupied", { x: 6, y: 7 }),
            slot("A-03", "Reserved", { x: 5, y: 8 }),
        ]);
        expect(result.cols).toBe(2);
        expect(result.rows).toBe(2);
        expect(result.slots.find((s) => s.code === "A-02")).toMatchObject({
            col: 1,
            row: 0,
            group: "taken",
        });
    });

    it("falls back to the automatic grid when two spaces share a cell", () => {
        const result = arrangeSlots([
            slot("A-01", "Available", { x: 1, y: 1 }),
            slot("A-02", "Available", { x: 1, y: 1 }),
        ]);
        expect(new Set(result.slots.map((s) => `${s.col}:${s.row}`)).size).toBe(
            2,
        );
    });
});

describe("zone counts", () => {
    const zone = (
        slots: LayoutSlot[],
        capacity = 2,
        current = 0,
    ): LayoutZone => ({
        id: "z",
        code: "CAR-A",
        vehicleGroup: "Car",
        capacity,
        currentVehicleCount: current,
        bounds: null,
        polygon: null,
        slots,
    });

    it("counts groups", () => {
        const arranged = arrangeSlots([
            slot("1"),
            slot("2", "Occupied"),
            slot("3", "Reserved"),
        ]);
        expect(countGroups(arranged.slots)).toEqual({
            free: 1,
            taken: 1,
            held: 1,
            blocked: 0,
        });
    });

    it("prefers the utilization API, then the slots, then the capacity", () => {
        expect(
            zoneAvailability(zone([slot("1")]), {
                available: 4,
                usableCapacity: 9,
            }),
        ).toEqual({ available: 4, capacity: 9 });
        expect(
            zoneAvailability(
                zone([slot("1"), slot("2", "Occupied")]),
                undefined,
            ),
        ).toEqual({ available: 1, capacity: 2 });
        expect(zoneAvailability(zone([], 20, 5), undefined)).toEqual({
            available: 15,
            capacity: 20,
        });
        expect(zoneAvailability(zone([], 2, 9), undefined).available).toBe(0);
    });
});

describe("project", () => {
    it("moves right and down with x, left and down with y, up with z", () => {
        const origin = project(0, 0, 0, 10);
        const [rx, ry] = project(1, 0, 0, 10);
        const [lx, ly] = project(0, 1, 0, 10);
        const [, uy] = project(0, 0, 1, 10);
        expect(origin).toEqual([0, 0]);
        expect(rx).toBeGreaterThan(0);
        expect(ry).toBeGreaterThan(0);
        expect(lx).toBeLessThan(0);
        expect(ly).toBeGreaterThan(0);
        expect(uy).toBeLessThan(0);
    });
});
