import { gatewayFetch } from "@/shared/api/client";

/** Reset a forgotten password with the emailed six-digit OTP. */
export async function resetPassword(
    email: string,
    otpCode: string,
    newPassword: string,
) {
    return gatewayFetch<{ message: string }>(
        "/api/identity/accounts/reset-password",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ email, otpCode, newPassword }),
        },
    );
}

/** Change the password of the signed-in account. */
export function changePassword(currentPassword: string, newPassword: string) {
    return gatewayFetch<{ message: string }>(
        "/api/identity/accounts/change-password",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ currentPassword, newPassword }),
        },
    );
}
