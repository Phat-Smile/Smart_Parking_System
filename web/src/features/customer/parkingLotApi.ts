import { getGatewayBaseUrl } from "@/shared/config/env";

export type ParkingLotSummary = {
    id: string;
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    type: string;
};

/** One row of GET /api/parking/lots/nearby (docs/api/US-SEARCH-002-Nearby-Parking-Lots.md). */
export type NearbyParkingLot = ParkingLotSummary & { distanceKm: number };

export type DailyOpeningHours = {
    dayOfWeek: number;
    isClosed: boolean;
    is24Hours: boolean;
    opensAt: string | null;
    closesAt: string | null;
};

/** Latest price of one vehicle type (pricing items of the lot detail, docs/api/Parking-Detail.md). */
export type LotPrice = {
    vehicleType: string;
    billingMode: string;
    blockMinutes: number | null;
    price: number;
    overnightFee: number;
};

/** Free spaces of the lot detail: car slots Available (BR-02.a) and motorbike spaces. */
export type LotAvailability = {
    car: { available: number };
    motorbike: { capacity: number; available: number } | null;
};

export type ParkingLotDetail = ParkingLotSummary & {
    openingHours: { days: DailyOpeningHours[] } | null;
    // Present once Parking returns prices and free spaces with the detail (MOD-PK-01).
    pricing?: LotPrice[];
    availability?: LotAvailability | null;
};

export class ParkingLotNotFoundError extends Error {}

export async function getApprovedParkingLots(signal?: AbortSignal) {
    const response = await fetch(`${getGatewayBaseUrl()}/api/parking/lots`, {
        signal,
    });
    if (!response.ok) throw new Error("Parking catalog unavailable");
    return (await response.json()) as ParkingLotSummary[];
}

/** Approved lots within radiusKm of the point, nearest first (up to 50). */
export async function searchNearbyParkingLots(
    latitude: number,
    longitude: number,
    radiusKm: number,
    signal?: AbortSignal,
): Promise<NearbyParkingLot[]> {
    const query = new URLSearchParams({
        latitude: String(latitude),
        longitude: String(longitude),
        radiusKm: String(radiusKm),
        limit: "50",
    });
    const response = await fetch(
        `${getGatewayBaseUrl()}/api/parking/lots/nearby?${query.toString()}`,
        { signal },
    );
    if (!response.ok) throw new Error("Parking search unavailable");
    return (await response.json()) as NearbyParkingLot[];
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

const VEHICLE_LABELS: Record<string, string> = {
    CAR: "Ô tô",
    MOTORBIKE: "Xe máy",
};

const formatVnd = (value: number) =>
    `${new Intl.NumberFormat("vi-VN").format(value)} ₫`;

/** "Ô tô 30.000 ₫/60 phút · Xe máy 5.000 ₫/lượt", or null when no price is set. */
export function describeLotPricing(
    pricing: LotPrice[] | undefined,
): string | null {
    if (!pricing?.length) return null;
    return pricing
        .map((item) => {
            const unit =
                item.billingMode === "Block" && item.blockMinutes
                    ? `${item.blockMinutes} phút`
                    : "lượt";
            const label =
                VEHICLE_LABELS[item.vehicleType.toUpperCase()] ??
                item.vehicleType;
            return `${label} ${formatVnd(item.price)}/${unit}`;
        })
        .join(" · ");
}

/** "7 ô tô · 30 xe máy", or null when Parking gave no counts. */
export function describeLotAvailability(
    availability: LotAvailability | null | undefined,
): string | null {
    if (!availability) return null;
    const parts = [`${availability.car.available} ô tô`];
    if (availability.motorbike)
        parts.push(`${availability.motorbike.available} xe máy`);
    return parts.join(" · ");
}
