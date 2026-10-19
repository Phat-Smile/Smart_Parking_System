import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import type { ReviewedParkingLot } from "./types";

export const MAX_REJECTION_REASON = 500;

const guidPattern =
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isParkingLotId(value: string) {
    return (
        guidPattern.test(value.trim()) &&
        value.trim() !== "00000000-0000-0000-0000-000000000000"
    );
}

/** US-ADMIN-002-A: Pending -> Approved. The endpoint takes no body. */
export function approveParkingLot(parkingLotId: string) {
    return gatewayFetch<ReviewedParkingLot>(
        `/api/parking/admin/lots/${encodeURIComponent(parkingLotId)}/approve`,
        { method: "POST" },
    );
}

/** US-ADMIN-002-B: Pending -> Rejected. The reason is trimmed, 1-500 chars. */
export function rejectParkingLot(parkingLotId: string, reason: string) {
    return gatewayFetch<ReviewedParkingLot>(
        `/api/parking/admin/lots/${encodeURIComponent(parkingLotId)}/reject`,
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ reason: reason.trim() }),
        },
    );
}

/** Returns a validation message for a rejection reason, or "" when valid. */
export function validateRejectionReason(reason: string) {
    const trimmed = reason.trim();
    if (!trimmed) return "Enter a reason so the owner knows what to fix.";
    if (trimmed.length > MAX_REJECTION_REASON)
        return `The reason can be at most ${MAX_REJECTION_REASON} characters.`;
    return "";
}

/** Maps the documented 400/401/403/404/409 failures to admin-facing copy. */
export function describeReviewError(error: unknown) {
    if (!(error instanceof ApiError))
        return "We couldn't reach the server. Check your connection and try again.";
    const code = error.problem?.errorCode;
    if (code === "parking.lot_already_reviewed" || error.status === 409)
        return "This parking lot was already reviewed (possibly by another administrator). Refresh to see the final decision.";
    if (code === "parking.lot_not_found" || error.status === 404)
        return "This parking lot no longer exists.";
    if (error.status === 400)
        return "The decision was rejected as invalid. Check the rejection reason (1–500 characters) and try again.";
    if (error.status === 401)
        return "Your session expired. Sign in again to continue.";
    if (error.status === 403)
        return "Only SystemAdmin accounts can review parking lots.";
    return "The decision could not be saved. Please try again.";
}
