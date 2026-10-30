import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";

/** One open session of the signed-in customer (MyParkingSession in Parking). */
export interface MyParkingSession {
    sessionId: string;
    lotId: string;
    plateNumber: string;
    vehicleType: string;
    status: string;
    entryTime: string;
    /** Session QR shown at the exit gate; not a VNPay payment QR. */
    qrToken: string;
    zoneCode: string | null;
    floorName: string | null;
}

export interface SessionLot {
    name: string;
    address: string;
}

/** Endpoint: GET /api/parking/sessions/me */
export async function getMyParkingSessions(): Promise<MyParkingSession[]> {
    return await gatewayFetch<MyParkingSession[]>("/api/parking/sessions/me");
}

/**
 * Name and address of a session's lot from the public lot detail.
 * A lot that is no longer public (404) has no name; the session is still shown.
 */
export async function getSessionLot(lotId: string): Promise<SessionLot | null> {
    try {
        const lot = await gatewayFetch<SessionLot>(
            `/api/parking/lots/${encodeURIComponent(lotId)}`,
        );
        return { name: lot.name, address: lot.address };
    } catch (error) {
        if (error instanceof ApiError && error.status === 404) return null;
        throw error;
    }
}
