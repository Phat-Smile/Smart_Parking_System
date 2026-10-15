// Keep short-lived access credentials in memory. Auth session data, including
// the refresh token returned by Identity, is also held in memory.
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
