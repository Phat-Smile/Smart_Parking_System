import { test } from 'node:test';
import assert from 'node:assert/strict';
import { runReadOnlyJourney, safeLocalBase, inspectPriceResponse, summarize } from './be2-business-qa.mjs';

const id = '11111111-1111-4111-8111-111111111111';
const roots = Object.fromEntries(['gateway', 'parking', 'booking', 'ai'].map(n => [n, `http://127.0.0.1:${({gateway:25000,parking:25002,booking:25003,ai:28000})[n]}`]));
const response = (status, body) => ({ status, body });
function fakeHttp(root, path) {
  if (path === '/health/ready') return response(200);
  if (path === '/api/parking/lots') return response(200, [{ id }]);
  if (path === `/api/parking/lots/${id}`) return response(200, { id });
  if (path === `/api/parking/lots/${id}/pricing`) return response(200, { lotId: id, items: [{ vehicleType: 'CAR', price: 30000 }] });
  if (path === `/api/parking/lots/${id}/availability`) return response(200, { car: { available: 3 } });
  if (path.startsWith('/api/booking/reservations/me') || path.startsWith('/api/parking/gate-readings/pending')) return response(401);
  throw new Error(`Unexpected test route: ${path}`);
}

test('full read-only cross-service journey accepts healthy contract and rejects private reads', async () => {
  const result = await runReadOnlyJourney(fakeHttp, roots);
  assert.equal(result.status, 'PASS');
  assert.equal(result.counts.PASS, 10);
  assert.equal(result.counts.FAIL, 0);
});

test('gateway failure is blocked, not an application regression', async () => {
  const result = await runReadOnlyJourney((root, path) => path === '/health/ready' && root === roots.gateway
    ? response(503) : fakeHttp(root, path), roots);
  assert.equal(result.status, 'BLOCKED');
  assert.equal(result.counts.BLOCKED, 1);
});

test('missing seed data is blocked without making write requests', async () => {
  const calls = [];
  const result = await runReadOnlyJourney((root, path) => {
    calls.push(path);
    return path === '/api/parking/lots' ? response(200, []) : fakeHttp(root, path);
  }, roots);
  assert.equal(result.status, 'BLOCKED');
  assert.ok(calls.every(p => p === '/health/ready' || p === '/api/parking/lots'));
});

test('unexpected private route exposure is a failure', async () => {
  const result = await runReadOnlyJourney((root, path) => path.includes('reservations/me')
    ? response(200, []) : fakeHttp(root, path), roots);
  assert.equal(result.status, 'FAIL');
  assert.ok(result.checks.some(c => c.step === 'booking_private' && c.status === 'FAIL'));
});

test('wrong pricing lot is a contract failure', async () => {
  const result = await runReadOnlyJourney((root, path) => path.endsWith('/pricing')
    ? response(200, { lotId: '22222222-2222-4222-8222-222222222222', items: [] })
    : fakeHttp(root, path), roots);
  assert.equal(result.status, 'FAIL');
});

test('pricing rejects invalid vehicle types and negative prices', () => {
  assert.throws(() => inspectPriceResponse({ lotId: id, items: [{ vehicleType: 'SCOOTER', price: 50 }] }, id));
  assert.throws(() => inspectPriceResponse({ lotId: id, items: [{ vehicleType: 'CAR', price: -1 }] }, id));
});

test('output remains conservative if one step is blocked', () => {
  const result = summarize([{ step: 'a', status: 'PASS' }, { step: 'b', status: 'BLOCKED' }]);
  assert.equal(result.status, 'BLOCKED');
});

test('only credential-free loopback addresses are accepted', () => {
  assert.equal(safeLocalBase('http://127.0.0.1:25000'), 'http://127.0.0.1:25000');
  for (const url of ['http://example.com', 'http://127.0.0.1:5000/secret', 'http://alice:password@localhost:5000', 'https://192.168.1.10:443']) {
    assert.throws(() => safeLocalBase(url));
  }
});
