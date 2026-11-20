import { test } from 'node:test';
import { strict as assert } from 'node:assert';
import { percentile, sampleEndpoint, validateQaPort, runSampling } from './be2-qa-perf.mjs';

test('percentile is nearest rank and does not modify input', () => {
  const values = [10, 100, 20, 40];
  assert.equal(percentile(values, .95), 100);
  assert.equal(percentile(values, .50), 20);
  assert.equal(percentile([], .95), null);
  assert.deepEqual(values, [10, 100, 20, 40]);
});

test('percentile rejects negative samples', () => {
  assert.throws(() => percentile([2, -1], .95));
});

test('accepts exactly isolated QA ports', () => {
  assert.equal(validateQaPort(25002, 25002), 'http://127.0.0.1:25002');
  for (const port of [15002, 5002, 443, '25002xyz', 25003, -1])
    assert.throws(() => validateQaPort(port, 25002));
});

test('rejects non-QA URLs and non-read-only endpoints', async () => {
  await assert.rejects(() => sampleEndpoint(fetch, 'http://example.com', '/health/ready'));
  await assert.rejects(() => sampleEndpoint(fetch, 'http://127.0.0.1:25002', '/api/parking/payments'));
  await assert.rejects(() => sampleEndpoint(fetch, 'http://127.0.0.1:25002', '/health/ready', { concurrency: 100 }));
});

test('all-200 counts, no POST; no data from response included', async () => {
  const seen = [];
  const fake = async (url, opts) => {
    seen.push({ url, method: opts.method, redirect: opts.redirect });
    return { status: 200, body: { async cancel() {} } };
  };
  const result = await sampleEndpoint(fake, 'http://127.0.0.1:25002', '/health/ready');
  assert.equal(result.status, 'SAMPLED');
  assert.equal(result.succeeded, 20);
  assert.equal(result.failed, 0);
  assert.ok(result.p95Ms >= 0);
  assert.equal(seen.length, 20);
  assert.ok(seen.every(x => x.method === 'GET' && x.redirect === 'error'));
});

test('HTTP error does not look like successful latency', async () => {
  const result = await sampleEndpoint(async () => ({ status: 500 }), 'http://127.0.0.1:25002', '/health/ready');
  assert.equal(result.status, 'BLOCKED');
  assert.equal(result.p95Ms, null);
  assert.equal(result.failures.HTTP_500, 20);
});

test('mixed HTTP and timeout reports DEGRADED', async () => {
  let count = 0;
  const fake = async () => { if (++count % 2 === 0) throw new Error('timeout'); return { status: 200 }; };
  const result = await sampleEndpoint(fake, 'http://127.0.0.1:25000', '/api/parking/lots?vehicleType=Car');
  assert.equal(result.status, 'DEGRADED');
  assert.equal(result.succeeded, 10);
  assert.equal(result.failed, 10);
  assert.equal(result.failures.TRANSPORT_OR_TIMEOUT, 10);
});

test('full QA profile collects 3 endpoint summaries with p95 not NFR acceptance', async () => {
  const report = await runSampling(async () => ({ status: 200 }));
  assert.equal(report.status, 'SAMPLED');
  assert.equal(report.results.length, 3);
  assert.equal(report.results.reduce((sum, r) => sum + r.attempted, 0), 60);
  assert.equal(report.nfrAcceptance, 'NOT_EVALUATED');
});
