import { HubConnectionBuilder } from "@microsoft/signalr";
import { afterEach, describe, expect, it, vi } from "vitest";
import { createRealtimeConnection } from "./connection";

afterEach(() => {
    vi.restoreAllMocks();
});

describe("createRealtimeConnection", () => {
    it("passes withCredentials: false to SignalR withUrl by default", () => {
        const withUrlSpy = vi.spyOn(HubConnectionBuilder.prototype, "withUrl");

        createRealtimeConnection({ hubPath: "/hubs/parking" });

        expect(withUrlSpy).toHaveBeenCalledWith(
            "http://localhost:5000/hubs/parking",
            expect.objectContaining({
                withCredentials: false,
            }),
        );
    });

    it("passes accessTokenFactory together with withCredentials: false", () => {
        const withUrlSpy = vi.spyOn(HubConnectionBuilder.prototype, "withUrl");
        const tokenFactory = () => "jwt-token";

        createRealtimeConnection({
            hubPath: "/hubs/parking",
            accessTokenFactory: tokenFactory,
        });

        expect(withUrlSpy).toHaveBeenCalledWith(
            "http://localhost:5000/hubs/parking",
            expect.objectContaining({
                withCredentials: false,
                accessTokenFactory: tokenFactory,
            }),
        );
    });

    it("rejects non-root-relative hub paths", () => {
        expect(() =>
            createRealtimeConnection({ hubPath: "https://evil.example/hub" }),
        ).toThrow("Hub path must be root-relative.");
        expect(() =>
            createRealtimeConnection({ hubPath: "//evil.example/hub" }),
        ).toThrow("Hub path must be root-relative.");
    });
});
