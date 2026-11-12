import { describe, it, expect, beforeEach } from "vitest";
import { getOwnerViewMode, setOwnerViewMode } from "./useOwnerViewMode";

describe("useOwnerViewMode isolation", () => {
    beforeEach(() => {
        window.sessionStorage.clear();
    });

    it("defaults to live mode when nothing is stored", () => {
        expect(getOwnerViewMode()).toBe("live");
    });

    it("switches to sample mode when setOwnerViewMode('sample') is called", () => {
        setOwnerViewMode("sample");
        expect(getOwnerViewMode()).toBe("sample");
        expect(window.sessionStorage.getItem("parkwise_owner_view_mode")).toBe(
            "sample",
        );
    });

    it("switches back to live mode when setOwnerViewMode('live') is called", () => {
        setOwnerViewMode("sample");
        expect(getOwnerViewMode()).toBe("sample");

        setOwnerViewMode("live");
        expect(getOwnerViewMode()).toBe("live");
        expect(window.sessionStorage.getItem("parkwise_owner_view_mode")).toBe(
            "live",
        );
    });
});
