import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { LotArrivalForecast } from "./LotArrivalForecast";

const copy = (english: string) => english;

afterEach(() => {
    vi.unstubAllGlobals();
});

function renderForecast(fetchImpl: () => Promise<unknown>) {
    vi.stubGlobal("fetch", vi.fn(fetchImpl));
    const client = new QueryClient();
    return render(
        <QueryClientProvider client={client}>
            <LotArrivalForecast
                lotId="lot-1"
                latitude={10.77}
                longitude={106.7}
                vehicleType="Car"
                date="2026-11-13"
                hour={10}
                copy={copy}
            />
        </QueryClientProvider>,
    );
}

const recommendation = (lotId: string, probabilityPercent: number | null) => ({
    lotId,
    name: lotId,
    address: "",
    latitude: 1,
    longitude: 1,
    rank: 1,
    driveTimeMinutes: 3,
    probabilityPercent,
    price: 15000,
    warningHighFullRisk: false,
    reasons: [],
    distanceKm: 0,
});

describe("LotArrivalForecast", () => {
    it("shows the chance at the booking hour for this lot", async () => {
        renderForecast(async () => ({
            ok: true,
            json: async () => ({
                recommendedLots: [
                    recommendation("lot-1", 82),
                    recommendation("lot-2", 40),
                ],
                fallbackUsed: false,
                fallbackReason: null,
            }),
        }));
        expect(await screen.findByText("82%")).toBeInTheDocument();
        expect(screen.getByText(/10:00/)).toBeInTheDocument();
        expect(screen.getByText("AI, simulated data")).toBeInTheDocument();
    });

    it("says there is no forecast when the lot is not in the answer", async () => {
        renderForecast(async () => ({
            ok: true,
            json: async () => ({
                recommendedLots: [recommendation("other", 50)],
                fallbackUsed: false,
                fallbackReason: null,
            }),
        }));
        expect(
            await screen.findByText(/No forecast for this lot/),
        ).toBeInTheDocument();
    });
});
