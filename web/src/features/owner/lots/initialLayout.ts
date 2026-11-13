import {
    createDefaultSlot,
    createDefaultZone,
    generateLayoutGuid,
    validateLayoutInput,
    type LayoutFloorDto,
} from "@/features/owner/layout/ownerLayoutApi";

/*
 * A first layout for a new lot, from what the wizard asks: total capacity, floors and rows per floor.
 * The capacity is shared evenly between floors. When the lot takes both vehicle types, cars get
 * CAR_SHARE of each floor and motorbikes the rest; the owner can change this in the layout editor.
 */

const CAR_SHARE = 0.7;

export type InitialLayoutInput = {
    capacity: number;
    floors: number;
    rowsPerFloor: number;
    acceptsCars: boolean;
    acceptsMotorbikes: boolean;
};

export function buildInitialLayout(
    input: InitialLayoutInput,
): LayoutFloorDto[] {
    const floorCount = Math.max(1, Math.floor(input.floors));
    const rows = Math.max(1, Math.floor(input.rowsPerFloor));
    const capacity = Math.max(0, Math.floor(input.capacity));
    const base = Math.floor(capacity / floorCount);
    const extra = capacity % floorCount;

    return Array.from({ length: floorCount }, (_, index) => {
        const floorNumber = index + 1;
        const floorCapacity = base + (index < extra ? 1 : 0);
        const cars = input.acceptsCars
            ? input.acceptsMotorbikes
                ? Math.ceil(floorCapacity * CAR_SHARE)
                : floorCapacity
            : 0;
        const motorbikes = input.acceptsMotorbikes ? floorCapacity - cars : 0;
        const zones = [];

        if (cars > 0) {
            // Slot codes are unique per lot, so the zone code carries the floor.
            const code = `F${floorNumber}-CAR`;
            const zone = createDefaultZone("Car", code, cars);
            zone.slots = Array.from({ length: cars }, (_, slot) =>
                createDefaultSlot(code, slot + 1, {
                    x: Math.floor(slot / rows),
                    y: slot % rows,
                }),
            );
            zones.push(zone);
        }
        if (motorbikes > 0) {
            const zone = createDefaultZone(
                "Motorbike",
                `F${floorNumber}-MOTO`,
                motorbikes,
            );
            // Placed under the car grid, wide enough to read as an area.
            const carColumns = Math.ceil(cars / rows);
            zone.bounds = {
                x: 0,
                y: rows + 1,
                width: Math.max(carColumns, 4),
                height: 3,
            };
            zones.push(zone);
        }
        return {
            id: generateLayoutGuid(),
            name: `Tầng ${floorNumber}`,
            zones,
        };
    }).filter((floor) => floor.zones.length > 0);
}

/** True when the generated layout passes the same checks the server applies. */
export function isLayoutValid(floors: LayoutFloorDto[]): boolean {
    return validateLayoutInput(floors).valid;
}
