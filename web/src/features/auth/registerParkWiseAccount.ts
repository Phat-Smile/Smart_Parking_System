import { gatewayFetch } from "@/shared/api/client";

export interface RegisterAccountRequest {
    email: string;
    password: string;
    fullName?: string | null;
    phoneNumber?: string | null;
    accountType?: string | null;
}

export interface ParkWiseRegisteredAccount {
    accountId: string;
    email: string;
    fullName: string | null;
    status: "PendingVerification" | "PendingEmailVerification";
    message: string;
}

export function registerParkWiseAccount(request: RegisterAccountRequest) {
    return gatewayFetch<ParkWiseRegisteredAccount>("/api/identity/accounts", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });
}
