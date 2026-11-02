import { gatewayFetch } from "@/shared/api/client";

export interface CreateReservationHoldRequest {
    lotId: string;
    vehicleId: string;
    plateNumber: string;
    vehicleGroup?: string;
    reservationType?: string;
    appointmentTime: string; // ISO 8601 string
    durationMinutes?: number;
    prepaidAmount: number;
    priceSnapshot?: string;
}

export interface ReservationHoldResponse {
    reservationId: string;
    lotId: string;
    vehicleId: string;
    plateNumber: string;
    vehicleGroup: string;
    reservationType: string;
    status: string;
    appointmentTime: string;
    durationMinutes: number | null;
    tempHoldExpiresAt: string | null;
    windowStartAt: string;
    windowEndAt: string;
    prepaidAmount: number;
    priceSnapshot: string | null;
    createdAt: string;
}

export interface ConfirmReservationRequest {
    paymentMethod?: string;
    depositAmount: number;
}

export interface ConfirmedReservationDto {
    reservationId: string;
    status: string;
    confirmedAt: string;
    qrCodeToken: string;
    prepaidAmount: number;
}

export interface CancelReservationRequest {
    reason?: string;
}

export interface CancelledReservationDto {
    reservationId: string;
    status: string;
    refundAmount: number;
    message: string;
}

export interface ReservationSummaryDto {
    reservationId: string;
    lotId: string;
    vehicleId: string;
    plateNumber: string;
    vehicleGroup: string;
    reservationType: string;
    status: string;
    appointmentTime: string;
    durationMinutes: number | null;
    tempHoldExpiresAt: string | null;
    windowStartAt: string;
    windowEndAt: string;
    prepaidAmount: number;
    qrCodeToken: string | null;
    priceSnapshot: string | null;
    createdAt: string;
    confirmedAt: string | null;
}

export async function createReservationHold(
    request: CreateReservationHoldRequest,
): Promise<ReservationHoldResponse> {
    return await gatewayFetch<ReservationHoldResponse>("/api/booking/holds", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });
}

export async function confirmReservation(
    reservationId: string,
    request: ConfirmReservationRequest,
): Promise<ConfirmedReservationDto> {
    return await gatewayFetch<ConfirmedReservationDto>(
        `/api/booking/reservations/${encodeURIComponent(reservationId)}/confirm`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(request),
        },
    );
}

export async function getMyReservations(
    status?: string,
): Promise<ReservationSummaryDto[]> {
    const query = status ? `?status=${encodeURIComponent(status)}` : "";
    return await gatewayFetch<ReservationSummaryDto[]>(
        `/api/booking/reservations/me${query}`,
    );
}

export async function cancelReservation(
    reservationId: string,
    request: CancelReservationRequest,
): Promise<CancelledReservationDto> {
    return await gatewayFetch<CancelledReservationDto>(
        `/api/booking/reservations/${encodeURIComponent(reservationId)}/cancel`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(request),
        },
    );
}
