import { gatewayFetch } from "@/shared/api/client";

export type AccountSession = {
    id: string;
    device: string;
    ipAddress: string;
    createdAtUtc: string;
    lastActivityAtUtc: string;
    isCurrent: boolean;
};

export function getAccountSessions() {
    return gatewayFetch<{ sessions: AccountSession[] }>(
        "/api/identity/accounts/sessions",
    );
}

export function revokeAccountSession(id: string) {
    return gatewayFetch<unknown>(
        `/api/identity/accounts/sessions/${encodeURIComponent(id)}`,
        { method: "DELETE" },
    );
}

export function getMfaEnabled() {
    return gatewayFetch<{ mfaEnabled: boolean }>("/api/identity/accounts/me");
}

export function setupMfa() {
    return gatewayFetch<{ secret: string; otpauthUri: string }>(
        "/api/identity/accounts/mfa/setup",
        { method: "POST" },
    );
}

function postMfa<T>(action: string, body: object) {
    return gatewayFetch<T>(`/api/identity/accounts/mfa/${action}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
    });
}

export function confirmMfa(code: string) {
    return postMfa<{ recoveryCodes: string[] }>("confirm", { code });
}

export function disableMfa(password: string, code: string) {
    return postMfa<{ message: string }>("disable", { password, code });
}

export function regenerateMfaRecoveryCodes(code: string) {
    return postMfa<{ recoveryCodes: string[] }>("recovery-codes", { code });
}
