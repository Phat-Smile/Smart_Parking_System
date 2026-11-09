export type ApprovalStatus = "Pending" | "Approved" | "Rejected";

export interface LotZone {
    id: string;
    name: string;
    vehicleType: string;
    slots: number;
    pricePerHourVnd: number;
}

export interface LotFloorLayout {
    name: string;
    rows: number;
    cols: number;
    blocked: number[];
}

/** One row of GET /api/parking/admin/lots: only what Parking stores. */
export interface AdminParkingLot {
    id: string;
    name: string;
    address: string;
    type: string;
    /** Parking does not know the owner's name or email; the screen shows the account id. */
    ownerUserId: string | null;
    status: ApprovalStatus;
    rejectionReason: string | null;
    latitude: number;
    longitude: number;

    // Optional compatibility fields for admin dashboard, review and list view
    ownerName?: string;
    ownerEmail?: string;
    submittedAtUtc?: string;
    reviewedAtUtc?: string | null;
    city?: string;
    is24h?: boolean;
    openTime?: string;
    closeTime?: string;
    vehicleTypes?: string[];
    totalCapacity?: number;
    zones?: LotZone[];
    pricing?: {
        firstHourVnd: number;
        extraHourVnd: number;
        dailyCapVnd: number;
        overnightVnd: number;
    };
    holdDepositVnd?: number;
    reservation?: {
        holdMinutes: number;
        freeCancelMinutes: number;
        noShowRule: string;
    };
    layout?: LotFloorLayout[];
    source?: "sample" | "backend";
}

export interface DailyOpeningHours {
    dayOfWeek: number;
    isClosed: boolean;
    is24Hours: boolean;
    opensAt: string | null;
    closesAt: string | null;
}

/** GET /api/parking/admin/lots/{id} (#128). */
export interface AdminParkingLotDetail {
    id: string;
    ownerUserId: string | null;
    name: string;
    address: string;
    latitude: number;
    longitude: number;
    type: string;
    approvalStatus: ApprovalStatus;
    rejectionReason: string | null;
    openingHours: { days: DailyOpeningHours[] } | null;
}

/** GET /api/parking/admin/lots/{id}/layout (#128). */
export interface AdminLotLayout {
    lotId: string;
    version: number;
    floors: Array<{
        id: string;
        name: string;
        zones: Array<{
            id: string;
            code: string;
            vehicleGroup: string;
            capacity: number;
            currentVehicleCount: number;
            slots: Array<{
                id: string;
                slotCode: string;
                effectiveStatus: string;
            }>;
        }>;
    }>;
}

/** GET /api/parking/admin/lots/{id}/pricing/{vehicleType} (#128). */
export interface AdminLotPricing {
    lotId: string;
    vehicleType: string;
    version: number;
    billingMode: string;
    blockMinutes: number | null;
    price: number;
    overnightFee: number;
    effectiveFromUtc: string;
}

/** Response of PUT/POST /api/parking/admin/lots/{id}/approve|reject. */
export interface ReviewedParkingLot {
    parkingLotId: string;
    approvalStatus: "Approved" | "Rejected";
    rejectionReason: string | null;
}

export interface OwnerApplicationDocument {
    id: string;
    type:
        | "BusinessLicense"
        | "IdentityCard"
        | "FireSafetyCert"
        | "PremisesLease"
        | "Other";
    name: string;
    fileUrl: string;
    fileType: "pdf" | "image";
    uploadedAtUtc: string;
}

export interface OwnerApplicationItem {
    accountId: string;
    email: string;
    phoneNumber?: string | null;
    fullName?: string | null;
    businessName?: string | null;
    taxCode?: string | null;
    businessAddress?: string | null;
    idCardNumber?: string | null;
    status: ApprovalStatus;
    rejectionReason?: string | null;
    reviewedAtUtc?: string | null;
    reviewedBy?: string | null;
    createdAtUtc: string;
    documents?: OwnerApplicationDocument[];
    ownerType?: string | null;
    submittedAtUtc?: string | null;
}

export interface ListOwnerApplicationsResponse {
    items: OwnerApplicationItem[];
    totalCount: number;
    page: number;
    pageSize: number;
}

export interface OwnerApplicationActionResult {
    accountId: string;
    status: ApprovalStatus;
    message?: string;
}

export interface ParkingLotAssignmentMember {
    lotId: string;
    accountId: string;
    role: "Staff" | "Owner";
    assignedAtUtc?: string;
    accountEmail?: string;
    accountFullName?: string;
    accountPhoneNumber?: string;
}
