const KEY = "parkwise.pending-verification-email";

export function rememberPendingVerification(email: string): boolean {
    try {
        window.sessionStorage.setItem(KEY, email);
        return true;
    } catch {
        return false;
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
    } catch {
        // Verification has already succeeded; storage may be unavailable.
    }
}
