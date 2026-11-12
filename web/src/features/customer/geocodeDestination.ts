export type MapCoordinates = {
    longitude: number;
    latitude: number;
};

export type DestinationSuggestion = {
    id: string;
    name: string;
    place: string;
};

function searchLanguage(query: string): "en" | "vi" {
    return /[^\x00-\x7F]/.test(query) ? "vi" : "en";
}

function readCoordinates(data: unknown): MapCoordinates | null {
    const coordinates = (
        data as { features?: { geometry?: { coordinates?: unknown } }[] }
    )?.features?.[0]?.geometry?.coordinates;

    if (
        !Array.isArray(coordinates) ||
        coordinates.length < 2 ||
        typeof coordinates[0] !== "number" ||
        typeof coordinates[1] !== "number" ||
        !Number.isFinite(coordinates[0]) ||
        !Number.isFinite(coordinates[1]) ||
        Math.abs(coordinates[0]) > 180 ||
        Math.abs(coordinates[1]) > 90
    ) {
        return null;
    }

    return { longitude: coordinates[0], latitude: coordinates[1] };
}

export async function suggestDestinations(
    query: string,
    token: string,
    sessionToken: string,
    signal?: AbortSignal,
): Promise<DestinationSuggestion[]> {
    const parameters = new URLSearchParams({
        q: query,
        access_token: token,
        session_token: sessionToken,
        limit: "5",
        proximity: "106.702,10.776",
        country: "VN",
        language: searchLanguage(query),
    });
    const response = await fetch(
        `https://api.mapbox.com/search/searchbox/v1/suggest?${parameters}`,
        { signal },
    );

    if (!response.ok) throw new Error("Mapbox suggestions failed");

    const data: unknown = await response.json();
    const suggestions = (data as { suggestions?: unknown })?.suggestions;
    if (!Array.isArray(suggestions)) return [];

    return suggestions
        .filter((item): item is Record<string, unknown> => {
            if (!item || typeof item !== "object") return false;
            const candidate = item as Record<string, unknown>;
            return (
                typeof candidate.mapbox_id === "string" &&
                typeof candidate.name === "string"
            );
        })
        .map((item) => ({
            id: item.mapbox_id as string,
            name: item.name as string,
            place:
                typeof item.place_formatted === "string"
                    ? item.place_formatted
                    : "",
        }));
}

export async function retrieveDestination(
    suggestionId: string,
    token: string,
    sessionToken: string,
    signal?: AbortSignal,
): Promise<MapCoordinates | null> {
    const parameters = new URLSearchParams({
        access_token: token,
        session_token: sessionToken,
    });
    const response = await fetch(
        `https://api.mapbox.com/search/searchbox/v1/retrieve/${encodeURIComponent(suggestionId)}?${parameters}`,
        { signal },
    );

    if (!response.ok) throw new Error("Mapbox destination retrieval failed");
    return readCoordinates(await response.json());
}

export async function geocodeDestination(
    query: string,
    token: string,
    signal?: AbortSignal,
): Promise<MapCoordinates | null> {
    const parameters = new URLSearchParams({
        q: query,
        access_token: token,
        limit: "1",
        proximity: "106.702,10.776",
        country: "VN",
        language: searchLanguage(query),
    });
    const response = await fetch(
        `https://api.mapbox.com/search/searchbox/v1/forward?${parameters}`,
        { signal },
    );

    if (!response.ok) {
        throw new Error("Mapbox destination search failed");
    }

    return readCoordinates(await response.json());
}
