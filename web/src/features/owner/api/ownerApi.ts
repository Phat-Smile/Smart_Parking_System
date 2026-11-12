import { gatewayFetch } from "@/shared/api/client";

// ==========================================
// 1. REGISTER PARKING LOT (US-OWNER-001)
// ==========================================
export type ParkingLotTypeEnum = "Outdoor" | "Underground" | "MultiStorey";

export interface ParkingLotFeaturesRequest {
    supportsElectricVehicles?: boolean;
    supportsLargeVehicles?: boolean;
    hasEvCharging?: boolean;
    isCovered?: boolean;
    hasCctv?: boolean;
}

export interface RegisterParkingLotRequest {
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    type: ParkingLotTypeEnum;
    features?: ParkingLotFeaturesRequest;
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
    features?: ParkingLotFeaturesRequest;
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
    features?: ParkingLotFeaturesRequest;
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
            features: req.features,
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
    features?: ParkingLotFeaturesRequest;
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
// 6. DEPOSIT & RESERVATION POLICIES (Owner Policies)
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
    const raw = await gatewayFetch<
        DepositPolicyResponse | { result: DepositPolicyResponse }
    >(`/api/parking/lots/${lotId}/deposit-policy`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(req),
    });
    if (raw && "result" in raw && raw.result) {
        return raw.result;
    }
    return raw as DepositPolicyResponse;
}

export async function getDepositPolicyApi(
    lotId: string | number,
): Promise<DepositPolicyResponse> {
    const raw = await gatewayFetch<
        DepositPolicyResponse | { result: DepositPolicyResponse }
    >(`/api/parking/lots/${lotId}/deposit-policy`, { method: "GET" });
    if (raw && "result" in raw && raw.result) {
        return raw.result;
    }
    return raw as DepositPolicyResponse;
}

export interface ConfigureReservationRatioPolicyRequest {
    reservationRatioPercent: number; // 10 to 50
}

export interface ReservationRatioPolicyResponse {
    lotId: string;
    reservationRatioPercent: number;
    isConfigured: boolean;
    updatedAtUtc: string | null;
}

export async function configureReservationRatioPolicyApi(
    lotId: string | number,
    req: ConfigureReservationRatioPolicyRequest | number,
): Promise<ReservationRatioPolicyResponse> {
    const payload =
        typeof req === "number" ? { reservationRatioPercent: req } : req;
    const raw = await gatewayFetch<
        | ReservationRatioPolicyResponse
        | { result: ReservationRatioPolicyResponse }
    >(`/api/parking/lots/${lotId}/reservation-ratio-policy`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload),
    });
    if (raw && "result" in raw && raw.result) {
        return raw.result;
    }
    return raw as ReservationRatioPolicyResponse;
}

export async function getReservationRatioPolicyApi(
    lotId: string | number,
): Promise<ReservationRatioPolicyResponse> {
    const raw = await gatewayFetch<
        | ReservationRatioPolicyResponse
        | { result: ReservationRatioPolicyResponse }
    >(`/api/parking/lots/${lotId}/reservation-ratio-policy`, { method: "GET" });
    if (raw && "result" in raw && raw.result) {
        return raw.result;
    }
    return raw as ReservationRatioPolicyResponse;
}

export interface ConfigureReservationAvailabilityPolicyRequest {
    reservationEnabled: boolean;
}

export interface ReservationAvailabilityPolicyResponse {
    lotId: string;
    reservationEnabled: boolean;
    isConfigured: boolean;
    updatedAtUtc: string | null;
}

export async function configureReservationAvailabilityApi(
    lotId: string | number,
    req: ConfigureReservationAvailabilityPolicyRequest,
): Promise<ReservationAvailabilityPolicyResponse> {
    const raw = await gatewayFetch<
        | ReservationAvailabilityPolicyResponse
        | {
              result: ReservationAvailabilityPolicyResponse;
          }
    >(`/api/parking/lots/${lotId}/reservation-availability`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(req),
    });
    if (raw && "result" in raw && raw.result) {
        return raw.result;
    }
    return raw as ReservationAvailabilityPolicyResponse;
}

export async function getReservationAvailabilityApi(
    lotId: string | number,
): Promise<ReservationAvailabilityPolicyResponse> {
    const raw = await gatewayFetch<
        | ReservationAvailabilityPolicyResponse
        | {
              result: ReservationAvailabilityPolicyResponse;
          }
    >(`/api/parking/lots/${lotId}/reservation-availability`, { method: "GET" });
    if (raw && "result" in raw && raw.result) {
        return raw.result;
    }
    return raw as ReservationAvailabilityPolicyResponse;
}

// ==========================================
// 7. PARKING LAYOUT MANAGEMENT (US-OWNER-007)
// ==========================================
export interface LayoutSlotDto {
    id: string;
    slotCode: string;
    sensorCode?: string | null;
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

// ==========================================
// 8. POLICIES: RESERVATION RATIO (30/70)
// ==========================================
