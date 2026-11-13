import type { ParkingLayout } from "./parkingLayoutApi";

export type LayoutZone = ParkingLayout["floors"][number]["zones"][number];
export type LayoutSlot = LayoutZone["slots"][number];

/** What a customer needs to know about a space; the exact internal reason stays with the operator. */
export type SlotGroup = "free" | "taken" | "held" | "blocked";

export function slotGroup(effectiveStatus: string): SlotGroup {
    const status = effectiveStatus.toLowerCase();
    if (status === "available") return "free";
    if (/reserv|held|hold|dispatch/.test(status)) return "held";
    if (/unavailable|maintenance|blocked|disabled/.test(status))
        return "blocked";
    return "taken";
}

export type TwinSlot = {
    id: string;
    code: string;
    col: number;
    row: number;
    group: SlotGroup;
};

export type SlotArrangement = {
    slots: TwinSlot[];
    cols: number;
    rows: number;
};

const naturalCompare = (a: string, b: string) =>
    a.localeCompare(b, undefined, { numeric: true, sensitivity: "base" });

/**
 * Place the spaces of one zone on a grid. Uses the owner's saved positions when every space has one,
 * otherwise lays the spaces out in two facing rows (the demo lots have no saved geometry).
 */
export function arrangeSlots(slots: LayoutSlot[]): SlotArrangement {
    if (slots.length === 0) return { slots: [], cols: 0, rows: 0 };

    const allPositioned = slots.every((slot) => slot.position !== null);
    if (allPositioned) {
        const minX = Math.min(...slots.map((slot) => slot.position!.x));
        const minY = Math.min(...slots.map((slot) => slot.position!.y));
        const placed = slots.map((slot) => ({
            id: slot.id,
            code: slot.slotCode,
            col: Math.round(slot.position!.x - minX),
            row: Math.round(slot.position!.y - minY),
            group: slotGroup(slot.effectiveStatus),
        }));
        const seen = new Set<string>();
        const unique = placed.every((slot) => {
            const key = `${slot.col}:${slot.row}`;
            if (seen.has(key)) return false;
            seen.add(key);
            return true;
        });
        // Two spaces on the same cell would hide one of them: fall back to the automatic grid.
        if (unique) {
            return {
                slots: placed,
                cols: Math.max(...placed.map((slot) => slot.col)) + 1,
                rows: Math.max(...placed.map((slot) => slot.row)) + 1,
            };
        }
    }

    const sorted = [...slots].sort((a, b) =>
        naturalCompare(a.slotCode, b.slotCode),
    );
    const cols = Math.min(10, Math.max(1, Math.ceil(sorted.length / 2)));
    const placed = sorted.map((slot, index) => ({
        id: slot.id,
        code: slot.slotCode,
        col: index % cols,
        row: Math.floor(index / cols),
        group: slotGroup(slot.effectiveStatus),
    }));
    return { slots: placed, cols, rows: Math.ceil(sorted.length / cols) };
}

export type Counts = Record<SlotGroup, number>;

export function countGroups(slots: Array<{ group: SlotGroup }>): Counts {
    const counts: Counts = { free: 0, taken: 0, held: 0, blocked: 0 };
    for (const slot of slots) counts[slot.group] += 1;
    return counts;
}

/** Free and total spaces of a zone: the utilization API wins, the layout snapshot is the fallback. */
export function zoneAvailability(
    zone: LayoutZone,
    utilization: { available: number; usableCapacity: number } | undefined,
) {
    if (utilization) {
        return {
            available: utilization.available,
            capacity: utilization.usableCapacity,
        };
    }
    if (zone.slots.length > 0) {
        const counts = countGroups(
            zone.slots.map((slot) => ({
                group: slotGroup(slot.effectiveStatus),
            })),
        );
        return { available: counts.free, capacity: zone.slots.length };
    }
    return {
        available: Math.max(0, zone.capacity - zone.currentVehicleCount),
        capacity: zone.capacity,
    };
}

/** Isometric projection: x runs to the lower right, y to the lower left, z is height. */
export function project(
    x: number,
    y: number,
    z: number,
    scale: number,
): [number, number] {
    return [(x - y) * scale * 0.866, (x + y) * scale * 0.5 - z * scale];
}
