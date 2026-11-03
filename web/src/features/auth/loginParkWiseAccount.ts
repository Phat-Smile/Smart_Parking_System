import { gatewayFetch } from "@/shared/api/client";
import { setAuthSession } from "./authSession";

export interface LoginResponse {
    accessToken: string;
    tokenType: "Bearer";
    expiresIn: number;
    refreshToken: string;
    account: {
        id: string;
        email: string;
        phoneNumber: string | null;
        status: "EmailVerified" | "Active" | "Suspended" | "Disabled";
        role: "Customer" | "Staff" | "ParkingLotOwner" | "SystemAdmin";
    };
}

/** Authenticate and retain the short-lived access token for this browser tab. */
export async function loginParkWiseAccount(email: string, password: string) {
    const response = await gatewayFetch<LoginResponse>(
        "/api/identity/accounts/login",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, password }),
        },
    );
    setAuthSession(response);
    return response.account;
}
