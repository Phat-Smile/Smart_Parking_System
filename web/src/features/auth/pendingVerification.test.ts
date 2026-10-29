import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
    clearPendingVerification,
    readPendingVerification,
    readPendingVerificationExpiration,
    rememberPendingVerification,
    rememberPendingVerificationExpiration,
} from "./pendingVerification";

describe("pending email verification", () => {
    beforeEach(() => window.sessionStorage.clear());
    afterEach(() => vi.useRealTimers());

    it("keeps only the email in tab storage until verification succeeds", () => {
        expect(readPendingVerification()).toBeNull();
        expect(rememberPendingVerification("driver@example.com")).toBe(true);
        expect(readPendingVerification()).toBe("driver@example.com");
        clearPendingVerification();
        expect(readPendingVerification()).toBeNull();
    });

    it("keeps the OTP expiry deadline through a page reload and clears it after verification", () => {
        vi.useFakeTimers();
        vi.setSystemTime(new Date("2026-10-15T08:00:00Z"));
        rememberPendingVerification("driver@example.com", 60, 300);
        expect(readPendingVerificationExpiration()).toBe(300);
        vi.advanceTimersByTime(61_000);
        expect(readPendingVerificationExpiration()).toBe(239);
        rememberPendingVerificationExpiration(60);
        expect(readPendingVerificationExpiration()).toBe(60);
        clearPendingVerification();
        expect(readPendingVerificationExpiration()).toBeNull();
    });
});
