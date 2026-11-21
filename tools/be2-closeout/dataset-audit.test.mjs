import {createHash} from 'node:crypto';
import test from 'node:test';import assert from 'node:assert/strict';
import {checkDataset,hamming64} from './dataset-audit.mjs';
const h=n=>n.toString(16).padStart(64,'0');
const q=n=>createHash('sha256').update(String(n)).digest('hex').slice(0,16);
function records(){const rows=[];let idx=0;for(const kind of ['car','motorbike'])for(const split of ['train','validation','test'])for(let i=0;i<2;i++){idx++;rows.push({id:`anon-${idx}`,image:`public/${idx}.jpg`,vehicle_type:kind,split,vehicle_identity_hash:h(idx+100),capture_session_hash:h(idx+300),image_sha256:h(idx+500),perceptual_hash:q(idx*1000)});}return rows;}
test('healthy independent groups are accepted only as split guard',()=>{const r=checkDataset(records());assert.equal(r.status,'PASS_SPLIT_GUARD_ONLY');assert.equal(r.not_model_accuracy,true);});
test('same vehicle across test and train fails',()=>{const a=records();a.at(-1).vehicle_identity_hash=a[0].vehicle_identity_hash;assert.equal(checkDataset(a).status,'FAIL_LEAKAGE');});
test('same recording session across splits fails',()=>{const a=records();a.at(-1).capture_session_hash=a[0].capture_session_hash;assert.equal(checkDataset(a).status,'FAIL_LEAKAGE');});
test('duplicate encoded images fail',()=>{const a=records();a[10].image_sha256=a[0].image_sha256;assert.equal(checkDataset(a).status,'FAIL_LEAKAGE');});
test('near duplicate visual hashes across splits fail',()=>{const a=records();a[10].perceptual_hash=a[0].perceptual_hash;assert.equal(checkDataset(a).status,'FAIL_LEAKAGE');});
test('missing subject metadata blocks claims',()=>{const a=records();delete a[0].vehicle_identity_hash;assert.equal(checkDataset(a).status,'BLOCKED_INCOMPLETE_METADATA');});
test('missing visual hashes blocks claims',()=>{const a=records();delete a[0].perceptual_hash;assert.equal(checkDataset(a).status,'BLOCKED_INCOMPLETE_METADATA');});
test('small class holdout blocks evidence',()=>{const a=records().filter(x=>!(x.vehicle_type==='car'&&x.split==='test'&&x.id==='anon-6'));assert.equal(checkDataset(a).status,'BLOCKED_TOO_FEW_SAMPLES');});
test('reject path traversal',()=>{const a=records();a[0].image='../secret.jpg';assert.throws(()=>checkDataset(a),/image relative/);});
test('reject personally identifying ID format',()=>{const a=records();a[0].id='59A-12345';assert.throws(()=>checkDataset(a),/anonymous/);});
test('hamming distance exact',()=>{assert.equal(hamming64('0000000000000000','000000000000000f'),4);});
test('never expose image paths or identity group in report',()=>{const a=records();const text=JSON.stringify(checkDataset(a));assert.ok(!text.includes('public/1.jpg'));assert.ok(!text.includes(a[0].vehicle_identity_hash));});
