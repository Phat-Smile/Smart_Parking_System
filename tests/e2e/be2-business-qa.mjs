/**
 * TECH-15, isolated QA: safe, bounded, read-only cross-service contract journey.
 * No credentials, geolocation, customer data, MQTT event or payment is created.
 * Explicitly NOT a substitute for the mutable Booking/Gate/Payment acceptance run.
 *
 * node tests/e2e/be2-business-qa.mjs
 * BE2_QA_GATEWAY_URL=http://127.0.0.1:25000 ... node ...
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const allowedHost = new Set(['127.0.0.1', 'localhost', '[::1]']);

export function safeLocalBase(value) {
  const url = new URL(value);
  if (!['http:', 'https:'].includes(url.protocol) || !allowedHost.has(url.hostname) ||
      url.username || url.password || url.pathname !== '/' || url.search || url.hash)
    throw new Error('QA URL must be an uncredentialed loopback origin, without query or path');
  return url.origin;
}

export function validateCatalog(data) {
  if (!Array.isArray(data)) throw new Error('Catalog must be a JSON array');
  const ids = new Set();
  for (const lot of data) {
    if (!lot || typeof lot !== 'object' || !guid.test(lot.id))
      throw new Error('Catalog lot missing valid ID');
    if (ids.has(lot.id.toLowerCase())) throw new Error('Duplicate lot ID in catalog');
    ids.add(lot.id.toLowerCase());
  }
  return data;
}

export function inspectPriceResponse(body, lotId) {
  if (!body || body.lotId?.toLowerCase() !== lotId.toLowerCase() || !Array.isArray(body.items))
    throw new Error('Pricing response has wrong lot or schema');
  for (const item of body.items) {
    if (!['CAR', 'MOTORBIKE'].includes(item.vehicleType) ||
        typeof item.price !== 'number' || !Number.isFinite(item.price) || item.price <= 0)
      throw new Error('Invalid price in lot pricing contract');
  }
}

export function inspectAvailability(body) {
  if (!body || !body.car || !Number.isInteger(body.car.available) || body.car.available < 0)
    throw new Error('Availability contract missing non-negative car slots');
}

export function summarize(checks) {
  const status = checks.some(x => x.status === 'FAIL') ? 'FAIL'
    : checks.some(x => x.status === 'BLOCKED') ? 'BLOCKED' : 'PASS';
  return { suite: 'BE2_TECH15_READ_ONLY_QA', status,
    counts: Object.fromEntries(['PASS', 'FAIL', 'BLOCKED'].map(k => [k, checks.filter(x => x.status === k).length])),
    checks,
    limitation: 'Read-only cross-service contracts; NOT reservation creation, payment, staff gate, live MQTT or full MF-01..13 E2E' };
}

export async function runReadOnlyJourney(http, base, emit = () => {}) {
  const checks = [];
  function record(step, status, detail) {
    checks.push({ step, status, detail });
    emit(`[QA][${status}] ${step}: ${detail}`);
  }
  const services = ['gateway', 'parking', 'booking', 'ai'];
  for (const name of services) {
    try {
      const response = await http(base[name], '/health/ready');
      record(`ready.${name}`, response.status === 200 ? 'PASS' : 'BLOCKED', `HTTP ${response.status}`);
    } catch { record(`ready.${name}`, 'BLOCKED', 'Service unreachable'); }
  }
  if (checks.some(x => x.status === 'BLOCKED')) return summarize(checks);

  // Gateway is the public route. Parking direct may intentionally require service credentials (403).
  let lots;
  try {
    const response = await http(base.gateway, '/api/parking/lots');
    if (response.status !== 200) {
      record('catalog', 'BLOCKED', `Gateway HTTP ${response.status}; check gateway route/auth`);
      return summarize(checks);
    }
    lots = validateCatalog(response.body);
    if (lots.length === 0) {
      record('catalog', 'BLOCKED', 'No approved lot in isolated QA seed');
      return summarize(checks);
    }
    record('catalog', 'PASS', `${lots.length} public lot(s) with stable IDs`);
  } catch (err) { record('catalog', 'FAIL', err.message); return summarize(checks); }

  const chosen = lots[0].id;
  for (const [step, path, check] of [
    ['lot_detail', `/api/parking/lots/${chosen}`, body => {
      if (body?.id?.toLowerCase() !== chosen.toLowerCase()) throw new Error('Lot detail ID mismatch');
    }],
    ['pricing', `/api/parking/lots/${chosen}/pricing`, body => inspectPriceResponse(body, chosen)],
    ['availability', `/api/parking/lots/${chosen}/availability`, inspectAvailability]
  ]) {
    try {
      const response = await http(base.gateway, path);
      if (response.status === 503) record(step, 'BLOCKED', 'Dependency unavailable (503)');
      else if (response.status !== 200) record(step, 'FAIL', `HTTP ${response.status}`);
      else { check(response.body); record(step, 'PASS', 'Response contract valid'); }
    } catch (err) { record(step, 'FAIL', err.message); }
  }

  // No customer/staff token is supplied: these private operations must not be readable anonymously.
  for (const [step, root, path] of [
    ['booking_private', base.booking, '/api/booking/reservations/me'],
    ['gate_private', base.parking, `/api/parking/gate-readings/pending?lotId=${chosen}`]
  ]) {
    try {
      const response = await http(root, path);
      if ([401, 403].includes(response.status)) record(step, 'PASS', `Unauthenticated access rejected (HTTP ${response.status})`);
      else record(step, 'FAIL', `Private endpoint unexpectedly returned HTTP ${response.status}`);
    } catch { record(step, 'BLOCKED', 'Private service route unavailable'); }
  }
  return summarize(checks);
}

async function request(root, path) {
  // Prevent SSRF and remote load in case a caller has set an unexpected variable.
  safeLocalBase(root);
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 7000);
  try {
    const response = await fetch(root + path, { method: 'GET', cache: 'no-store', redirect: 'error',
      headers: { Accept: 'application/json' }, signal: controller.signal });
    const body = response.status === 200 ? await response.json() : null;
    return { status: response.status, body };
  } finally { clearTimeout(timeout); }
}

export async function main() {
  const base = {
    gateway: safeLocalBase(process.env.BE2_QA_GATEWAY_URL || 'http://127.0.0.1:25000'),
    parking: safeLocalBase(process.env.BE2_QA_PARKING_URL || 'http://127.0.0.1:25002'),
    booking: safeLocalBase(process.env.BE2_QA_BOOKING_URL || 'http://127.0.0.1:25003'),
    ai: safeLocalBase(process.env.BE2_QA_AI_URL || 'http://127.0.0.1:28000')
  };
  const result = await runReadOnlyJourney(request, base, line => console.log(line));
  if (process.env.BE2_QA_REPORT_PATH) {
    const path = resolve(process.env.BE2_QA_REPORT_PATH);
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, JSON.stringify(result, null, 2) + '\n', { mode: 0o600 });
  }
  console.log(`[QA] RESULT=${result.status} ${JSON.stringify(result.counts)}`);
  return result.status === 'PASS' ? 0 : result.status === 'BLOCKED' ? 3 : 2;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  main().then(code => { process.exitCode = code; }, err => { console.error('[QA] INPUT ERROR', err.message); process.exitCode = 2; });
}
