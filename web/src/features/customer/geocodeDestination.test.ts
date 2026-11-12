import { afterEach, describe, expect, it, vi } from "vitest";
import {
    geocodeDestination,
    retrieveDestination,
    suggestDestinations,
} from "./geocodeDestination";

afterEach(() => vi.unstubAllGlobals());

describe("geocodeDestination", () => {
    it("looks up a destination using Mapbox Search Box", async () => {
        const fetchMock = vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({
                features: [{ geometry: { coordinates: [106.701, 10.775] } }],
            }),
        });
        vi.stubGlobal("fetch", fetchMock);

        await expect(
            geocodeDestination("Bến Thành", "pk.test"),
        ).resolves.toEqual({ longitude: 106.701, latitude: 10.775 });

        const url = new URL(fetchMock.mock.calls[0][0]);
        expect(url.pathname).toBe("/search/searchbox/v1/forward");
        expect(url.searchParams.get("q")).toBe("Bến Thành");
        expect(url.searchParams.get("country")).toBe("VN");
        expect(url.searchParams.get("language")).toBe("vi");
    });

    it("returns English place suggestions with a shared search session", async () => {
        const fetchMock = vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({
                suggestions: [
                    {
                        mapbox_id: "place-1",
                        name: "Ben Thanh Market",
                        place_formatted: "Ho Chi Minh City, Vietnam",
                    },
                    { mapbox_id: 123, name: "invalid" },
                ],
            }),
        });
        vi.stubGlobal("fetch", fetchMock);

        await expect(
            suggestDestinations("Ben Thanh Market", "pk.test", "session-1"),
        ).resolves.toEqual([
            {
                id: "place-1",
                name: "Ben Thanh Market",
                place: "Ho Chi Minh City, Vietnam",
            },
        ]);

        const url = new URL(fetchMock.mock.calls[0][0]);
        expect(url.pathname).toBe("/search/searchbox/v1/suggest");
        expect(url.searchParams.get("language")).toBe("en");
        expect(url.searchParams.get("session_token")).toBe("session-1");
    });

    it("retrieves coordinates for a selected suggestion in the same session", async () => {
        const fetchMock = vi.fn().mockResolvedValue({
            ok: true,
            json: async () => ({
                features: [{ geometry: { coordinates: [106.698, 10.772] } }],
            }),
        });
        vi.stubGlobal("fetch", fetchMock);

        await expect(
            retrieveDestination("place/1", "pk.test", "session-1"),
        ).resolves.toEqual({ longitude: 106.698, latitude: 10.772 });

        const url = new URL(fetchMock.mock.calls[0][0]);
        expect(url.pathname).toBe("/search/searchbox/v1/retrieve/place%2F1");
        expect(url.searchParams.get("session_token")).toBe("session-1");
    });

    it("ignores empty or invalid coordinates", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn().mockResolvedValue({
                ok: true,
                json: async () => ({ features: [] }),
            }),
        );
        await expect(
            geocodeDestination("unknown", "pk.test"),
        ).resolves.toBeNull();
    });

    it("reports Mapbox request failures", async () => {
        vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: false }));
        await expect(
            geocodeDestination("Bến Thành", "pk.test"),
        ).rejects.toThrow("Mapbox destination search failed");
    });
});
