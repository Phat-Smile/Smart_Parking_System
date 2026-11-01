const KEY = "parkwise.pending-verification-email";
const COOLDOWN_KEY = "parkwise.pending-verification-resend-at";
const EXPIRY_KEY = "parkwise.pending-verification-expires-at";
const ROLE_KEY = "parkwise.pending-verification-role";
const CRED_KEY = "parkwise.pending-verification-cred";

// Identity currently issues registration verification codes for five minutes.
export const VERIFICATION_CODE_LIFETIME_SECONDS = 5 * 60;

export function rememberPendingVerification(
    email: string,
    cooldownSeconds = 0,
    expiresInSeconds = 0,
): boolean {
    try {
        const previousEmail = window.sessionStorage.getItem(KEY);
        window.sessionStorage.setItem(KEY, email);
        if (
            previousEmail &&
            previousEmail.toLowerCase() !== email.toLowerCase()
        ) {
            window.sessionStorage.removeItem(COOLDOWN_KEY);
            window.sessionStorage.removeItem(EXPIRY_KEY);
            window.sessionStorage.removeItem(ROLE_KEY);
            window.sessionStorage.removeItem(CRED_KEY);
        }
        if (cooldownSeconds > 0) {
            window.sessionStorage.setItem(
                COOLDOWN_KEY,
                String(Date.now() + cooldownSeconds * 1000),
            );
        }
        if (expiresInSeconds > 0)
            rememberPendingVerificationExpiration(expiresInSeconds);
        return true;
    } catch {
        return false;
    }
}

export function rememberPendingCredentials(
    email: string,
    password: string,
): boolean {
    try {
        window.sessionStorage.setItem(
            CRED_KEY,
            JSON.stringify({ email, password }),
        );
        return true;
    } catch {
        return false;
    }
}

export function readPendingCredentials(): {
    email: string;
    password: string;
} | null {
    try {
        const raw = window.sessionStorage.getItem(CRED_KEY);
        if (!raw) return null;
        return JSON.parse(raw);
    } catch {
        return null;
    }
}

export function clearPendingCredentials(): void {
    try {
        window.sessionStorage.removeItem(CRED_KEY);
    } catch {
        // ignore
    }
}

export function rememberPendingRole(role: string): boolean {
    try {
        window.sessionStorage.setItem(ROLE_KEY, role);
        return true;
    } catch {
        return false;
    }
}

export function readPendingRole(): string | null {
    try {
        return window.sessionStorage.getItem(ROLE_KEY);
    } catch {
        return null;
    }
}

export function readPendingVerificationExpiration(): number | null {
    try {
        const value = window.sessionStorage.getItem(EXPIRY_KEY);
        if (value === null) return null;
        const deadline = Number(value);
        return Number.isFinite(deadline)
            ? Math.max(0, Math.ceil((deadline - Date.now()) / 1000))
            : null;
    } catch {
        return null;
    }
}

export function rememberPendingVerificationExpiration(seconds: number): void {
    try {
        window.sessionStorage.setItem(
            EXPIRY_KEY,
            String(Date.now() + Math.max(0, seconds) * 1000),
        );
    } catch {
        // The visible timer can still run for this page without session storage.
    }
}

export function readPendingVerificationCooldown(): number {
    try {
        const deadline = Number(window.sessionStorage.getItem(COOLDOWN_KEY));
        return Number.isFinite(deadline)
            ? Math.max(0, Math.ceil((deadline - Date.now()) / 1000))
            : 0;
    } catch {
        return 0;
    }
}

export function rememberPendingVerificationCooldown(seconds: number): void {
    try {
        window.sessionStorage.setItem(
            COOLDOWN_KEY,
            String(Date.now() + seconds * 1000),
        );
    } catch {
        // The visible countdown can still run without session storage.
    }
}

export function readPendingVerification(): string | null {
    try {
        return window.sessionStorage.getItem(KEY);
    } catch {
        return null;
    }
}

export function clearPendingVerification(): void {
    try {
        window.sessionStorage.removeItem(KEY);
        window.sessionStorage.removeItem(COOLDOWN_KEY);
        window.sessionStorage.removeItem(EXPIRY_KEY);
        window.sessionStorage.removeItem(ROLE_KEY);
        window.sessionStorage.removeItem(CRED_KEY);
    } catch {
        // Verification has already succeeded; storage may be unavailable.
    }
}
