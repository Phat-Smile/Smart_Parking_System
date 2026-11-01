export type VehicleType = "Car" | "Motorbike";

export type CustomerType = "Registered" | "Guest";

export type SessionStatus = "Active" | "Paid" | "Completed" | "Cancelled";

export type PaymentStatus = "Pending" | "Paid" | "Exempt";

/** Counts come from Parking; null means Parking could not give them (no motorbike zone, lot not public). */
export interface AssignedLotResponse {
    lotId: string;
    lotName: string;
    address?: string;
    availableMotorbikeSlots: number | null;
    motorbikeCapacity: number | null;
    availableCarSlots: number | null;
    totalCapacity?: number | null;
    occupiedSlots?: number;
}

export interface OpenedSessionResponse {
    sessionId: string;
    lotId: string;
    plateNumber: string;
    vehicleType: VehicleType;
    customerType: CustomerType;
    status: SessionStatus;
    entryTime: string;
    isAccountLinked: boolean;
    reservationId: string | null;
    qrToken: string;
    zoneId?: string | null;
    zoneCode?: string | null;
    floorName?: string | null;
    slotCode?: string | null;
    availableMotorbikeSlots: number | null;
}

export interface CancelledSessionResponse {
    sessionId: string;
    cancelledAt: string;
    reason: string;
}

export interface CompletedSessionResponse {
    sessionId: string;
    status: SessionStatus;
    completedAt: string;
    amountPaidVnd?: number;
    paymentMethod?: string;
}

export interface SessionByQrResponse {
    sessionId: string;
    lotId: string;
    lotName?: string;
    plateNumber: string;
    vehicleType: VehicleType;
    customerType: CustomerType;
    entryTime: string;
    status: SessionStatus;
    qrToken: string;
    amountDueVnd?: number;
    totalFeeVnd?: number;
}

/** A session as the staff screens show it; fields Parking or Payment did not send stay undefined. */
export interface StaffSession {
    id: string;
    lotId: string;
    plateNumber: string;
    vehicleType: VehicleType;
    customerType?: CustomerType;
    driverName?: string;
    entryTime: string;
    exitTime?: string;
    status: SessionStatus;
    qrToken?: string;
    slotCode: string | null;
    paymentStatus: PaymentStatus;
    totalFeeVnd?: number;
    prepaidVnd?: number;
    amountDueVnd?: number;
    paymentMethod?: "VNPay" | "Cash" | "Wallet";
}

export interface VerificationIssue {
    id: string;
    sessionId?: string;
    spot: string;
    spotVi?: string;
    spotEn?: string;
    plateNumber: string;
    problem: string;
    problemVi?: string;
    problemEn?: string;
    detectedPlate: string;
    confidence: number;
    currentState: string;
    currentStateVi?: string;
    currentStateEn?: string;
    createdAt: string;
    status: "Pending" | "Resolved";
}

export interface OccupancyStats {
    /** Motorbike capacity only: Parking has no car total for staff yet. */
    totalCapacity: number | null;
    occupied: number;
    carAvailable: number | null;
    motorbikeAvailable: number | null;
    activeSessionsCount: number;
    needsVerificationCount: number;
}

export interface StaffLotItem {
    id: string;
    name: string;
    address: string;
    type: string;
}

export interface ReissuedQrResponse {
    sessionId: string;
    qrToken: string;
}

export interface VerifiedExitResponse {
    sessionId: string;
    canExit: boolean;
    reason?: string;
}

export interface OpenSessionByPlateResponse {
    sessionId: string;
    lotId: string;
    plateNumber: string;
    vehicleType: VehicleType;
    entryTime: string;
    status: SessionStatus;
    qrToken?: string;
}

export interface LotParkingSessionItem {
    sessionId: string;
    plateNumber: string;
    vehicleType: string;
    status: string;
    entryTime: string;
    slotCode: string | null;
}

export interface LotParkingSessionPageResponse {
    items: LotParkingSessionItem[];
    totalCount: number;
    page: number;
    pageSize: number;
}
