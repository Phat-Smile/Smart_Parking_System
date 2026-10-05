import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

vi.mock("next/dynamic", () => ({
    default: () =>
        function DeferredTechnicalScene() {
            return <span>Deferred scene</span>;
        },
}));

import { ThreeCanvas } from "@/shared/three/three-canvas";
import { MapCanvas } from "@/shared/map/map-canvas";
import {
    createRealtimeConnection,
    DEFAULT_RECONNECT_DELAYS_MS,
} from "@/shared/realtime/connection";

describe("technical client boundaries", () => {
    it("defers Three.js scene behind a client-only boundary", () => {
        render(<ThreeCanvas />);
        expect(screen.getByText("Deferred scene")).toBeInTheDocument();
    });

    it("does not mount Mapbox without a public browser token", () => {
        render(<MapCanvas />);
        expect(screen.getByText(/public Mapbox token/)).toBeInTheDocument();
    });

    it("builds but never starts a SignalR connection", () => {
        const connection = createRealtimeConnection({ hubPath: "/future-hub" });
        expect(connection.baseUrl).toBe("http://localhost:5000/future-hub");
        expect(connection.state).toBe("Disconnected");
        expect(DEFAULT_RECONNECT_DELAYS_MS).toEqual([0, 2_000, 10_000, 30_000]);
        expect(() =>
            createRealtimeConnection({ hubPath: "https://another-host/hub" }),
        ).toThrow();
    });
});
