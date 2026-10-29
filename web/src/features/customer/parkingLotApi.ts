import { getGatewayBaseUrl } from "@/shared/config/env";

export type ParkingLotSummary = {
    id: string;
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    type: string;
};

export type DailyOpeningHours = {
    dayOfWeek: number;
    isClosed: boolean;
    is24Hours: boolean;
    opensAt: string | null;
    closesAt: string | null;
};

export type ParkingLotDetail = ParkingLotSummary & {
    openingHours: { days: DailyOpeningHours[] } | null;
};

export class ParkingLotNotFoundError extends Error {}

export async function getApprovedParkingLots(signal?: AbortSignal) {
    const response = await fetch(`${getGatewayBaseUrl()}/api/parking/lots`, {
        signal,
    });
    if (!response.ok) throw new Error("Parking catalog unavailable");
    return (await response.json()) as ParkingLotSummary[];
}

export async function getApprovedParkingLot(id: string, signal?: AbortSignal) {
    const response = await fetch(
        `${getGatewayBaseUrl()}/api/parking/lots/${encodeURIComponent(id)}`,
        { signal },
    );
    if (response.status === 404) throw new ParkingLotNotFoundError();
    if (!response.ok) throw new Error("Parking lot unavailable");
    return (await response.json()) as ParkingLotDetail;
}
