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

/** Response of POST /api/parking/admin/lots/{id}/approve|reject. */
export interface ReviewedParkingLot {
    parkingLotId: string;
    approvalStatus: "Approved" | "Rejected";
    rejectionReason: string | null;
}
