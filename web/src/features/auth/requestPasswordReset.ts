import { gatewayFetch } from "@/shared/api/client";

/** Request the Identity reset-link flow through this project's gateway when configured. */
export function requestPasswordReset(email: string) {
    const path = process.env.NEXT_PUBLIC_IDENTITY_FORGOT_PASSWORD_PATH;
    if (!path) {
        throw new Error("Password reset is not configured yet.");
    }
    return gatewayFetch<{ message: string }>(path, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
    });
}
