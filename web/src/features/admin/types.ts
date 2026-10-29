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
    /** Slot indexes (row * cols + col) that are aisles / not usable. */
    blocked: number[];
}

export interface AdminParkingLot {
    id: string;
    name: string;
    ownerName: string;
    ownerEmail: string;
    submittedAtUtc: string;
    status: ApprovalStatus;
    rejectionReason: string | null;
    reviewedAtUtc: string | null;
    address: string;
    city: string;
    latitude: number;
    longitude: number;
    is24h: boolean;
    openTime: string;
    closeTime: string;
    vehicleTypes: string[];
    totalCapacity: number;
    zones: LotZone[];
    pricing: {
        firstHourVnd: number;
        extraHourVnd: number;
        dailyCapVnd: number;
        overnightVnd: number;
    };
    holdDepositVnd: number;
    reservation: {
        holdMinutes: number;
        freeCancelMinutes: number;
        noShowRule: string;
    };
    layout: LotFloorLayout[];
    /**
     * "sample": demo data used until Parking exposes admin list/detail
     * endpoints. "backend": a lot id the admin typed in; only the review
     * decision itself is backed by the real API.
     */
    source: "sample" | "backend";
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
