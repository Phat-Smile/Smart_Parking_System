#!/usr/bin/env node
/**
 * BE-2 R7 read-only live HTTP smoke: Gateway/Parking -> OSRM/Search -> AI -> Arrival.
 * This checks running local services and contracts, NOT browser/payment/gate full E2E.
 * No writes, migrations, seed, keys, user GPS or remote hosts.
 */
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

export class ProbeProblem extends Error {}

export function localBase(input) {
  let url;
  try { url = new URL(input); } catch { throw new ProbeProblem('Invalid service base URL'); }
  if (url.protocol !== 'http:' || !['localhost', '127.0.0.1', '[::1]'].includes(url.hostname) ||
      !url.port || url.username || url.password || url.pathname !== '/' || url.search || url.hash) {
    throw new ProbeProblem('Only explicit localhost HTTP ports are supported; NEVER point QA at production');
  }
  return url.origin;
}

export class HttpProbe {
  constructor(timeoutMs = 12000) { this.timeoutMs = timeoutMs; }
  async request(base, path, params, payload) {
    const url = new URL(path, `${base}/`);
    if (params) for (const [k, v] of Object.entries(params)) url.searchParams.set(k, String(v));
    let res;
    try {
      res = await fetch(url, {
        method: payload === undefined ? 'GET' : 'POST',
        headers: { 'Accept': 'application/json', 'Content-Type': 'application/json',
          'User-Agent': 'ParkWise-BE2-R7-ReadOnly-QA/1.0' },
        body: payload === undefined ? undefined : JSON.stringify(payload),
        signal: AbortSignal.timeout(this.timeoutMs),
        redirect: 'error',
      });
    } catch {
      // Never print user-provided coordinates/query strings or tokens.
      throw new ProbeProblem('HTTP transport unavailable or timed out');
    }
    const response = await res.text();
    if (response.length > 1048576) throw new ProbeProblem('Oversize HTTP response');
    let content = null;
    if (response.length) { try { content = JSON.parse(response); } catch { /* non-JSON health body */ } }
    return { status: res.status, content };
  }
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function guid(value) {
  if (typeof value !== 'string' || !GUID.test(value)) throw new ProbeProblem('Invalid lot ID');
  return value.toLowerCase();
}
function number(value, key, min = 0, max = Infinity) {
  if (typeof value !== 'number' || !Number.isFinite(value) || value < min || value > max)
    throw new ProbeProblem(`Invalid ${key}`);
  return value;
}

export function validateRecommendations(payload, catalogIds) {
  if (!payload || !Array.isArray(payload.recommendedLots)) throw new ProbeProblem('Invalid recommendation schema');
  if (payload.recommendedLots.length > 3) throw new ProbeProblem('More than Top 3 recommendations');
  if (typeof payload.fallbackUsed !== 'boolean') throw new ProbeProblem('fallbackUsed must be boolean');
  if (payload.fallbackUsed && !payload.fallbackReason) throw new ProbeProblem('Fallback reason missing');
  if (!payload.fallbackUsed && payload.fallbackReason != null) throw new ProbeProblem('Unexpected fallback reason');
  const ids = new Set();
  for (const [i, item] of payload.recommendedLots.entries()) {
    if (!item || typeof item !== 'object') throw new ProbeProblem('Invalid recommendation item');
    const id = guid(item.lotId);
    if (ids.has(id) || !catalogIds.has(id)) throw new ProbeProblem('Duplicate/unknown approved lot in AI output');
    ids.add(id);
    if (item.rank !== i + 1) throw new ProbeProblem('Non-consecutive AI rank');
    number(item.price, 'price');
    if (item.driveTimeMinutes !== null) number(item.driveTimeMinutes, 'driving ETA');
    if (typeof item.insufficientData !== 'boolean') throw new ProbeProblem('insufficientData missing');
    if (item.probabilityPercent === null) {
      if (!item.insufficientData) throw new ProbeProblem('Unknown probability marked as reliable');
    } else {
      number(item.probabilityPercent, 'probability', 0, 100);
      if (item.insufficientData) throw new ProbeProblem('Insufficient data with trusted probability');
    }
    if (typeof item.warningHighFullRisk !== 'boolean' ||
        item.warningHighFullRisk !== (item.probabilityPercent !== null && item.probabilityPercent < 30))
      throw new ProbeProblem('Incorrect risk warning flag');
    if (!Array.isArray(item.reasons) || item.reasons.length < 1 ||
        item.reasons.some(r => typeof r !== 'string' || !r.trim()))
      throw new ProbeProblem('Missing recommendation reasons');
  }
  const [winner, ...others] = payload.recommendedLots;
  if (winner?.probabilityPercent < 30 && winner?.probabilityPercent !== null &&
      others.some(x => x.probabilityPercent !== null && x.probabilityPercent >= 30))
    throw new ProbeProblem('Risky Top 1 despite safer returned candidate');
  return payload.recommendedLots;
}

export function validateArrival(payload, id, type) {
  if (!payload || guid(payload.lotId) !== id || payload.vehicleType !== type)
    throw new ProbeProblem('Arrival forecast lot/vehicle mismatch');
  number(payload.probabilityPercent, 'arrival probability', 0, 100);
  if (!Number.isInteger(payload.sampleCount) || payload.sampleCount < 0 ||
      typeof payload.estimated !== 'boolean' || typeof payload.insufficientData !== 'boolean')
    throw new ProbeProblem('Arrival provenance/quality metadata missing');
}

export function summarize(checks) {
  const status = checks.some(x => x.status === 'FAIL') ? 'FAIL'
    : checks.some(x => x.status === 'BLOCKED') ? 'BLOCKED' : 'PASS';
  return { suite: 'BE2_R7_READONLY_LIVE_HTTP_SMOKE', status,
    counts: Object.fromEntries(['PASS', 'FAIL', 'BLOCKED'].map(s => [s, checks.filter(c => c.status === s).length])),
    checks,
    limitation: 'Read-only live HTTP; NOT browser/payment/gate E2E, LLM-mode/source-image attestation, OSRM-provider attribution, or p95 NFR' };
}

export async function runProbe(http, config, emit = () => {}) {
  const checks = [];
  function mark(step, status, detail) {
    checks.push({ step, status, detail });
    emit(`[R7][${status}] ${step}: ${detail}`);
  }
  const required = [ ['Parking', config.parking], ['Booking', config.booking], ['AI', config.ai] ];
  if (config.gateway) required.push(['Gateway', config.gateway]);
  let ready = true;
  for (const [name, base] of required) {
    try {
      const { status } = await http.request(base, '/health/ready');
      if (status === 200) mark(`readiness.${name}`, 'PASS', 'HTTP 200');
      else { ready = false; mark(`readiness.${name}`, 'BLOCKED', `HTTP ${status}`); }
    } catch (err) { ready = false; mark(`readiness.${name}`, 'BLOCKED', err instanceof ProbeProblem ? err.message : 'Request failed'); }
  }
  if (!ready) return summarize(checks);
  const arrivalAt = new Date(Date.now() + 30 * 60_000).toISOString();
  const authSample = { customerLatitude: 0, customerLongitude: 0, vehicleType: 'Car', arrivalAt,
    candidateLots: [] };
  try {
    const { status } = await http.request(config.ai, '/internal/ai/recommendations', undefined, authSample);
    const state = [401, 403].includes(status) ? 'PASS' : status === 503 ? 'BLOCKED' : 'FAIL';
    const detail = state === 'PASS' ? `HTTP ${status} unauthenticated request rejected`
      : state === 'BLOCKED' ? 'AI internal API not configured (HTTP 503)' : `Unexpected HTTP ${status}`;
    mark('ai.internal_auth', state, detail);
  } catch (err) { mark('ai.internal_auth', 'BLOCKED', 'Internal auth probe unavailable'); }

  const endpoint = config.gateway || config.parking;
  let catalog, ids;
  try {
    const response = await http.request(endpoint, '/api/parking/lots', { vehicleType: 'Car' });
    if (response.status !== 200 || !Array.isArray(response.content))
      throw new ProbeProblem(`Catalog expected HTTP 200 JSON array, got ${response.status}`);
    catalog = response.content;
    ids = new Set(catalog.map(l => guid(l.id)));
    if (!ids.size) {
      mark('catalog', 'BLOCKED', 'No approved Car lot; provide isolated demo data manually');
      return summarize(checks);
    }
    mark('catalog', 'PASS', `${ids.size} approved Car lots`);
  } catch (err) { mark('catalog', 'FAIL', err.message); return summarize(checks); }

  let origin, search;
  try {
    const first = catalog[0];
    origin = config.latitude === undefined ? {
      latitude: number(Number(first.latitude), 'lot latitude', -90, 90) + 0.003,
      longitude: number(Number(first.longitude), 'lot longitude', -180, 180) + 0.003,
    } : { latitude: config.latitude, longitude: config.longitude };
    number(origin.latitude, 'latitude', -90, 90);
    number(origin.longitude, 'longitude', -180, 180);
    const result = await http.request(endpoint, '/api/parking/lots/search', {
      ...origin, radiusKm: config.radiusKm, vehicleType: 'Car', limit: 10, sort: 'distance' });
    if (result.status !== 200 || !Array.isArray(result.content))
      throw new ProbeProblem(`Search expected HTTP 200 JSON array, got ${result.status}`);
    if (result.content.length === 0) {
      mark('search', 'BLOCKED', 'No searchable lots near synthetic origin');
      return summarize(checks);
    }
    search = new Map(result.content.map(item => [guid(item.id), item]));
    if ([...search.keys()].some(id => !ids.has(id))) throw new ProbeProblem('Search returned unapproved lot');
    const routed = [...search.values()].filter(item => item.drivingTimeMinutes !== null);
    for (const item of routed) number(item.drivingTimeMinutes, 'Search ETA');
    mark('search', 'PASS', `${search.size} searchable lots; ${routed.length} OSRM ETA(s) available`);
  } catch (err) { mark('search', 'FAIL', err.message); return summarize(checks); }

  let chosenId = guid(catalog[0].id);
  try {
    const response = await http.request(endpoint, '/api/parking/lots/recommendations', {
      ...origin, radiusKm: config.radiusKm, vehicleType: 'Car', durationMinutes: 120, topK: 3,
      priority: 'balanced', arrivalAt });
    if (response.status !== 200) throw new ProbeProblem(`Recommendation HTTP ${response.status}`);
    const items = validateRecommendations(response.content, ids);
    if (!items.length) mark('recommendation', 'BLOCKED', 'No eligible priced candidates');
    else if (response.content.fallbackUsed) mark('recommendation', 'BLOCKED', 'Non-AI fallback, full AI path not proved');
    else mark('recommendation', 'PASS', `${items.length} valid AI recommendations`);
    if (items.length) {
      chosenId = guid(items[0].lotId);
      let comparable = 0;
      for (const item of items) {
        const fromSearch = search.get(guid(item.lotId))?.drivingTimeMinutes;
        if (fromSearch !== undefined && fromSearch !== null && item.driveTimeMinutes !== null) {
          comparable++;
          if (Math.abs(fromSearch - item.driveTimeMinutes) > 0.11)
            throw new ProbeProblem('Search vs Recommendation OSRM ETA mismatch');
        }
      }
      if (comparable) mark('osrm_consistency', 'PASS', `${comparable} common ETA(s) match`);
      else mark('osrm_consistency', 'BLOCKED', 'No comparable non-null OSRM ETA; routing not verified');
    }
  } catch (err) { mark('recommendation', 'FAIL', err.message); return summarize(checks); }

  try {
    const { status, content } = await http.request(endpoint,
      `/api/parking/lots/${chosenId}/arrival-availability`, { arrivalAt, vehicleType: 'Car' });
    if (status === 503) mark('arrival_availability', 'BLOCKED', 'Forecast dependency returned 503');
    else if (status !== 200) mark('arrival_availability', 'FAIL', `Expected HTTP 200; got ${status}`);
    else { validateArrival(content, chosenId, 'Car'); mark('arrival_availability', 'PASS', 'Valid live forecast contract'); }
  } catch (err) { mark('arrival_availability', 'FAIL', err.message); }
  return summarize(checks);
}

function parseArgs(argv) {
  const args = { parking: process.env.BE2_R7_PARKING_URL || 'http://127.0.0.1:5002',
    booking: process.env.BE2_R7_BOOKING_URL || 'http://127.0.0.1:5003',
    ai: process.env.BE2_R7_AI_URL || 'http://127.0.0.1:8000',
    gateway: process.env.BE2_R7_GATEWAY_URL === 'none' ? undefined : (process.env.BE2_R7_GATEWAY_URL || 'http://127.0.0.1:5000'),
    radiusKm: 10, report: '', timeoutMs: 12000 };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i], v = argv[++i];
    if (!['--parking', '--booking', '--ai', '--gateway', '--latitude', '--longitude', '--radius-km', '--timeout-ms', '--report'].includes(k) || v === undefined)
      throw new ProbeProblem('Unknown argument or missing value');
    if (k === '--parking') args.parking = v;
    if (k === '--booking') args.booking = v;
    if (k === '--ai') args.ai = v;
    if (k === '--gateway') args.gateway = v;
    if (k === '--latitude') args.latitude = Number(v);
    if (k === '--longitude') args.longitude = Number(v);
    if (k === '--radius-km') args.radiusKm = Number(v);
    if (k === '--timeout-ms') args.timeoutMs = Number(v);
    if (k === '--report') args.report = v;
  }
  if ((args.latitude === undefined) !== (args.longitude === undefined)) throw new ProbeProblem('Latitude and longitude must be paired');
  number(args.radiusKm, 'radiusKm', 0.001, 50);
  number(args.timeoutMs, 'timeoutMs', 100, 60000);
  if (args.latitude !== undefined) {
    number(args.latitude, 'latitude', -90, 90);
    number(args.longitude, 'longitude', -180, 180);
  }
  for (const k of ['parking', 'booking', 'ai', 'gateway']) if (args[k]) args[k] = localBase(args[k]);
  return args;
}

export async function cli(argv) {
  let args;
  try { args = parseArgs(argv); }
  catch (err) { console.error(`[R7][INPUT] ${err.message}`); return 2; }
  const result = await runProbe(new HttpProbe(args.timeoutMs), args, line => console.log(line));
  if (args.report) {
    const file = resolve(args.report);
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, JSON.stringify(result, null, 2) + '\n', { encoding: 'utf8', mode: 0o600 });
  }
  console.log(`[R7] RESULT=${result.status}; ${JSON.stringify(result.counts)}`);
  return { PASS: 0, FAIL: 2, BLOCKED: 3 }[result.status];
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  process.exitCode = await cli(process.argv.slice(2));
}
