import { gatewayFetch } from "@/shared/api/client";

export interface LoginResponse {
    accessToken: string;
    tokenType: string;
    expiresIn: number;
    refreshToken: string;
    account: {
        id: string;
        email: string;
        phoneNumber: string | null;
        status: string;
        role: string;
    };
}

/** Validate credentials with Identity. Session persistence awaits US-AUTH-005. */
export async function loginParkWiseAccount(email: string, password: string) {
    const response = await gatewayFetch<LoginResponse>(
        "/api/identity/accounts/login",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, password }),
        },
    );
    // Tokens are deliberately not stored or logged while refresh/logout is unavailable.
    return response.account;
}
