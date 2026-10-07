import { gatewayFetch } from "@/shared/api/client";

export interface RegisterAccountRequest {
    email: string;
    password: string;
    phoneNumber: string | null;
}

export interface RegisteredAccount {
    accountId: string;
    email: string;
    status: string;
}

export function registerAccount(request: RegisterAccountRequest) {
    return gatewayFetch<RegisteredAccount>("/api/identity/accounts", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(request),
    });
}
