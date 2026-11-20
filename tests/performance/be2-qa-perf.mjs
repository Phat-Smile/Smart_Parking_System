#!/usr/bin/env node
/**
 * TECH-14: bounded, read-only, localhost-only QA latency sampler.
 * Does not verify the production p95 NFR, OSRM upstream attribution or Business E2E.
 * Never sends user GPS, tokens, POST, or writes to DB.
 */
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

export function validateQaPort(value, expected) {
  const p = Number(value);
  if (!Number.isInteger(p) || p !== expected)
    throw new Error(`Refusing non-QA port; expected ${expected}`);
  return `http://127.0.0.1:${p}`;
}

export function percentile(values, q) {
  if (!values.length || !Number.isFinite(q) || q < 0 || q > 1) return null;
  if (values.some(x => !Number.isFinite(x) || x < 0)) throw new Error('Invalid timing sample');
  const sorted = [...values].sort((a, b) => a - b);
  return Math.round(sorted[Math.max(0, Math.ceil(q * sorted.length) - 1)] * 100) / 100;
}

export async function sampleEndpoint(fetchFn, base, path, {
  requests = 20, concurrency = 4, timeoutMs = 4000,
} = {}) {
  if (![20, 4].includes(requests) || concurrency < 1 || concurrency > 4 || timeoutMs > 4000)
    throw new Error('Unsafe sample parameters');
  if (!/^http:\/\/127\.0\.0\.1:(25000|25002)$/.test(base))
    throw new Error('Only isolated QA localhost endpoints are allowed');
  if (!['/health/ready', '/api/parking/lots?vehicleType=Car'].includes(path))
    throw new Error('Only approved read-only endpoints are allowed');

  const samples = [];
  let cursor = 0;
  async function worker() {
    while (cursor < requests) {
      cursor++;
      const start = performance.now();
      try {
        const response = await fetchFn(`${base}${path}`, {
          method: 'GET', redirect: 'error',
          headers: { Accept: 'application/json', 'User-Agent': 'ParkWise-BE2-TECH14-QA/1.0' },
          signal: AbortSignal.timeout(timeoutMs),
        });
        samples.push({ ms: performance.now() - start, ok: response.status === 200,
          result: `HTTP_${response.status}` });
        // No response body is read, printed or stored. Cancel to release connection.
        await response.body?.cancel?.().catch?.(() => {});
      } catch {
        samples.push({ ms: performance.now() - start, ok: false, result: 'TRANSPORT_OR_TIMEOUT' });
      }
    }
  }
  await Promise.all(Array.from({ length: concurrency }, () => worker()));
  const successful = samples.filter(x => x.ok).map(x => x.ms);
  const failures = samples.reduce((acc, x) => {
    if (!x.ok) acc[x.result] = (acc[x.result] || 0) + 1;
    return acc;
  }, {});
  return {
    path, attempted: samples.length, succeeded: successful.length,
    failed: samples.length - successful.length, failures,
    p50Ms: percentile(successful, 0.50), p95Ms: percentile(successful, 0.95),
    p99Ms: percentile(successful, 0.99),
    status: successful.length === 0 ? 'BLOCKED' : successful.length === samples.length ? 'SAMPLED' : 'DEGRADED',
  };
}

export async function runSampling(fetchFn = fetch) {
  const parking = validateQaPort(process.env.BE2_QA_PARKING_PORT ?? 25002, 25002);
  const gateway = validateQaPort(process.env.BE2_QA_GATEWAY_PORT ?? 25000, 25000);
  const profiles = [
    { name: 'parking.readiness', base: parking, path: '/health/ready' },
    { name: 'parking.catalog', base: parking, path: '/api/parking/lots?vehicleType=Car' },
    { name: 'gateway.catalog', base: gateway, path: '/api/parking/lots?vehicleType=Car' },
  ];
  const results = [];
  // Serial profiles avoid bursting QA or external routing services.
  for (const item of profiles) {
    const row = await sampleEndpoint(fetchFn, item.base, item.path);
    results.push({ endpoint: item.name, ...row });
  }
  return {
    suite: 'BE2_TECH14_READONLY_QA_LATENCY_BASELINE',
    status: results.some(x => x.status === 'DEGRADED') ? 'DEGRADED' :
      results.some(x => x.status === 'BLOCKED') ? 'BLOCKED' : 'SAMPLED',
    requestsMaximum: 60, maxConcurrent: 4, origin: 'isolated-localhost-QA', results,
    nfrAcceptance: 'NOT_EVALUATED',
    limitation: 'Only bounded 20-sample per endpoint baseline; not production load, Search/OSRM p95, or full Business E2E. Thresholds pending TECH-14 acceptance criteria.',
  };
}

const isCli = process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isCli) {
  try {
    const report = await runSampling();
    for (const row of report.results)
      console.log(`[TECH14][${row.status}] ${row.endpoint}: ${row.succeeded}/${row.attempted} HTTP 200; p95=${row.p95Ms ?? 'N/A'}ms; failures=${JSON.stringify(row.failures)}`);
    console.log(`[TECH14] REPORT_JSON ${JSON.stringify(report)}`);
    console.log(`[TECH14] NFR acceptance=NOT_EVALUATED; data collection=${report.status}`);
  } catch (err) {
    console.error(`[TECH14][CONFIG_ERROR] ${err.message}`);
    process.exitCode = 2;
  }
}
