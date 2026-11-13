import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { CustomerLiveParkingLayout } from "../CustomerLiveParkingLayout";
import type { ParkingLayout, ZoneUtilization } from "../parkingLayoutApi";
import { ParkingTwin } from "./ParkingTwin";

const copy = (english: string) => english;

const layout: ParkingLayout = {
    lotId: "lot-1",
    version: 0,
    floors: [
        {
            id: "f1",
            name: "Ground",
            zones: [
                {
                    id: "z-car",
                    code: "CAR-A",
                    vehicleGroup: "Car",
                    capacity: 3,
                    currentVehicleCount: 1,
                    bounds: null,
                    polygon: null,
                    slots: [
                        {
                            id: "s1",
                            slotCode: "A-01",
                            effectiveStatus: "Available",
                            position: null,
                        },
                        {
                            id: "s2",
                            slotCode: "A-02",
                            effectiveStatus: "Occupied",
                            position: null,
                        },
                        {
                            id: "s3",
                            slotCode: "A-03",
                            effectiveStatus: "Reserved",
                            position: null,
                        },
                    ],
                },
                {
                    id: "z-moto",
                    code: "MOTO-B",
                    vehicleGroup: "Motorbike",
                    capacity: 20,
                    currentVehicleCount: 5,
                    bounds: null,
                    polygon: null,
                    slots: [],
                },
            ],
        },
    ],
};

const usage: ZoneUtilization = {
    lotId: "lot-1",
    layoutVersion: 0,
    zones: [
        { zoneId: "z-car", available: 1, usableCapacity: 3, inUse: 2 },
        { zoneId: "z-moto", available: 15, usableCapacity: 20, inUse: 5 },
    ],
};

afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
});

describe("ParkingTwin", () => {
    it("draws one tile per space and per motorbike tile, and sums each zone", () => {
        const { container } = render(
            <ParkingTwin floor={layout.floors[0]} usage={usage} copy={copy} />,
        );
        expect(container.querySelectorAll(".lot-twin-tile-free").length).toBe(
            1 + 15,
        );
        expect(container.querySelectorAll(".lot-twin-tile-held").length).toBe(
            1,
        );
        expect(container.querySelectorAll(".lot-twin-tile-taken").length).toBe(
            1 + 5,
        );
        expect(screen.getByText("CAR-A")).toBeInTheDocument();
        expect(screen.getByText("1/3")).toBeInTheDocument();
        expect(screen.getByText("15/20")).toBeInTheDocument();
        expect(screen.getByText(/1 held/)).toBeInTheDocument();
    });

    it("gives the drawing a text alternative with the counts", () => {
        render(
            <ParkingTwin floor={layout.floors[0]} usage={usage} copy={copy} />,
        );
        expect(
            screen.getByRole("img", {
                name: "Ground: CAR-A 1/3, MOTO-B 15/20",
            }),
        ).toBeInTheDocument();
    });
});

describe("CustomerLiveParkingLayout", () => {
    it("loads the public layout and zone usage and shows the twin", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn(async (url: string) => ({
                ok: true,
                json: async () =>
                    String(url).endsWith("/layout") ? layout : usage,
            })),
        );
        const { container } = render(
            <CustomerLiveParkingLayout lotId="lot-1" />,
        );
        await waitFor(() =>
            expect(container.querySelector(".lot-twin-svg")).not.toBeNull(),
        );
        expect(screen.getByText("Current parking layout")).toBeInTheDocument();
    });

    it("says so when the layout cannot be loaded", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn(async () => ({ ok: false, json: async () => ({}) })),
        );
        render(<CustomerLiveParkingLayout lotId="lot-1" />);
        expect(await screen.findByRole("alert")).toHaveTextContent(
            "unavailable",
        );
    });

    it("says so when the lot has no floors", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn(async (url: string) => ({
                ok: true,
                json: async () =>
                    String(url).endsWith("/layout")
                        ? { ...layout, floors: [] }
                        : usage,
            })),
        );
        render(<CustomerLiveParkingLayout lotId="lot-1" />);
        expect(
            await screen.findByText("This lot has no layout data yet."),
        ).toBeInTheDocument();
    });
});
