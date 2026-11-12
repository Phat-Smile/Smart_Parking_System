import { describe, it, expect, vi, beforeEach } from "vitest";
import {
    createVneidAuthSession,
    saveActiveVneidSession,
    getActiveVneidSession,
    approveVneidSession,
    rejectVneidSession,
    subscribeToVneidSession,
} from "./vneidSync";
import { VNEID_SEED_PROFILES } from "./vneidSeedData";

describe("vneidSync Module", () => {
    beforeEach(() => {
        window.localStorage.clear();
        vi.restoreAllMocks();
    });

    it("should create valid auth session with expiration and qr payload", () => {
        const session = createVneidAuthSession(60);
        expect(session.sessionId).toMatch(/^vneid_sess_/);
        expect(session.expiresAt).toBeGreaterThan(session.createdAt);
        expect(session.qrPayload).toContain("vneid://oauth2/authorize");
        expect(session.qrPayload).toContain(session.sessionId);

        const active = getActiveVneidSession();
        expect(active?.sessionId).toBe(session.sessionId);
    });

    it("should expire session after expiration timestamp", () => {
        const expiredSession = {
            sessionId: "vneid_sess_old",
            createdAt: Date.now() - 100000,
            expiresAt: Date.now() - 1000,
            qrPayload: "vneid://expired",
        };
        saveActiveVneidSession(expiredSession);

        const active = getActiveVneidSession();
        expect(active).toBeNull();
    });

    it("should subscribe and receive approval message across tabs", () => {
        const session = createVneidAuthSession(60);
        const onApproved = vi.fn();
        const onRejected = vi.fn();

        const unsubscribe = subscribeToVneidSession(session.sessionId, {
            onApproved,
            onRejected,
        });

        const profile = VNEID_SEED_PROFILES[0];
        approveVneidSession(session.sessionId, profile);

        // Check localStorage event triggered
        expect(
            window.localStorage.getItem("parkwise_vneid_sync_event"),
        ).toContain("VNEID_AUTH_APPROVED");

        unsubscribe();
    });

    it("should handle rejection event properly", () => {
        const session = createVneidAuthSession(60);
        const onApproved = vi.fn();
        const onRejected = vi.fn();

        const unsubscribe = subscribeToVneidSession(session.sessionId, {
            onApproved,
            onRejected,
        });

        rejectVneidSession(session.sessionId, "Người dùng đã hủy");

        expect(
            window.localStorage.getItem("parkwise_vneid_sync_event"),
        ).toContain("VNEID_AUTH_REJECTED");

        unsubscribe();
    });
});
