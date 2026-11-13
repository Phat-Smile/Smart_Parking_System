import { Fragment } from "react";
import {
    arrangeSlots,
    countGroups,
    project,
    zoneAvailability,
    type LayoutZone,
    type SlotGroup,
} from "../layoutTwin";
import type { ParkingLayout, ZoneUtilization } from "../parkingLayoutApi";

type Copy = (english: string, vietnamese: string) => string;
type Floor = ParkingLayout["floors"][number];

const SCALE = 34;
const SLOT_DEPTH = 1.7;
const ZONE_GAP = 1.1;
const MARGIN = 0.6;
const SLAB = 0.3;
const MAX_TILES = 30;

type Block = {
    zone: LayoutZone;
    y: number;
    cols: number;
    rows: number;
    cellDepth: number;
    cells: Array<{ id: string; col: number; row: number; group: SlotGroup }>;
    /** Spaces without individual slots (motorbike zones) are drawn as small tiles. */
    tiled: boolean;
};

function buildBlocks(floor: Floor, usage: ZoneUtilization): Block[] {
    let y = MARGIN;
    return floor.zones.map((zone) => {
        const arranged = arrangeSlots(zone.slots);
        let block: Block;
        if (arranged.slots.length > 0) {
            block = {
                zone,
                y,
                cols: arranged.cols,
                rows: arranged.rows,
                cellDepth: SLOT_DEPTH,
                cells: arranged.slots.map((slot) => ({
                    id: slot.id,
                    col: slot.col,
                    row: slot.row,
                    group: slot.group,
                })),
                tiled: false,
            };
        } else {
            const { available, capacity } = zoneAvailability(
                zone,
                usage.zones.find((item) => item.zoneId === zone.id),
            );
            const tiles = Math.min(MAX_TILES, Math.max(1, capacity));
            const free =
                capacity > 0 ? Math.round((available / capacity) * tiles) : 0;
            const cols = Math.min(10, Math.ceil(tiles / 2));
            block = {
                zone,
                y,
                cols,
                rows: Math.ceil(tiles / cols),
                cellDepth: 1,
                cells: Array.from({ length: tiles }, (_, index) => ({
                    id: `${zone.id}-${index}`,
                    col: index % cols,
                    row: Math.floor(index / cols),
                    group: index < free ? "free" : "taken",
                })),
                tiled: true,
            };
        }
        y += block.rows * block.cellDepth + ZONE_GAP;
        return block;
    });
}

type Poly = { points: string; className: string; order: number; key: string };

function box(
    keyBase: string,
    order: number,
    x0: number,
    y0: number,
    z0: number,
    x1: number,
    y1: number,
    z1: number,
    classes: { top: string; left: string; right: string },
    origin: [number, number],
): Poly[] {
    const p = (x: number, y: number, z: number) => {
        const [sx, sy] = project(x, y, z, SCALE);
        return `${(sx + origin[0]).toFixed(1)},${(sy + origin[1]).toFixed(1)}`;
    };
    return [
        {
            key: `${keyBase}-l`,
            className: classes.left,
            order,
            points: [
                p(x0, y1, z1),
                p(x1, y1, z1),
                p(x1, y1, z0),
                p(x0, y1, z0),
            ].join(" "),
        },
        {
            key: `${keyBase}-r`,
            className: classes.right,
            order,
            points: [
                p(x1, y0, z1),
                p(x1, y1, z1),
                p(x1, y1, z0),
                p(x1, y0, z0),
            ].join(" "),
        },
        {
            key: `${keyBase}-t`,
            className: classes.top,
            order,
            points: [
                p(x0, y0, z1),
                p(x1, y0, z1),
                p(x1, y1, z1),
                p(x0, y1, z1),
            ].join(" "),
        },
    ];
}

const SLAB_CLASSES = {
    top: "lot-twin-slab-top",
    left: "lot-twin-slab-left",
    right: "lot-twin-slab-right",
};
const CAR_CLASSES = [0, 1, 2].map((tone) => ({
    top: `lot-twin-car-top-${tone}`,
    left: `lot-twin-car-left-${tone}`,
    right: `lot-twin-car-right-${tone}`,
}));

