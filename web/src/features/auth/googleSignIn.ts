import { gatewayFetch } from "@/shared/api/client";
import { ApiError } from "@/shared/errors/api-error";
import type { LoginResponse } from "./loginParkWiseAccount";
import { setAuthSession } from "./authSession";

/** US-AUTH-002: exchange the Google ID token for a ParkWise session. Signs up on the first Google sign-in. */
export async function googleLoginParkWiseAccount(idToken: string) {
    const response = await gatewayFetch<LoginResponse>(
        "/api/identity/accounts/google",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ idToken }),
        },
    );
    setAuthSession(response);
    return response.account;
}

export function describeGoogleSignInError(error: unknown): string {
    if (!(error instanceof ApiError)) {
        return "We couldn't connect. Check your connection and try again.";
    }

    const code = error.problem?.errorCode;
    const message =
        code === "identity.invalid_google_token" || error.status === 401
            ? "Google sign-in could not be verified. Please try again."
            : code === "identity.google_login_unavailable" ||
                error.status === 503
              ? "Google sign-in is not available right now. Use your email and password instead."
              : code === "identity.account_suspended" ||
                  code === "identity.account_disabled" ||
                  error.status === 403
                ? "This account is suspended or disabled. Please contact support."
                : code === "identity.account_temporarily_locked" ||
                    error.status === 423
                  ? "This account is temporarily locked. Please try again later."
                  : "We couldn't sign you in with Google. Please try again later.";
    return error.correlationId
        ? `${message} Support ID: ${error.correlationId}`
        : message;
}
