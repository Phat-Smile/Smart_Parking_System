import { gatewayFetch } from "@/shared/api/client";

export interface VerifiedEmailAccount {
    accountId: string;
    email: string;
    status: "Active" | "EmailVerified";
    isVerified: boolean;
    message: string;
}

export interface ResentOtpResult {
    email: string;
    expiresInSeconds: number;
    cooldownSeconds: number;
    message: string;
    otpSent: boolean;
}

export function verifyParkWiseEmail(email: string, otpCode: string) {
    return gatewayFetch<VerifiedEmailAccount>(
        "/api/identity/accounts/verify-email",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, otpCode }),
        },
    );
}

export function resendParkWiseOtp(email: string) {
    return gatewayFetch<ResentOtpResult>("/api/identity/accounts/resend-otp", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
    });
}
