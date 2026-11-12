// Keep short-lived access credentials in memory and browser storage so page
// navigations / reloads retain the active JWT.
const ACCESS_TOKEN_STORAGE_KEY = "parkwise:access-token";
let accessToken: string | undefined;

export function setAccessToken(token: string) {
    accessToken = token;
    if (typeof window !== "undefined") {
        try {
            window.sessionStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, token);
            window.localStorage.setItem(ACCESS_TOKEN_STORAGE_KEY, token);
        } catch {
            // Ignore storage errors in restricted environments
        }
    }
}

export function getAccessToken() {
    if (!accessToken && typeof window !== "undefined") {
        try {
            const stored =
                window.sessionStorage.getItem(ACCESS_TOKEN_STORAGE_KEY) ||
                window.localStorage.getItem(ACCESS_TOKEN_STORAGE_KEY);
            if (stored && !stored.startsWith("mock-token-")) {
                accessToken = stored;
            }
        } catch {
            // Ignore storage errors
        }
    }
    return accessToken;
}

export function clearAccessToken() {
    accessToken = undefined;
    if (typeof window !== "undefined") {
        try {
            window.sessionStorage.removeItem(ACCESS_TOKEN_STORAGE_KEY);
            window.localStorage.removeItem(ACCESS_TOKEN_STORAGE_KEY);
        } catch {
            // Ignore storage errors
        }
    }
}
