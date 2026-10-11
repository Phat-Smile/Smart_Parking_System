// Keep short-lived access credentials in memory. Refresh credentials belong in
// the backend's HttpOnly cookie and must not be copied into browser storage.
let accessToken: string | undefined;

export function setAccessToken(token: string) {
    accessToken = token;
}

export function getAccessToken() {
    return accessToken;
}

export function clearAccessToken() {
    accessToken = undefined;
}
