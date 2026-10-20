import { gatewayFetch } from "@/shared/api/client";

export interface ForgotPasswordResponse {
    email: string;
    expiresInSeconds: number;
    cooldownSeconds: number;
    message: string;
    otpSent?: boolean;
}

/** Request the Identity password-reset OTP. */
export function requestPasswordReset(email: string) {
    return gatewayFetch<ForgotPasswordResponse>(
        "/api/identity/accounts/forgot-password",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email }),
        },
    );
}
