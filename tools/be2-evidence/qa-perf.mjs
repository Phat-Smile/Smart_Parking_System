#!/usr/bin/env node
// Bounded, sequential, read-only QA sampling. NOT a load test or NFR acceptance.
import fs from "node:fs";
import path from "node:path";
import { performance } from "node:perf_hooks";
import { pathToFileURL } from "node:url";

const allowedPorts = new Set(["25000", "25002", "25003", "28000"]);
const allowedPaths = new Set([
    "/health/ready",
    "/api/parking/lots",
    "/api/parking/parking-lots",
]);
export function safeQaUrl(raw) {
    let url;
    try {
        url = new URL(raw);
    } catch {
        throw new Error("Invalid URL");
    }
    if (
        url.protocol !== "http:" ||
        !["127.0.0.1", "localhost"].includes(url.hostname) ||
        !allowedPorts.has(url.port) ||
        !allowedPaths.has(url.pathname) ||
        url.username ||
        url.password ||
        url.search ||
        url.hash
    )
        throw new Error(
            "Only local read-only isolated QA endpoints are allowed",
        );
    return url;
}
export function p95(samples) {
    if (!samples.length) return null;
    const vals = [...samples].sort((a, b) => a - b);
    return (
        Math.round(vals[Math.max(0, Math.ceil(vals.length * 0.95) - 1)] * 100) /
        100
    );
}
export function summarize(samples) {
    const successes = samples.filter((s) => s.http === 200 && s.ms !== null);
    return {
        total: samples.length,
        http_200: successes.length,
        other_http: samples.length - successes.length,
        p95_ms_http_200: p95(successes.map((s) => s.ms)),
        state:
            successes.length === samples.length
                ? "SAMPLE_ONLY"
                : "DEGRADED_SAMPLE",
        warning:
            "Sequential read-only samples are not load tests, SLO pass, or TECH-14 acceptance",
    };
}
export async function probe(
    raw,
    { count = 4, timeoutMs = 2000, delayMs = 250 } = {},
) {
    const url = safeQaUrl(raw);
    if (!Number.isInteger(count) || count < 1 || count > 8)
        throw new Error("count must be 1..8");
    if (!Number.isInteger(timeoutMs) || timeoutMs < 200 || timeoutMs > 4000)
        throw new Error("timeout must be 200..4000");
    const samples = [];
    for (let i = 0; i < count; i++) {
        const t = performance.now();
        try {
            // No auth, cookies or response-body capture. GET only; redirect disabled.
            const res = await fetch(url, {
                method: "GET",
                redirect: "manual",
                signal: AbortSignal.timeout(timeoutMs),
                cache: "no-store",
            });
            samples.push({
                http: res.status,
                ms: Math.round((performance.now() - t) * 100) / 100,
            });
            await res.body?.cancel();
        } catch {
            samples.push({ http: null, ms: null });
        }
        if (i < count - 1)
            await new Promise((resolve) => setTimeout(resolve, delayMs));
    }
    return summarize(samples);
}
async function main() {
    const a = process.argv.slice(2);
    if (a.length !== 4 || a[0] !== "--url" || a[2] !== "--output")
        throw new Error(
            "Usage: --url http://127.0.0.1:25002/health/ready --output report.json",
        );
    const r = await probe(a[1]);
    fs.mkdirSync(path.dirname(path.resolve(a[3])), { recursive: true });
    fs.writeFileSync(a[3], JSON.stringify(r, null, 2) + "\n");
    console.log(
        `QA_PERF: ${r.state}, ${r.http_200}/${r.total} HTTP200; p95=${r.p95_ms_http_200}`,
    );
}
if (
    process.argv[1] &&
    import.meta.url === pathToFileURL(path.resolve(process.argv[1])).href
) {
    main().catch((e) => {
        console.error("QA_PERF_BLOCKED: " + e.message);
        process.exitCode = 2;
    });
}
