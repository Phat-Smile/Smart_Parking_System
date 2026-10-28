import { gatewayFetch } from "@/shared/api/client";
import { clearAuthSession, getAuthSessionSnapshot } from "./authSession";

export async function logoutParkWiseAccount() {
    const session = getAuthSessionSnapshot();
    try {
        await gatewayFetch<{ message: string }>(
            "/api/identity/accounts/logout",
            {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                // Current Identity implementation still requires the body token.
                body: JSON.stringify(
                    session?.refreshToken
                        ? { refreshToken: session.refreshToken }
                        : {},
                ),
            },
        );
    } finally {
        clearAuthSession();
    }
}
