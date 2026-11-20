import { clearAccessToken, setAccessToken } from "@/shared/api/access-token";
import type { LoginResponse } from "./loginParkWiseAccount";
import { getGatewayBaseUrl } from "@/shared/config/env";
import { registerAuthRefreshHandler } from "@/shared/api/client";

const REGISTERED_NAME_PREFIX = "parkwise:registered-profile-name:";
const AUTH_SESSION_STORAGE_KEY = "parkwise:auth-session";

export type ActiveAuthSession = {
    account: LoginResponse["account"];
    refreshToken?: string;
    displayName: string;
};

let activeSession: ActiveAuthSession | null = null;
let sessionHydrated = false;
const listeners = new Set<() => void>();

function emitChange() {
    for (const listener of listeners) listener();
}

function hydrateSessionIfNeeded() {
    if (sessionHydrated || typeof window === "undefined") return;
    sessionHydrated = true;
    try {
        const raw =
            window.sessionStorage.getItem(AUTH_SESSION_STORAGE_KEY) ||
            window.localStorage.getItem(AUTH_SESSION_STORAGE_KEY);
        if (!raw) return;
        const parsed = JSON.parse(raw) as ActiveAuthSession;
        if (parsed && parsed.account?.email) {
            delete parsed.refreshToken;
            activeSession = parsed;
        } else {
            window.sessionStorage.removeItem(AUTH_SESSION_STORAGE_KEY);
            window.localStorage.removeItem(AUTH_SESSION_STORAGE_KEY);
        }
    } catch {
        // Ignore corrupted session storage
    }
}

export function subscribeAuthSession(listener: () => void) {
    hydrateSessionIfNeeded();
    listeners.add(listener);
    return () => listeners.delete(listener);
}

export function getAuthSessionSnapshot() {
    hydrateSessionIfNeeded();
    return activeSession;
}

export function setAuthSession(response: LoginResponse) {
    sessionHydrated = true;
    setAccessToken(response.accessToken);
    const displayName = readRegisteredProfileName(response.account.email);
    activeSession = {
        account: response.account,
        displayName,
    };
    if (typeof window !== "undefined") {
        try {
            // Strictly omit refreshToken from storage to prevent XSS exfiltration.
            // HttpOnly cookie handles refresh via credentials: "include".
            const sessionToPersist = {
                account: activeSession.account,
                displayName: activeSession.displayName,
            };
            const serialized = JSON.stringify(sessionToPersist);
            window.sessionStorage.setItem(AUTH_SESSION_STORAGE_KEY, serialized);
            window.localStorage.setItem(AUTH_SESSION_STORAGE_KEY, serialized);
        } catch {
            // Ignore storage errors
        }
    }
    emitChange();
    return activeSession;
}

export function clearAuthSession() {
    sessionHydrated = true;
    clearAccessToken();
    activeSession = null;
    if (typeof window !== "undefined") {
        try {
            window.sessionStorage.removeItem(AUTH_SESSION_STORAGE_KEY);
            window.localStorage.removeItem(AUTH_SESSION_STORAGE_KEY);
        } catch {
            // Ignore storage errors
        }
    }
    emitChange();
}

export function rememberRegisteredProfileName(
    email: string,
    fullName: string | null | undefined,
) {
    if (!fullName?.trim()) return;
    try {
        window.sessionStorage.setItem(
            `${REGISTERED_NAME_PREFIX}${email.trim().toLowerCase()}`,
            fullName.trim(),
        );
    } catch {
        // A missing cached name should never interrupt registration.
    }
}

export function readRegisteredProfileName(email: string) {
    try {
        return (
            window.sessionStorage
                .getItem(
                    `${REGISTERED_NAME_PREFIX}${email.trim().toLowerCase()}`,
                )
                ?.trim() ?? ""
        );
    } catch {
        return "";
    }
}

registerAuthRefreshHandler(async () => {
    hydrateSessionIfNeeded();
    try {
        const response = await fetch(
            new URL(
                "/api/identity/accounts/refresh",
                `${getGatewayBaseUrl()}/`,
            ),
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    Accept: "application/json",
                },
                credentials: "include",
                body: "{}",
            },
        );
        if (!response.ok) {
            if ([401, 403].includes(response.status)) {
                clearAuthSession();
            }
            return null;
        }
        const next = (await response.json()) as LoginResponse;
        setAuthSession(next);
        return next.accessToken;
    } catch {
        return null;
    }
});
