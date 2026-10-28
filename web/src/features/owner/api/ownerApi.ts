import { gatewayFetch } from "@/shared/api/client";

// ==========================================
// 1. REGISTER PARKING LOT (US-OWNER-001)
// ==========================================
export type ParkingLotTypeEnum = "Outdoor" | "Underground" | "MultiStorey";

export interface RegisterParkingLotRequest {
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    type: ParkingLotTypeEnum;
}

export interface RegisteredParkingLotResponse {
    id: string;
    ownerUserId: string;
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    type: string;
    approvalStatus: "Pending" | string;
}

// ==========================================
// 2. CONFIGURE PRICING (US-OWNER-004)
// ==========================================
export type BillingMode = "Block" | "PerEntry";
export type VehicleTypeUpper = "CAR" | "MOTORBIKE";

export interface ConfigurePricingRequest {
    billingMode: BillingMode;
    blockMinutes?: number | null;
    price: number;
    overnightFee?: number;
    expectedVersion?: number | null;
    reason?: string;
}

export interface ConfigurePricingResult {
    version: number;
    isNewVersionCreated: boolean;
}

export interface ConfigurePricingResponse {
    result: ConfigurePricingResult;
    isSuccess: boolean;
    statusCode: number;
    message: string;
}

// ==========================================
// 3. CATALOG & DETAIL (PUBLIC / OWNER VIEW)
// ==========================================
export interface ParkingLotCatalogItem {
    id: string;
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    type: string;
}

export interface ParkingLotDetail {
    id: string;
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    type: string;
    approvalStatus: string;
}

// Helper to retrieve token from common local storage keys
export function getAuthToken(): string | null {
    if (typeof window === "undefined") return null;
    return (
        localStorage.getItem("token") ||
        localStorage.getItem("accessToken") ||
        localStorage.getItem("smart_parking_token") ||
        localStorage.getItem("auth_token") ||
        null
    );
}

function createAuthHeaders(): HeadersInit {
    const headers: Record<string, string> = {
        "Content-Type": "application/json",
    };
    const token = getAuthToken();
    if (token) {
        headers["Authorization"] = `Bearer ${token}`;
    }
    return headers;
}

/**
 * US-OWNER-001: Register a new parking lot for admin review.
 * Endpoint: POST /api/parking/lots
 */
export async function registerParkingLotApi(
    req: RegisterParkingLotRequest,
): Promise<RegisteredParkingLotResponse> {
    return gatewayFetch<RegisteredParkingLotResponse>("/api/parking/lots", {
        method: "POST",
        headers: createAuthHeaders(),
        body: JSON.stringify({
            name: req.name.trim(),
            address: req.address.trim(),
            latitude: req.latitude,
            longitude: req.longitude,
            type: req.type,
        }),
    });
}

/**
 * US-OWNER-004: Configure pricing for a vehicle type (CAR / MOTORBIKE).
 * Endpoint: PUT /api/parking/lots/{lotId}/pricing/{vehicleType}
 */
export async function configurePricingApi(
    lotId: string | number,
    vehicleType: VehicleTypeUpper,
    req: ConfigurePricingRequest,
): Promise<ConfigurePricingResponse> {
    return gatewayFetch<ConfigurePricingResponse>(
        `/api/parking/lots/${lotId}/pricing/${vehicleType}`,
        {
            method: "PUT",
            headers: createAuthHeaders(),
            body: JSON.stringify(req),
        },
    );
}

/**
 * Get public list of approved parking lots.
 * Endpoint: GET /api/parking/lots
 */
export async function getApprovedParkingLotsApi(): Promise<
    ParkingLotCatalogItem[]
> {
    return gatewayFetch<ParkingLotCatalogItem[]>("/api/parking/lots", {
        method: "GET",
        headers: createAuthHeaders(),
    });
}

/**
 * Get parking lot detail.
 * Endpoint: GET /api/parking/lots/{id}
 */
export async function getParkingLotDetailApi(
    lotId: string,
): Promise<ParkingLotDetail> {
    return gatewayFetch<ParkingLotDetail>(`/api/parking/lots/${lotId}`, {
        method: "GET",
        headers: createAuthHeaders(),
    });
}
