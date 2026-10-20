const KEY = "parkwise.pending-verification-email";
const COOLDOWN_KEY = "parkwise.pending-verification-resend-at";

export function rememberPendingVerification(
    email: string,
    cooldownSeconds = 0,
): boolean {
    try {
        window.sessionStorage.setItem(KEY, email);
        if (cooldownSeconds > 0) {
            window.sessionStorage.setItem(
                COOLDOWN_KEY,
                String(Date.now() + cooldownSeconds * 1000),
            );
        }
        return true;
    } catch {
        return false;
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
    } catch {
        // Verification has already succeeded; storage may be unavailable.
    }
}
