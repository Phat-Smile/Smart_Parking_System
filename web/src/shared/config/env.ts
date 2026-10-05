const LOCAL_GATEWAY_URL = "http://localhost:5000";

export function parseGatewayBaseUrl(value: string | undefined): string {
    const candidate = value?.trim() || LOCAL_GATEWAY_URL;
    const url = new URL(candidate);
    if (
        !["http:", "https:"].includes(url.protocol) ||
        url.username ||
        url.password ||
        url.search ||
        url.hash ||
        url.pathname !== "/"
    ) {
        throw new Error(
            "NEXT_PUBLIC_API_BASE_URL must be an HTTP(S) origin without credentials.",
        );
    }
    return url.origin;
}

export function getGatewayBaseUrl(): string {
    return parseGatewayBaseUrl(process.env.NEXT_PUBLIC_API_BASE_URL);
}

export function getPublicMapboxToken(): string | undefined {
    const value = process.env.NEXT_PUBLIC_MAPBOX_ACCESS_TOKEN?.trim();
    return value || undefined;
}
