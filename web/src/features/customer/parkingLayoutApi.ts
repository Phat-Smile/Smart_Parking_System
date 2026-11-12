import { getGatewayBaseUrl } from "@/shared/config/env";

export type GridPoint = { x: number; y: number };
export type ParkingLayout = {
    lotId: string;
    version: number;
    floors: {
        id: string;
        name: string;
        zones: {
            id: string;
            code: string;
            vehicleGroup: string;
            capacity: number;
            currentVehicleCount: number;
            bounds: {
                x: number;
                y: number;
                width: number;
                height: number;
            } | null;
            polygon: GridPoint[] | null;
            slots: {
                id: string;
                slotCode: string;
                effectiveStatus: string;
                position: GridPoint | null;
            }[];
        }[];
    }[];
};

export type ZoneUtilization = {
    lotId: string;
    layoutVersion: number;
    zones: {
        zoneId: string;
        available: number;
        usableCapacity: number;
        inUse: number;
    }[];
};

async function readPublic<T>(
    lotId: string,
    path: string,
    signal?: AbortSignal,
) {
    const response = await fetch(
        `${getGatewayBaseUrl()}/api/parking/lots/${encodeURIComponent(lotId)}/${path}`,
        { signal, cache: "no-store" },
    );
    if (!response.ok) throw new Error("Parking layout unavailable");
    return (await response.json()) as T;
}

export function getPublicParkingLayout(lotId: string, signal?: AbortSignal) {
    return readPublic<ParkingLayout>(lotId, "layout", signal);
}

export function getPublicZoneUtilization(lotId: string, signal?: AbortSignal) {
    return readPublic<ZoneUtilization>(lotId, "zone-utilization", signal);
}
