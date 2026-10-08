import { gatewayFetch } from "@/shared/api/client";

export class ResetEndpointUnavailableError extends Error {
    constructor() {
        super("The password reset endpoint is not configured.");
    }
}

/** The Identity reset route must be supplied when its backend contract exists. */
export async function changePassword(token: string, newPassword: string) {
    const path = process.env.NEXT_PUBLIC_IDENTITY_RESET_PASSWORD_PATH?.trim();
    if (!path) throw new ResetEndpointUnavailableError();

    await gatewayFetch<unknown>(path, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ token, newPassword }),
    });
}
