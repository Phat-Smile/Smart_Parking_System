import test from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { once } from 'node:events';
import {
  localBase, ProbeProblem, HttpProbe, validateRecommendations, validateArrival,
  runProbe, summarize,
} from './be2-r7-smoke.mjs';

const A = '11111111-1111-4111-8111-111111111111';
const B = '22222222-2222-4222-8222-222222222222';
const BASE = { parking: 'http://127.0.0.1:5002', booking: 'http://127.0.0.1:5003', ai: 'http://127.0.0.1:8000', radiusKm: 10 };
function item(id=A, rank=1, p=70, eta=4.3) {
  return {lotId:id, name:'Mock lot', rank, price:18000, driveTimeMinutes:eta,
    probabilityPercent:p, insufficientData:p===null,
    warningHighFullRisk:p!==null&&p<30, reasons:['Phù hợp'], sampleCount:12};
}
const catalog = [{ id:A, latitude:10.8, longitude:106.68 }, {id:B, latitude:10.81, longitude:106.69}];
const search = [{id:A, drivingTimeMinutes:4.3}, {id:B, drivingTimeMinutes:8.2}];
class FakeHttp {
  constructor(opts={}) { this.opts=opts; this.called=[]; }
  async request(base, path, params, payload) {
    this.called.push({base,path,params,payload});
    if (path === '/health/ready') return {status:this.opts.down === base ? 503 : 200, content:{}};
    if (path === '/internal/ai/recommendations') return {status:this.opts.authStatus??403,content:{detail:'Forbidden'}};
    if (path === '/api/parking/lots') return {status:200,content:this.opts.emptyCatalog ? []:catalog};
    if (path === '/api/parking/lots/search') return {status:200,content:this.opts.noSearch ? []:
      (this.opts.noEta ? search.map(x=>({...x,drivingTimeMinutes:null})):search)};
    if (path === '/api/parking/lots/recommendations') return {status:200, content:this.opts.recommendation ??
      {recommendedLots:[item()],fallbackUsed:false,fallbackReason:null}};
    if (path.includes('arrival-availability')) return {status:this.opts.arrival503?503:200,content:
      {lotId:A,vehicleType:'Car',probabilityPercent:66.67,sampleCount:10,estimated:true,insufficientData:false}};
    throw new Error('Unexpected path '+path);
  }
}

