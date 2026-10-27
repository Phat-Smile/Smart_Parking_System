const KEY = "parkwise.pending-password-reset";

type PendingPasswordReset = {
    email: string;
    otpCode: string;
    expiresAt: number;
};

export function rememberPendingPasswordReset(
    email: string,
    otpCode: string,
    expiresAt: number,
): void {
    window.sessionStorage.setItem(
        KEY,
        JSON.stringify({ email, otpCode, expiresAt }),
    );
}

export function readPendingPasswordReset(): PendingPasswordReset | null {
    try {
        const raw = window.sessionStorage.getItem(KEY);
        if (!raw) return null;
        const value: unknown = JSON.parse(raw);
        if (
            !value ||
            typeof value !== "object" ||
            !("email" in value) ||
            !("otpCode" in value) ||
            !("expiresAt" in value) ||
            typeof value.email !== "string" ||
            typeof value.otpCode !== "string" ||
            typeof value.expiresAt !== "number" ||
            !/^[0-9]{6}$/.test(value.otpCode) ||
            value.expiresAt <= Date.now()
        ) {
            clearPendingPasswordReset();
            return null;
        }
        return value as PendingPasswordReset;
    } catch {
        return null;
    }
}

export function clearPendingPasswordReset(): void {
    try {
        window.sessionStorage.removeItem(KEY);
    } catch {
        // Storage can be unavailable in a private browser context.
    }
}
