import { getGatewayBaseUrl } from "@/shared/config/env";
import { ApiError } from "@/shared/errors/api-error";
import { parseProblemDetails } from "@/shared/errors/problem-details";
import { getAccessToken } from "./access-token";

let refreshHandler: (() => Promise<string | null>) | undefined;
let refreshInFlight: Promise<string | null> | null = null;

export function registerAuthRefreshHandler(
    handler: () => Promise<string | null>,
) {
    refreshHandler = handler;
}

const PUBLIC_IDENTITY_PATHS = new Set([
    "/api/identity/accounts",
    "/api/identity/accounts/login",
    "/api/identity/accounts/google",
    "/api/identity/accounts/verify-email",
    "/api/identity/accounts/resend-otp",
    "/api/identity/accounts/forgot-password",
    "/api/identity/accounts/reset-password",
    "/api/identity/accounts/refresh",
    "/api/identity/accounts/logout",
]);

export async function gatewayFetch<T>(
    path: string,
    init?: RequestInit,
): Promise<T> {
    const response = await gatewaySend(path, init, "application/json");
    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
}

// For files the backend streams (for example an owner's document): same token and refresh handling,
// returned as a Blob so the page can open it with URL.createObjectURL.
export async function gatewayFetchBlob(
    path: string,
    init?: RequestInit,
): Promise<Blob> {
    const response = await gatewaySend(path, init, "*/*");
    return await response.blob();
}

async function gatewaySend(
    path: string,
    init: RequestInit | undefined,
    accept: string,
): Promise<Response> {
    if (!path.startsWith("/") || path.startsWith("//")) {
        throw new Error("Gateway API paths must be root-relative.");
    }
    const url = new URL(path, `${getGatewayBaseUrl()}/`);
    const gateway = new URL(getGatewayBaseUrl());
    if (url.origin !== gateway.origin) {
        throw new Error("Gateway API path cannot change origin.");
    }

    const headers = new Headers(init?.headers);
    if (!headers.has("Accept")) headers.set("Accept", accept);
    if (!headers.has("X-Correlation-ID")) {
        if (
            typeof crypto !== "undefined" &&
            typeof crypto.randomUUID === "function"
        ) {
            headers.set("X-Correlation-ID", crypto.randomUUID());
        }
    }
    const accessToken = getAccessToken();
    if (accessToken && !headers.has("Authorization")) {
        headers.set("Authorization", `Bearer ${accessToken}`);
    }
    let response = await fetch(url, {
        ...init,
        headers,
        credentials: "include",
    });
    if (
        response.status === 401 &&
        refreshHandler &&
        !PUBLIC_IDENTITY_PATHS.has(path)
    ) {
        refreshInFlight ??= refreshHandler().finally(() => {
            refreshInFlight = null;
        });
        const token = await refreshInFlight;
        if (token) {
            headers.set("Authorization", `Bearer ${token}`);
            response = await fetch(url, {
                ...init,
                headers,
                credentials: "include",
            });
        }
    }
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
            (() => {
                const seconds = Number(response.headers.get("Retry-After"));
                return Number.isFinite(seconds) && seconds > 0
                    ? Math.ceil(seconds)
                    : undefined;
            })(),
        );
    }
    return response;
}