test('URL safety: loopback explicit port only',()=>{
  assert.equal(localBase('http://localhost:5002'), 'http://localhost:5002');
  assert.throws(()=>localBase('https://localhost:5002'), ProbeProblem);
  assert.throws(()=>localBase('http://parking.example:5002'), ProbeProblem);
  assert.throws(()=>localBase('http://127.0.0.1:5002/not-root'), ProbeProblem);
  assert.throws(()=>localBase('http://user:secret@localhost:5002'), ProbeProblem);
});
test('valid AI response and nullable ETA',()=>{
  const result=validateRecommendations({recommendedLots:[item(A,1,70,null)], fallbackUsed:false,fallbackReason:null},new Set([A]));
  assert.equal(result.length,1);
});
test('reject AI hallucinated lot IDs',()=>{
  assert.throws(()=>validateRecommendations({recommendedLots:[item(B)],fallbackUsed:false,fallbackReason:null},new Set([A])), ProbeProblem);
});
test('reject warning flag inconsistent with published probability',()=>{
  const fake=item();fake.warningHighFullRisk=true;
  assert.throws(()=>validateRecommendations({recommendedLots:[fake],fallbackUsed:false,fallbackReason:null},new Set([A])),/risk warning/i);
});
test('reject unreliable probability claimed as trusted',()=>{
  const fake=item(A,1,null);fake.insufficientData=false;
  assert.throws(()=>validateRecommendations({recommendedLots:[fake],fallbackUsed:false,fallbackReason:null},new Set([A])),ProbeProblem);
});
test('reject risky top 1 before safe candidate',()=>{
  assert.throws(()=>validateRecommendations({recommendedLots:[item(A,1,10),item(B,2,80)],fallbackUsed:false,fallbackReason:null},new Set([A,B])),ProbeProblem);
});
test('reject Top 4, non-sequential rank and duplicate ID',()=>{
  assert.throws(()=>validateRecommendations({recommendedLots:[item(A,1),item(B,2),item(A,3),item(B,4)],fallbackUsed:false},new Set([A,B])),ProbeProblem);
  assert.throws(()=>validateRecommendations({recommendedLots:[item(A,2)],fallbackUsed:false,fallbackReason:null},new Set([A])),ProbeProblem);
});
test('arrival response validates sample and probability',()=>{
  validateArrival({lotId:A,vehicleType:'Car',probabilityPercent:50,sampleCount:0,estimated:true,insufficientData:true},A,'Car');
  assert.throws(()=>validateArrival({lotId:A,vehicleType:'Car',probabilityPercent:150,sampleCount:1,estimated:true,insufficientData:false},A,'Car'),ProbeProblem);
});
test('complete fake HTTP chain produces PASS and does not write',async()=>{
  const fake=new FakeHttp();const result=await runProbe(fake,BASE);
  assert.equal(result.status,'PASS');
  assert.deepEqual(fake.called.map(x=>x.path).filter(x=>x.includes('api/parking')), [
    '/api/parking/lots','/api/parking/lots/search','/api/parking/lots/recommendations',
    `/api/parking/lots/${A}/arrival-availability`]);
  assert.ok(fake.called.filter(x=>x.payload!==undefined).every(x=>x.path==='/internal/ai/recommendations'));
});
test('unavailable backend is BLOCKED not PASS',async()=>{
  const result=await runProbe(new FakeHttp({down:BASE.parking}),BASE);
  assert.equal(result.status,'BLOCKED');
});
test('empty approved catalog is BLOCKED',async()=>{
  const result=await runProbe(new FakeHttp({emptyCatalog:true}),BASE);
  assert.equal(result.status,'BLOCKED');
});
test('AI fallback cannot satisfy live AI E2E',async()=>{
  const result=await runProbe(new FakeHttp({recommendation:{recommendedLots:[item()],fallbackUsed:true,fallbackReason:'provider_unavailable'}}),BASE);
  assert.equal(result.status,'BLOCKED');
});
test('AI internal authentication wrongly open is FAIL',async()=>{
  const result=await runProbe(new FakeHttp({authStatus:200}),BASE);
  assert.equal(result.status,'FAIL');
});
test('unconfigured AI key returns BLOCKED not security FAIL',async()=>{
  const result=await runProbe(new FakeHttp({authStatus:503}),BASE);
  assert.equal(result.status,'BLOCKED');
});
test('OSRM ETA not available is BLOCKED even when HTTP succeeds',async()=>{
  const result=await runProbe(new FakeHttp({noEta:true,recommendation:{recommendedLots:[item(A,1,70,null)],fallbackUsed:false,fallbackReason:null}}),BASE);
  assert.equal(result.status,'BLOCKED');
});
test('mismatched Search/Recommendation ETA is FAIL',async()=>{
  const result=await runProbe(new FakeHttp({recommendation:{recommendedLots:[item(A,1,70,55)],fallbackUsed:false,fallbackReason:null}}),BASE);
  assert.equal(result.status,'FAIL');
});
test('invalid arrival dependency is BLOCKED',async()=>{
  const result=await runProbe(new FakeHttp({arrival503:true}),BASE);
  assert.equal(result.status,'BLOCKED');
});
test('summary prioritizes FAIL over BLOCKED',()=>{
  assert.equal(summarize([{status:'BLOCKED'},{status:'FAIL'}]).status,'FAIL');
});
test('actual local HTTP probe supports JSON responses and 403 safely',async()=>{
  const server=createServer((req,res)=>{
    res.writeHead(req.method==='POST'?403:200,{'Content-Type':'application/json'});
    res.end(JSON.stringify({ok:true}));
  });
  server.listen(0,'127.0.0.1');await once(server,'listening');
  try {
    const port=server.address().port;
    const h=new HttpProbe();
    assert.deepEqual(await h.request(`http://127.0.0.1:${port}`,'/health/ready'),{status:200,content:{ok:true}});
    assert.equal((await h.request(`http://127.0.0.1:${port}`,'/internal/ai/recommendations',undefined,{})).status,403);
  } finally { server.close(); await once(server,'close'); }
});
