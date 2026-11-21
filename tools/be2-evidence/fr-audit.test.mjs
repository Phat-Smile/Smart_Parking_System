import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {summarize,toMarkdown,catalogPath} from './fr-audit.mjs';
const items=JSON.parse(fs.readFileSync(catalogPath,'utf8')).items;
test('read historical BE-2 scope accurately: 23 tasks and 80 SP',()=>{
 const r=summarize(items); assert.equal(r.tasks,23);assert.equal(r.sp,80);
 const after=r.items.filter(x=>x.sprint==='Sau OJT');assert.equal(after.length,1);assert.equal(after[0].sp,8);
});
test('ownership blocks Lộc items before any code changes',()=>{
 const r=summarize(items);
 for(const id of ['US-AI-001','US-PRIV-001','TECH-15']){
  assert.equal(r.items.find(x=>x.id===id).action,'DELEGATED_DO_NOT_EDIT');
 }
});
test('unclaimed ANPR is a proposal not an accepted story',()=>{
 const r=summarize(items);const a=r.items.find(x=>x.id==='US-AI-004');
 assert.equal(a.action,'PROPOSE_BE2_OWNERSHIP');assert.equal(a.evidence_status,'NOT_VERIFIED');
});
test('candidate file existence does not pass acceptance',()=>{
 const r=summarize(items,()=>true);
 assert.ok(r.items.find(x=>x.id==='US-AI-001').file_candidates.every(x=>x.exists));
 assert.ok(r.items.every(x=>x.evidence_status==='NOT_VERIFIED'));
});
test('markdown escaped and warns against assuming acceptance',()=>{
 const r=summarize(items);const t=toMarkdown(r);assert.match(t,/not functional acceptance/i);
 assert.match(t,/US-AI-004/);assert.match(t,/TECH-14/);
});
test('duplicate item codes fail rather than distort totals',()=>{
 assert.throws(()=>summarize([items[0],items[0]]),/duplicate/);
});