export function ParkingTwin({
    floor,
    usage,
    copy,
}: {
    floor: Floor;
    usage: ZoneUtilization;
    copy: Copy;
}) {
    const blocks = buildBlocks(floor, usage);
    const width = Math.max(...blocks.map((b) => b.cols), 1) + MARGIN * 2;
    const depth =
        blocks.reduce(
            (max, b) => Math.max(max, b.y + b.rows * b.cellDepth),
            0,
        ) + MARGIN;

    // Screen bounds of the slab, then everything is drawn relative to that origin.
    const corners = [
        project(0, 0, -SLAB, SCALE),
        project(width, 0, -SLAB, SCALE),
        project(0, depth, -SLAB, SCALE),
        project(width, depth, -SLAB, SCALE),
        project(0, 0, 1.5, SCALE),
        project(width, 0, 1.5, SCALE),
    ];
    const minX = Math.min(...corners.map((c) => c[0]));
    const maxX = Math.max(...corners.map((c) => c[0]));
    const minY = Math.min(...corners.map((c) => c[1]));
    const maxY = Math.max(...corners.map((c) => c[1]));
    const pad = 14;
    const origin: [number, number] = [pad - minX, pad - minY];
    const viewWidth = maxX - minX + pad * 2;
    const viewHeight = maxY - minY + pad * 2;

    const polys: Poly[] = [
        ...box("slab", 0, 0, 0, -SLAB, width, depth, 0, SLAB_CLASSES, origin),
    ];
    const tiles: Array<{
        key: string;
        points: string;
        className: string;
        order: number;
    }> = [];
    let carIndex = 0;
    for (const block of blocks) {
        for (const cell of block.cells) {
            const x0 = MARGIN + cell.col + 0.06;
            const x1 = MARGIN + cell.col + 0.94;
            const y0 = block.y + cell.row * block.cellDepth + 0.06;
            const y1 = block.y + (cell.row + 1) * block.cellDepth - 0.06;
            const order = (x1 + y1) * 10;
            const corner = (x: number, y: number) => {
                const [sx, sy] = project(x, y, 0.01, SCALE);
                return `${(sx + origin[0]).toFixed(1)},${(sy + origin[1]).toFixed(1)}`;
            };
            tiles.push({
                key: `tile-${cell.id}`,
                className: `lot-twin-tile lot-twin-tile-${cell.group}`,
                order,
                points: [
                    corner(x0, y0),
                    corner(x1, y0),
                    corner(x1, y1),
                    corner(x0, y1),
                ].join(" "),
            });
            if (cell.group === "taken") {
                const tone = CAR_CLASSES[carIndex++ % CAR_CLASSES.length];
                if (block.tiled) {
                    polys.push(
                        ...box(
                            `car-${cell.id}`,
                            order + 1,
                            x0 + 0.15,
                            y0 + 0.15,
                            0,
                            x1 - 0.15,
                            y1 - 0.15,
                            0.4,
                            tone,
                            origin,
                        ),
                    );
                } else {
                    polys.push(
                        ...box(
                            `car-${cell.id}`,
                            order + 1,
                            x0 + 0.14,
                            y0 + 0.26,
                            0,
                            x1 - 0.14,
                            y1 - 0.26,
                            0.5,
                            tone,
                            origin,
                        ),
                        ...box(
                            `cab-${cell.id}`,
                            order + 2,
                            x0 + 0.24,
                            y0 + 0.55,
                            0.5,
                            x1 - 0.24,
                            y1 - 0.6,
                            0.85,
                            tone,
                            origin,
                        ),
                    );
                }
            }
        }
    }
    const drawn = [...tiles, ...polys].sort((a, b) => a.order - b.order);

    const summary = blocks
        .map((block) => {
            const { available, capacity } = zoneAvailability(
                block.zone,
                usage.zones.find((item) => item.zoneId === block.zone.id),
            );
            return `${block.zone.code} ${available}/${capacity}`;
        })
        .join(", ");

    return (
        <div className="lot-twin">
            <svg
                className="lot-twin-svg"
                viewBox={`0 0 ${viewWidth.toFixed(0)} ${viewHeight.toFixed(0)}`}
                role="img"
                aria-label={`${floor.name}: ${summary}`}
            >
                {drawn.map((item) => (
                    <polygon
                        key={item.key}
                        points={item.points}
                        className={item.className}
                    />
                ))}
            </svg>
            <ul className="lot-twin-zones">
                {blocks.map((block) => {
                    const { available, capacity } = zoneAvailability(
                        block.zone,
                        usage.zones.find(
                            (item) => item.zoneId === block.zone.id,
                        ),
                    );
                    const counts = countGroups(block.cells);
                    return (
                        <li key={block.zone.id}>
                            <strong>{block.zone.code}</strong>
                            <span>
                                {block.zone.vehicleGroup === "Motorbike"
                                    ? copy("Motorbike", "Xe máy")
                                    : block.zone.vehicleGroup === "Car"
                                      ? copy("Car", "Ô tô")
                                      : block.zone.vehicleGroup}
                            </span>
                            <b>
                                {available}/{capacity}
                            </b>
                            <small>
                                {copy("free", "còn trống")}
                                {counts.held > 0 && (
                                    <Fragment>
                                        {", "}
                                        {counts.held} {copy("held", "đang giữ")}
                                    </Fragment>
                                )}
                            </small>
                        </li>
                    );
                })}
            </ul>
        </div>
    );
}
