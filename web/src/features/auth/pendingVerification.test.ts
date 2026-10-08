import { beforeEach, describe, expect, it } from "vitest";
import {
    clearPendingVerification,
    readPendingVerification,
    rememberPendingVerification,
} from "./pendingVerification";

describe("pending email verification", () => {
    beforeEach(() => window.sessionStorage.clear());

    it("keeps only the email in tab storage until verification succeeds", () => {
        expect(readPendingVerification()).toBeNull();
        expect(rememberPendingVerification("driver@example.com")).toBe(true);
        expect(readPendingVerification()).toBe("driver@example.com");
        clearPendingVerification();
        expect(readPendingVerification()).toBeNull();
    });
});
