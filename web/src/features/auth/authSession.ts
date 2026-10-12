import { clearAccessToken, setAccessToken } from "@/shared/api/access-token";
import type { LoginResponse } from "./loginParkWiseAccount";

const REGISTERED_NAME_PREFIX = "parkwise:registered-profile-name:";

export type ActiveAuthSession = {
    account: LoginResponse["account"];
    refreshToken: string;
    displayName: string;
};

let activeSession: ActiveAuthSession | null = null;
const listeners = new Set<() => void>();

function emitChange() {
    for (const listener of listeners) listener();
}

export function subscribeAuthSession(listener: () => void) {
    listeners.add(listener);
    return () => listeners.delete(listener);
}

export function getAuthSessionSnapshot() {
    return activeSession;
}

export function setAuthSession(response: LoginResponse) {
    setAccessToken(response.accessToken);
    const displayName = readRegisteredProfileName(response.account.email);
    activeSession = {
        account: response.account,
        refreshToken: response.refreshToken ?? "",
        displayName,
    };
    emitChange();
    return activeSession;
}

export function clearAuthSession() {
    clearAccessToken();
    activeSession = null;
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
