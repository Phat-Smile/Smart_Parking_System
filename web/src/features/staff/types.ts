export type VehicleType = "Car" | "Motorbike";

export type CustomerType = "Registered" | "Guest";

export type SessionStatus = "Active" | "Completed" | "Cancelled";

export type PaymentStatus = "Pending" | "Paid" | "Exempt";

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
}

export interface CancelledSessionResponse {
    sessionId: string;
    cancelledAt: string;
    reason: string;
}

export interface SessionByQrResponse {
    sessionId: string;
    lotId: string;
    lotName: string;
    plateNumber: string;
    vehicleType: VehicleType;
    customerType: CustomerType;
    entryTime: string;
    status: SessionStatus;
    qrToken: string;
}

export interface StaffSession {
    id: string;
    lotId: string;
    plateNumber: string;
    vehicleType: VehicleType;
    customerType: CustomerType;
    driverName?: string;
    entryTime: string;
    exitTime?: string;
    status: SessionStatus;
    qrToken: string;
    slotCode: string;
    paymentStatus: PaymentStatus;
    totalFeeVnd: number;
    prepaidVnd: number;
    amountDueVnd: number;
    paymentMethod?: "VNPay" | "Cash" | "Wallet";
}

export interface VerificationIssue {
    id: string;
    sessionId?: string;
    spot: string;
    plateNumber: string;
    problem: string;
    detectedPlate: string;
    confidence: number;
    currentState: string;
    createdAt: string;
    status: "Pending" | "Resolved";
}

export interface OccupancyStats {
    totalCapacity: number;
    occupied: number;
    carAvailable: number;
    motorbikeAvailable: number;
    activeSessionsCount: number;
    needsVerificationCount: number;
}
