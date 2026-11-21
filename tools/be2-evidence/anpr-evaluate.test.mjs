import {test} from 'node:test';
import assert from 'node:assert/strict';
import {plateKey, vehicleKey, evaluateRows, applyThresholds, boundedConfidence} from './anpr-evaluate.mjs';
const make=(id,truth,plate,vehicle='car',confidence=0.9,group='day')=>({id, ground_truth:{plate:truth,vehicle_type:'car'},prediction:{plate,vehicle_type:vehicle,confidence},group});
test('normalization only removes separators, never guesses unreadable plate',()=>{
  assert.equal(plateKey('59 A-123.45'),'59A12345');
  assert.equal(plateKey('73J'),'73J');
  assert.notEqual(plateKey('73J'),'73J01234');
  assert.equal(vehicleKey(' MOTORBIKE '),'motorbike');
});
test('exact accepted plate and type use conservative matching',()=>{
 const a=evaluateRows([make('1','59A12345','59 A-123.45')]);
 assert.equal(a.plate_accuracy_accepted,1);assert.equal(a.joint_accuracy_accepted,1);
 assert.equal(a.coverage,1);assert.equal(a.status,'EVIDENCE_ONLY');
});
test('abstentions count against coverage not as correct predictions',()=>{
 const a=evaluateRows([make('1','59A12345',''),make('2','59A12345','59A12345')]);
 assert.equal(a.coverage,0.5);assert.equal(a.abstained,1);assert.equal(a.joint_accuracy_accepted,1);
});
test('missing vehicle type abstains; guessed plate cannot count',()=>{
 const row=make('1','59A12345','59A12345',null);
 assert.equal(evaluateRows([row]).accepted,0);
});
test('different vehicle type is wrong despite correct plate',()=>{
 const a=evaluateRows([make('1','59A12345','59A12345','motorbike')]);
 assert.equal(a.vehicle_accuracy_accepted,0);assert.equal(a.joint_accuracy_accepted,0);
});
test('same plate with mismatched final digit is wrong',()=>{
 assert.equal(evaluateRows([make('1','59A12345','59A12346')]).plate_accuracy_accepted,0);
});
test('duplicate ids rejected to avoid inflated accuracy',()=>{
 assert.throws(()=>evaluateRows([make('1','A1','A1'),make('1','A1','A1')]),/Duplicate/);
});
test('incomplete truth rejected',()=>{
 assert.throws(()=>evaluateRows([make('1','', '59A1')]),/ground truth/);
});
test('invalid confidence rejected',()=>{
 assert.equal(boundedConfidence(0),true);assert.equal(boundedConfidence(1),true);
 assert.throws(()=>evaluateRows([make('1','A1','A1','car',1.1)]),/Confidence/);
 assert.throws(()=>evaluateRows([make('1','A1','A1','car',NaN)]),/Confidence/);
});
test('withhold small group aggregates',()=>{
 const a=evaluateRows([make('1','A1','A1','car',.9,'night')]);
 assert.deepEqual(a.groups_above_privacy_threshold,{});
});
test('groups counted only when privacy minimum met',()=>{
 const a=evaluateRows([1,2,3,4,5].map(i=>make(''+i,'A1','A1')));
 assert.equal(a.groups_above_privacy_threshold.day.count,5);
});
test('no official thresholds means evidence-only, not PASS',()=>{
 const a=evaluateRows([make('1','A1','A1')]);assert.equal(applyThresholds(a).status,'EVIDENCE_ONLY');
});
test('explicit user thresholds can pass or fail but not claim product acceptance',()=>{
 const a=evaluateRows([make('1','A1','A1'),make('2','A1','')]);
 const t={minSamples:2,minCoverage:.8,minJointAccuracy:.8};
 assert.equal(applyThresholds(a,t).status,'BELOW_INPUT_THRESHOLDS');
 assert.equal(applyThresholds(a,{...t,minCoverage:.5}).status,'MEETS_INPUT_THRESHOLDS');
 assert.match(applyThresholds(a,{...t,minCoverage:.5}).disclaimer,/not official/);
});
test('partial threshold specification rejected',()=>{
 const a=evaluateRows([make('1','A1','A1')]);
 assert.throws(()=>applyThresholds(a,{minSamples:1}),/All three/);
});
test('empty dataset rejected rather than 100% success',()=>{
 assert.throws(()=>evaluateRows([]),/empty/);
});
test('report does not expose raw plate or sample id',()=>{
 const a=evaluateRows([make('private-uuid-999','SECRETPLATE999','SECRETPLATE999')]);
 const txt=JSON.stringify(a);assert.ok(!txt.includes('SECRETPLATE999'));assert.ok(!txt.includes('private-uuid-999'));
});
