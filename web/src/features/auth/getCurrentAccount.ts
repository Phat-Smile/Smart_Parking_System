import { gatewayFetch } from "@/shared/api/client";

export interface CurrentAccountResponse {
    id: string;
    email: string;
    phoneNumber: string | null;
    fullName: string | null;
    role: "Customer" | "Staff" | "ParkingLotOwner" | "SystemAdmin";
    status: "EmailVerified" | "Suspended" | "Disabled";
    hasPassword: boolean;
    googleLinked: boolean;
    createdAtUtc: string;
}

/** The frontend contract is ready; Identity does not expose this route yet. */
export function getCurrentAccount() {
    return gatewayFetch<CurrentAccountResponse>("/api/identity/accounts/me", {
        method: "GET",
        credentials: "include",
    });
}
