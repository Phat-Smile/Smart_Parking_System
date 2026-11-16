import { useQuery } from "@tanstack/react-query";
import { getGatewayBaseUrl } from "@/shared/config/env";
import { useOwnerStore } from "@/store/useOwnerStore";

/*
 * Real space counts for the owner dashboard.
 * Queries public layout and zone-utilization endpoints for approved owner lots.
 */

export type PublicLayout = {
    floors: {
        id: string;
        name: string;
        zones: {
            id: string;
            capacity: number;
            slots: { effectiveStatus: string }[];
        }[];
    }[];
};

export type PublicUtilization = {
    zones: {
        zoneId: string;
        available: number;
        usableCapacity: number;
        inUse: number;
    }[];
};

export type OccupancyCounts = {
    total: number;
    available: number;
    reserved: number;
    occupied: number;
    maintenance: number;
};

export type FloorOccupancy = OccupancyCounts & { name: string };

export type OwnerOccupancy = OccupancyCounts & { floors: FloorOccupancy[] };

export const emptyOccupancy = (): OccupancyCounts => ({
    total: 0,
    available: 0,
    reserved: 0,
    occupied: 0,
    maintenance: 0,
});

export async function readPublic<T>(
    lotId: string,
    path: string,
    signal?: AbortSignal,
): Promise<T> {
    const response = await fetch(
        `${getGatewayBaseUrl()}/api/parking/lots/${encodeURIComponent(lotId)}/${path}`,
        { cache: "no-store", signal },
    );
    if (!response.ok) throw new Error(`Parking ${path} unavailable`);
    return (await response.json()) as T;
}

/** Folds one lot's layout and zone usage into per-floor counts. Exported for tests. */
export function countLot(
    layout: PublicLayout,
    usage: PublicUtilization,
): FloorOccupancy[] {
    const byZone = new Map(usage.zones.map((zone) => [zone.zoneId, zone]));
    return layout.floors.map((floor) => {
        const counts: FloorOccupancy = { ...emptyOccupancy(), name: floor.name };
        for (const zone of floor.zones) {
            const use = byZone.get(zone.id);
            const usable = use?.usableCapacity ?? zone.capacity;
            const inUse = use?.inUse ?? 0;
            // Only car zones list slots, so only they can tell a held space from a taken one.
            const reserved = zone.slots.filter(
                (slot) => slot.effectiveStatus === "Reserved",
            ).length;
            counts.total += zone.capacity;
            counts.maintenance += Math.max(0, zone.capacity - usable);
            counts.reserved += Math.min(reserved, inUse);
            counts.occupied += Math.max(0, inUse - reserved);
            counts.available += use?.available ?? Math.max(0, usable - inUse);
        }
        return counts;
    });
}

/** Adds floors of several lots together by position (first floor with first floor, ...). */
export function mergeFloors(perLot: FloorOccupancy[][]): OwnerOccupancy {
    const floors: FloorOccupancy[] = [];
    for (const lotFloors of perLot) {
        lotFloors.forEach((floor, index) => {
            const merged = floors[index] ?? {
                ...emptyOccupancy(),
                name: floor.name,
            };
            merged.total += floor.total;
            merged.available += floor.available;
            merged.reserved += floor.reserved;
            merged.occupied += floor.occupied;
            merged.maintenance += floor.maintenance;
            floors[index] = merged;
        });
    }
    const all = floors.reduce<OccupancyCounts>((sum, floor) => {
        sum.total += floor.total;
        sum.available += floor.available;
        sum.reserved += floor.reserved;
        sum.occupied += floor.occupied;
        sum.maintenance += floor.maintenance;
        return sum;
    }, emptyOccupancy());
    return { ...all, floors };
}

/** Counts for every approved lot of the signed-in owner, refreshed every 15 seconds. */
export function useOwnerOccupancy() {
    const lots = useOwnerStore((s) => s.lots);
    const lotIds = lots
        .filter((lot) => lot.ap === "approved" && lot.backendId)
        .map((lot) => lot.backendId as string);
    return useQuery({
        queryKey: ["owner-occupancy", lotIds],
        enabled: lotIds.length > 0,
        refetchInterval: 15_000,
        queryFn: async (): Promise<OwnerOccupancy> => {
            const perLot = await Promise.all(
                lotIds.map(async (id) => {
                    const [layout, usage] = await Promise.all([
                        readPublic<PublicLayout>(id, "layout"),
                        readPublic<PublicUtilization>(id, "zone-utilization"),
                    ]);
                    return countLot(layout, usage);
                }),
            );
            return mergeFloors(perLot);
        },
    });
}
