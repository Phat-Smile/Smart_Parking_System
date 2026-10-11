import { gatewayFetch } from "@/shared/api/client";
import { clearAuthSession, getAuthSessionSnapshot } from "./authSession";

export async function logoutParkWiseAccount() {
    const session = getAuthSessionSnapshot();
    if (!session?.refreshToken) {
        throw new Error("No active refresh session is available.");
    }

    await gatewayFetch<{ message: string }>("/api/identity/accounts/logout", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken: session.refreshToken }),
    });
    clearAuthSession();
}
