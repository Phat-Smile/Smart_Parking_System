import { getGatewayBaseUrl } from "@/shared/config/env";
import { ApiError } from "@/shared/errors/api-error";
import { parseProblemDetails } from "@/shared/errors/problem-details";
import { getAccessToken } from "./access-token";

export async function gatewayFetch<T>(
    path: string,
    init?: RequestInit,
): Promise<T> {
    if (!path.startsWith("/") || path.startsWith("//")) {
        throw new Error("Gateway API paths must be root-relative.");
    }
    const url = new URL(path, `${getGatewayBaseUrl()}/`);
    const gateway = new URL(getGatewayBaseUrl());
    if (url.origin !== gateway.origin) {
        throw new Error("Gateway API path cannot change origin.");
    }

    const headers = new Headers(init?.headers);
    if (!headers.has("Accept")) headers.set("Accept", "application/json");
    const accessToken = getAccessToken();
    if (accessToken && !headers.has("Authorization")) {
        headers.set("Authorization", `Bearer ${accessToken}`);
    }
    const response = await fetch(url, { ...init, headers });
    const correlationHeader =
        response.headers.get("X-Correlation-ID") || undefined;
    if (!response.ok) {
        const contentType = response.headers.get("content-type") || "";
        const body: unknown = contentType.includes("json")
            ? await response.json().catch(() => undefined)
            : undefined;
        const problem = parseProblemDetails(body);
        throw new ApiError(
            response.status,
            problem,
            correlationHeader || problem?.correlationId,
        );
    }
    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
}
