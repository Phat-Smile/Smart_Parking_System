import { gatewayFetch } from "@/shared/api/client";
import { setAuthSession } from "./authSession";

export interface LoginResponse {
    accessToken: string;
    tokenType: "Bearer";
    expiresIn: number;
    refreshToken?: string;
    account: {
        id: string;
        email: string;
        phoneNumber: string | null;
        status: "EmailVerified" | "Active" | "Suspended" | "Disabled";
        role: "Customer" | "Staff" | "ParkingLotOwner" | "SystemAdmin";
    };
}

export interface MfaRequiredResponse {
    mfaRequired: true;
    mfaToken: string;
    expiresIn?: number;
}

export type LoginAccount = LoginResponse["account"];

export type LoginOutcome =
    | (LoginAccount & { mfaRequired?: false; mfaToken?: never })
    | MfaRequiredResponse;

/** Authenticate and retain the short-lived access token for this browser tab, or return MFA requirement. */
export async function loginParkWiseAccount(
    email: string,
    password: string,
): Promise<LoginOutcome> {
    const response = await gatewayFetch<LoginResponse | MfaRequiredResponse>(
        "/api/identity/accounts/login",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, password }),
        },
    );

    if ("mfaRequired" in response && response.mfaRequired) {
        return {
            mfaRequired: true,
            mfaToken: response.mfaToken,
            expiresIn: response.expiresIn,
        };
    }

    const session = response as LoginResponse;
    setAuthSession(session);
    return session.account;
}

/** Complete step 2 MFA authentication with TOTP code or recovery code. */
export async function completeMfaLogin(
    mfaToken: string,
    code: string,
): Promise<LoginAccount> {
    const response = await gatewayFetch<LoginResponse>(
        "/api/identity/accounts/login/mfa",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ mfaToken, code }),
        },
    );
    setAuthSession(response);
    return response.account;
}
