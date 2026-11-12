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

export type CustomerLotSearchItem = ParkingLotSummary & {
    distanceKm: number | null;
    drivingTimeMinutes: number | null;
    startingPrice: number | null;
    billingMode: string | null;
    availableSpaces: number | null;
    prebookable: boolean;
    compatibility: string[];
    amenities: string[];
};

export type CustomerLotSearchFilters = {
    latitude?: number;
    longitude?: number;
    radiusKm?: number;
    vehicleType?: "Car" | "Motorbike";
    maxPrice?: number;
    availableOnly?: boolean;
    prebookableOnly?: boolean;
    compatibility?: string[];
    amenities?: string[];
    sort?: "recommended" | "name" | "price" | "distance";
};

export async function searchCustomerParkingLots(
    filters: CustomerLotSearchFilters,
    signal?: AbortSignal,
): Promise<CustomerLotSearchItem[]> {
    const params = new URLSearchParams({ limit: "50" });
    for (const key of [
        "latitude",
        "longitude",
        "radiusKm",
        "vehicleType",
        "maxPrice",
        "sort",
    ] as const) {
        const value = filters[key];
        if (value !== undefined) params.set(key, String(value));
    }
    if (filters.availableOnly) params.set("availableOnly", "true");
    if (filters.prebookableOnly) params.set("prebookableOnly", "true");
    if (filters.compatibility?.length)
        params.set("compatibility", filters.compatibility.join(","));
    if (filters.amenities?.length)
        params.set("amenities", filters.amenities.join(","));
    const response = await fetch(
        `${getGatewayBaseUrl()}/api/parking/lots/search?${params}`,
        { signal },
    );
    if (!response.ok) throw new Error("Parking search unavailable");
    return (await response.json()) as CustomerLotSearchItem[];
}

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

export async function getApprovedParkingLots(
    options?: { vehicleType?: "Car" | "Motorbike"; arrivalAt?: string },
    signal?: AbortSignal,
) {
    const query = new URLSearchParams();
    if (options?.vehicleType) query.set("vehicleType", options.vehicleType);
    if (options?.arrivalAt) query.set("arrivalAt", options.arrivalAt);
    const suffix = query.size ? `?${query.toString()}` : "";
    const response = await fetch(
        `${getGatewayBaseUrl()}/api/parking/lots${suffix}`,
        { signal },
    );
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
    CAR: "Car",
    MOTORBIKE: "Motorbike",
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
                    ? `${item.blockMinutes} min`
                    : "visit";
            const label =
                VEHICLE_LABELS[item.vehicleType.toUpperCase()] ??
                item.vehicleType;
            return `${label} ${formatVnd(item.price)}/${unit}`;
        })
        .join(" · ");
}

export type LotPricingEstimate = {
    rateValue: number;
    isPerEntry: boolean;
    estimatedTotal: number;
};

export function findLotPrice(
    pricing: LotPrice[] | undefined,
    vehicleType?: string,
): LotPrice | null {
    if (!pricing?.length) return null;
    if (vehicleType) {
        const wanted = vehicleType.toUpperCase();
        const exact = pricing.find(
            (item) => item.vehicleType.toUpperCase() === wanted,
        );
        if (exact) return exact;
    }
    return (
        pricing.find((item) => item.vehicleType.toUpperCase() === "CAR") ??
        pricing[0]
    );
}

export function estimateLotBookingPricing(
    pricing: LotPrice[] | undefined,
    durationHours: number,
    vehicleType?: string,
    fallbackHourlyPrice?: number,
): LotPricingEstimate | null {
    const matched = findLotPrice(pricing, vehicleType);
    if (matched) {
        if (
            matched.billingMode === "Block" &&
            matched.blockMinutes &&
            matched.blockMinutes > 0
        ) {
            const hourlyRate = Math.round(
                (matched.price * 60) / matched.blockMinutes,
            );
            const blocks = Math.max(
                1,
                Math.ceil((durationHours * 60) / matched.blockMinutes),
            );
            return {
                rateValue: hourlyRate,
                isPerEntry: false,
                estimatedTotal: blocks * matched.price,
            };
        }
        return {
            rateValue: matched.price,
            isPerEntry: matched.billingMode === "PerEntry",
            estimatedTotal:
                matched.billingMode === "PerEntry"
                    ? matched.price
                    : matched.price * durationHours,
        };
    }

    if (fallbackHourlyPrice !== undefined) {
        return {
            rateValue: fallbackHourlyPrice,
            isPerEntry: false,
            estimatedTotal: fallbackHourlyPrice * durationHours,
        };
    }

    return null;
}

/** "7 cars · 30 motorbikes", or null when Parking gave no counts. */
export function describeLotAvailability(
    availability: LotAvailability | null | undefined,
): string | null {
    if (!availability) return null;
    const parts = [`${availability.car.available} cars`];
    if (availability.motorbike)
        parts.push(`${availability.motorbike.available} motorbikes`);
    return parts.join(" · ");
}

/** Top-3 AI Recommendation item (US-AI-001/002) */
export type RecommendedParkingLot = {
    lotId: string;
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    rank: number;
    driveTimeMinutes: number | null;
    probabilityPercent: number;
    price: number;
    warningHighFullRisk: boolean;
    reasons: string[];
    distanceKm: number;
};

export type ParkingLotRecommendationResult = {
    recommendedLots: RecommendedParkingLot[];
    fallbackUsed: boolean;
    fallbackReason: string | null;
};

export async function getParkingLotRecommendations(
    latitude: number,
    longitude: number,
    options?: {
        vehicleType?: string;
        arrivalAt?: string;
        durationMinutes?: number;
        radiusKm?: number;
        topK?: number;
    },
    signal?: AbortSignal,
): Promise<ParkingLotRecommendationResult> {
    const params = new URLSearchParams({
        latitude: String(latitude),
        longitude: String(longitude),
        vehicleType: options?.vehicleType ?? "Car",
        durationMinutes: String(options?.durationMinutes ?? 120),
        radiusKm: String(options?.radiusKm ?? 10),
        topK: String(options?.topK ?? 3),
    });
    if (options?.arrivalAt) {
        params.set("arrivalAt", options.arrivalAt);
    }
    const response = await fetch(
        `${getGatewayBaseUrl()}/api/parking/lots/recommendations?${params.toString()}`,
        { signal },
    );
    if (!response.ok) {
        throw new Error("Failed to fetch parking lot recommendations");
    }
    return (await response.json()) as ParkingLotRecommendationResult;
}
