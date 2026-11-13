import { describe, expect, it, vi } from "vitest";
import { gatewayFetch } from "@/shared/api/client";
import { getGatewayBaseUrl, parseGatewayBaseUrl } from "@/shared/config/env";
import { ApiError } from "@/shared/errors/api-error";
import { parseProblemDetails } from "@/shared/errors/problem-details";

describe("Gateway config and API client", () => {
    it("uses the local Gateway default and rejects invalid schemes", () => {
        expect(parseGatewayBaseUrl(undefined)).toBe("http://localhost:5000");
        expect(getGatewayBaseUrl()).toBe("http://localhost:5000");
        expect(() => parseGatewayBaseUrl("javascript:alert(1)")).toThrow();
        expect(() =>
            parseGatewayBaseUrl("https://user:secret@example.com"),
        ).toThrow();
    });

    it("calls only a root-relative Gateway path", async () => {
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ ok: true }), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);
        await expect(
            gatewayFetch<{ ok: boolean }>("/api/identity/health"),
        ).resolves.toEqual({ ok: true });
        expect(String(fetchMock.mock.calls[0][0])).toBe(
            "http://localhost:5000/api/identity/health",
        );
        await expect(gatewayFetch("//localhost:5001/private")).rejects.toThrow(
            "root-relative",
        );
        expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    it("preserves ProblemDetails and support identifiers without exposing detail as message", async () => {
        vi.stubGlobal(
            "fetch",
            vi.fn().mockResolvedValue(
                new Response(
                    JSON.stringify({
                        type: "about:blank",
                        title: "Request failed",
                        status: 400,
                        detail: "internal exception text",
                        instance: "/api/test",
                        traceId: "trace-123",
                        correlationId: "body-correlation",
                    }),
                    {
                        status: 400,
                        headers: {
                            "Content-Type": "application/problem+json",
                            "X-Correlation-ID": "response-correlation",
                        },
                    },
                ),
            ),
        );
        try {
            await gatewayFetch("/api/identity/test");
            throw new Error("Expected ApiError");
        } catch (error) {
            expect(error).toBeInstanceOf(ApiError);
            const apiError = error as ApiError;
            expect(apiError.message).toBe("Request failed");
            expect(apiError.message).not.toContain("internal exception");
            expect(apiError.correlationId).toBe("response-correlation");
            expect(apiError.traceId).toBe("trace-123");
            expect(apiError.problem?.detail).toBe("internal exception text");
        }
    });

    it("ignores non-object error payloads", () => {
        expect(parseProblemDetails("raw server exception")).toBeUndefined();
    });

    it("preserves validation field names without exposing them as error text", () => {
        expect(
            parseProblemDetails({
                status: 400,
                errorCode: "request.validation_failed",
                errors: {
                    PhotoUrl: ["invalid photo"],
                    LicensePlate: ["invalid plate", 42],
                },
            })?.validationErrors,
        ).toEqual({
            PhotoUrl: ["invalid photo"],
            LicensePlate: ["invalid plate"],
        });
    });

    it("attaches X-Correlation-ID header to outbound requests", async () => {
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ ok: true }), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);
        await gatewayFetch("/api/test");
        const headers = fetchMock.mock.calls[0][1]?.headers as Headers;
        expect(headers.get("X-Correlation-ID")).toBeDefined();
        expect(headers.get("X-Correlation-ID")).toMatch(
            /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i,
        );
    });
});
