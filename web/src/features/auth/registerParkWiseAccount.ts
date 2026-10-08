import { gatewayFetch } from "@/shared/api/client";

export interface ParkWiseRegisteredAccount {
    accountId: string;
    email: string;
    fullName: string;
    status: string;
    // Present after the dormant-account contract in US-AUTH-001 is merged.
    isNewAccount?: boolean;
}

export function registerParkWiseAccount(request: {
    fullName: string;
    email: string;
    password: string;
    phoneNumber: string;
}) {
    return gatewayFetch<ParkWiseRegisteredAccount>("/api/identity/accounts", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });
}
