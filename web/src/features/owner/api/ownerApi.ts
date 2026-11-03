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

/**
 * US-OWNER-001: Register a new parking lot for admin review.
 * Endpoint: POST /api/parking/lots
 */
export async function registerParkingLotApi(
    req: RegisterParkingLotRequest,
): Promise<RegisteredParkingLotResponse> {
    return gatewayFetch<RegisteredParkingLotResponse>("/api/parking/lots", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
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
            headers: { "Content-Type": "application/json" },
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
    });
}

export interface OwnerParkingLotItem {
    id: string;
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    type: string;
    approvalStatus: "Pending" | "Approved" | "Rejected";
    rejectionReason?: string | null;
    hasOpeningHours?: boolean;
    hasPricing?: boolean;
}

export interface OwnerParkingLotsPagedResult {
    items: OwnerParkingLotItem[];
    totalCount: number;
    page: number;
    pageSize: number;
}

/**
 * Get owner's lots (including Pending, Approved, Rejected).
 * Endpoint: GET /api/parking/owner/lots (API-PK-01)
 */
export async function getOwnerParkingLotsApi(params?: {
    page?: number;
    pageSize?: number;
    status?: string;
}): Promise<OwnerParkingLotsPagedResult> {
    const query = new URLSearchParams();
    if (params?.page) query.set("page", String(params.page));
    if (params?.pageSize) query.set("pageSize", String(params.pageSize));
    if (params?.status) query.set("status", params.status);
    const qStr = query.toString();
    return gatewayFetch<OwnerParkingLotsPagedResult>(
        `/api/parking/owner/lots${qStr ? `?${qStr}` : ""}`,
        { method: "GET" },
    );
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
    });
}

// ==========================================
// 4. PARKING SESSIONS (US-PARK-003)
// ==========================================
export interface OpenParkingSessionRequest {
    lotId: string;
    plateNumber: string;
    vehicleType: "Car" | "Motorbike";
}

export interface OpenedParkingSessionResponse {
    sessionId: string;
    lotId: string;
    plateNumber: string;
    vehicleType: string;
    startedAtUtc: string;
}

export async function openParkingSessionApi(
    req: OpenParkingSessionRequest,
): Promise<OpenedParkingSessionResponse> {
    return gatewayFetch<OpenedParkingSessionResponse>("/api/parking/sessions", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(req),
    });
}

export async function cancelParkingSessionApi(
    sessionId: string,
    reason: string,
): Promise<void> {
    return gatewayFetch(`/api/parking/sessions/${sessionId}/cancel`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ reason }),
    });
}

// ==========================================
// 5. OPENING HOURS (US-OWNER-002)
// ==========================================
export interface OpeningHoursDayItem {
    dayOfWeek: number; // 0 (Sunday) - 6 (Saturday)
    isClosed: boolean;
    is24Hours: boolean;
    opensAt?: string | null; // "HH:mm"
    closesAt?: string | null; // "HH:mm"
}

export interface ConfigureOpeningHoursRequest {
    days: OpeningHoursDayItem[];
}

export async function configureOpeningHoursApi(
    lotId: string | number,
    req: ConfigureOpeningHoursRequest,
): Promise<{ days: OpeningHoursDayItem[] }> {
    return gatewayFetch<{ days: OpeningHoursDayItem[] }>(
        `/api/parking/owner/lots/${lotId}/opening-hours`,
        {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(req),
        },
    );
}

export async function getOpeningHoursApi(
    lotId: string | number,
): Promise<{ days: OpeningHoursDayItem[]; isConfigured: boolean }> {
    return gatewayFetch<{ days: OpeningHoursDayItem[]; isConfigured: boolean }>(
        `/api/parking/owner/lots/${lotId}/opening-hours`,
        { method: "GET" },
    );
}

// ==========================================
// 6. DEPOSIT POLICY (OwnerDepositConfig)
// ==========================================
export interface ConfigureDepositPolicyRequest {
    holdDepositVnd: number; // integer VND >= 0
}

/** GET/PUT /api/parking/lots/{lotId}/deposit-policy (DepositPolicyResponse in Parking). */
export interface DepositPolicyResponse {
    lotId: string;
    holdDepositVnd: number;
    isConfigured: boolean;
    updatedAtUtc: string | null;
}

export async function configureDepositPolicyApi(
    lotId: string | number,
    req: ConfigureDepositPolicyRequest,
): Promise<DepositPolicyResponse> {
    return gatewayFetch<DepositPolicyResponse>(
        `/api/parking/lots/${lotId}/deposit-policy`,
        {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(req),
        },
    );
}

export async function getDepositPolicyApi(
    lotId: string | number,
): Promise<DepositPolicyResponse> {
    return gatewayFetch<DepositPolicyResponse>(
        `/api/parking/lots/${lotId}/deposit-policy`,
        { method: "GET" },
    );
}

// ==========================================
// 7. PARKING LAYOUT MANAGEMENT (US-OWNER-007)
// ==========================================
export interface LayoutSlotDto {
    id: string;
    slotCode: string;
    overrideState?: "Maintenance" | "Unavailable" | null;
    position: { x: number; y: number };
}

export interface LayoutZoneDto {
    id: string;
    code: string;
    vehicleGroup: "Car" | "Motorbike";
    capacity: number;
    hasSlotSensor: boolean;
    bounds?: { x: number; y: number; width: number; height: number };
    slots?: LayoutSlotDto[];
}

export interface LayoutFloorDto {
    id: string;
    name: string;
    zones: LayoutZoneDto[];
}

export interface ParkingLayoutDocumentDto {
    version: number;
    floors: LayoutFloorDto[];
}

export interface SaveParkingLayoutRequest {
    expectedVersion: number;
    floors: LayoutFloorDto[];
}

export async function getParkingLayoutApi(
    lotId: string | number,
): Promise<ParkingLayoutDocumentDto> {
    return gatewayFetch<ParkingLayoutDocumentDto>(
        `/api/parking/owner/lots/${lotId}/layout`,
        { method: "GET" },
    );
}

export async function saveParkingLayoutApi(
    lotId: string | number,
    req: SaveParkingLayoutRequest,
): Promise<ParkingLayoutDocumentDto> {
    return gatewayFetch<ParkingLayoutDocumentDto>(
        `/api/parking/owner/lots/${lotId}/layout`,
        {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(req),
        },
    );
}
