import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {preflight,secureImage} from './manifest-check.mjs';
function fixture(fn){const root=fs.mkdtempSync(path.join(os.tmpdir(),'be2-anpr-'));try{const rows=[];
 for(const split of ['train','validation','test'])for(const cls of ['car','motorbike'])for(let j=0;j<2;j++){
  const id=`${split}-${cls}-${j}`,name=id+'.png';fs.writeFileSync(path.join(root,name),Buffer.from(id));
  rows.push({id,image:name,vehicle_type:cls,split,group:'day'});
 }return fn(root,rows);
 }finally{fs.rmSync(root,{recursive:true,force:true});}}
const jsonl=rows=>rows.map(x=>JSON.stringify(x)).join('\n')+'\n';
test('valid distribution reports aggregate only',()=>fixture((root,rows)=>{const r=preflight(jsonl(rows),root);assert.equal(r.samples,12);assert.deepEqual(r.classes,['car','motorbike']);assert.equal(r.split_counts.test,4);assert.equal(JSON.stringify(r).includes('train-car-0'),false)}));
test('path traversal refused',()=>fixture((root,rows)=>{rows[0].image='../outside.png';assert.throws(()=>preflight(jsonl(rows),root),/Invalid relative/)}));
test('absolute path refused',()=>fixture((root,rows)=>{rows[0].image=path.resolve(root,'train-car-0.png');assert.throws(()=>preflight(jsonl(rows),root),/Invalid relative/)}));
test('duplicate sample ID refused',()=>fixture((root,rows)=>{rows[1].id=rows[0].id;assert.throws(()=>preflight(jsonl(rows),root),/Duplicate sample id/)}));
test('duplicate image encoded content refused',()=>fixture((root,rows)=>{fs.writeFileSync(path.join(root,rows[1].image),fs.readFileSync(path.join(root,rows[0].image)));assert.throws(()=>preflight(jsonl(rows),root),/Duplicate encoded/)}));
test('missing class in validation refused',()=>fixture((root,rows)=>{rows=rows.filter(x=>!(x.split==='validation'&&x.vehicle_type==='car'));assert.throws(()=>preflight(jsonl(rows),root),/Insufficient/)}));
test('unrecognized class or split refused',()=>fixture((root,rows)=>{rows[0].vehicle_type='Motorbike';assert.throws(()=>preflight(jsonl(rows),root),/Invalid class/)}));
test('symlink escape refused',t=>fixture((root,rows)=>{const outside=path.join(os.tmpdir(),'be2-external-'+process.pid+'.png');fs.writeFileSync(outside,'external');try{
 fs.unlinkSync(path.join(root,rows[0].image));
 try{fs.symlinkSync(outside,path.join(root,rows[0].image));}
 catch(error){if(error.code==='EPERM'||error.code==='EACCES'){t.skip('Windows symlink permission unavailable');return;}throw error;}
 assert.throws(()=>secureImage(root,rows[0].image),/escapes/);
 }finally{fs.rmSync(outside,{force:true})}}));
test('duplicate path with independent IDs refused',()=>fixture((root,rows)=>{rows[1].image=rows[0].image;assert.throws(()=>preflight(jsonl(rows),root),/Same image path/)}));
test('non-image extension rejected',()=>fixture((root,rows)=>{rows[0].image='file.exe';assert.throws(()=>preflight(jsonl(rows),root),/Unsupported/)}));
test('invalid group refused',()=>fixture((root,rows)=>{rows[0].group='site/location';assert.throws(()=>preflight(jsonl(rows),root),/Invalid group/)}));
test('too many/minimum zero categories rejected',()=>fixture((root,rows)=>{rows=rows.filter(x=>x.vehicle_type==='car');assert.throws(()=>preflight(jsonl(rows),root),/Require 2-5/)}));
