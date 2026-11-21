import {test} from 'node:test';
import assert from 'node:assert/strict';
import {safeQaUrl,p95,summarize} from './qa-perf.mjs';
test('only isolated QA local URLs are permitted',()=>{
 assert.equal(safeQaUrl('http://127.0.0.1:25002/health/ready').port,'25002');
 for(const u of ['https://127.0.0.1:25002/health/ready','http://example.com:25002/health/ready',
  'http://localhost:15002/health/ready','http://localhost:25002/api/payments','http://admin:pass@localhost:25002/health/ready',
  'http://localhost:25002/health/ready?token=secret']) assert.throws(()=>safeQaUrl(u));
});
test('p95 uses nearest rank on success samples',()=>{
 assert.equal(p95([3,5,2,1,4]),5);assert.equal(p95([]),null);
});
test('HTTP 403 must never look like a successful p95 sample',()=>{
 const r=summarize([{http:200,ms:12},{http:403,ms:2}]);
 assert.equal(r.p95_ms_http_200,12);assert.equal(r.state,'DEGRADED_SAMPLE');
});
test('all-200 still evidence-only, not NFR acceptance',()=>{
 const r=summarize([{http:200,ms:12}]);assert.equal(r.state,'SAMPLE_ONLY');
 assert.match(r.warning,/not load tests/);
});
