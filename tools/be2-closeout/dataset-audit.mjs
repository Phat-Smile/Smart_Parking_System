#!/usr/bin/env node
/** Anonymous camera-dataset leakage checks, no image/model IO and no raw IDs in reports. */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const VALID_SPLITS = new Set(['train','validation','test']);
const HASH = /^[a-f0-9]{16,64}$/;
const SHA = /^[a-f0-9]{64}$/;
const PHASH = /^[a-f0-9]{16}$/;
export function hamming64(a,b) { let x=BigInt(`0x${a}`)^BigInt(`0x${b}`), n=0;while(x){x &= x-1n;n++;}return n; }
export function checkDataset(raw, options={}) {
 if(!Array.isArray(raw)||!raw.length||raw.length>2000) throw Error('sample count must be 1..2000');
 const n=options.nearHamming??4;
 if(!Number.isInteger(n)||n<0||n>12)throw Error('nearHamming must be 0..12');
 const seen=new Set(), subject=new Map(), session=new Map(), exact=new Map();
 const counts={}, classes=new Set(); let missingGroups=0, missingFingerprints=0, errors=[];
 const pHashes=[];
 for(const row of raw) {
  if(!row||typeof row!=='object'||!VALID_SPLITS.has(row.split)||!(/^[a-z][a-z0-9_-]{1,31}$/).test(row.vehicle_type??'')) throw Error('invalid split or vehicle_type');
  if(typeof row.id!=='string'||!/^anon-[a-zA-Z0-9_-]{1,75}$/.test(row.id))throw Error('sample IDs must use anonymous anon- prefix');
  if(seen.has(row.id))errors.push('duplicate anonymous sample id');seen.add(row.id);
  if(typeof row.image!=='string'||!row.image||path.posix.isAbsolute(row.image)||/^[a-zA-Z]:/.test(row.image)||row.image.startsWith('//')||row.image.includes('\\')||row.image.split('/').some(t=>!t||t==='.'||t==='..')||!/\.(png|jpe?g|webp)$/i.test(row.image))throw Error('invalid image relative path');
  classes.add(row.vehicle_type);counts[row.vehicle_type]??={train:0,validation:0,test:0};counts[row.vehicle_type][row.split]++;
  for(const [field,map] of [['vehicle_identity_hash',subject],['capture_session_hash',session]]) {
   const h=row[field];if(typeof h!=='string'||!HASH.test(h)){missingGroups++;continue;}
   const prior=map.get(h);if(prior&&prior!==row.split) errors.push(`${field} shared across train/validation/test`); else map.set(h,row.split);
  }
  const sha=row.image_sha256;if(typeof sha!=='string'||!SHA.test(sha)){missingFingerprints++;} else {if(exact.has(sha))errors.push('duplicate image bytes across dataset');exact.set(sha,row.split);}
  const p=row.perceptual_hash;if(typeof p!=='string'||!PHASH.test(p)){missingFingerprints++;} else pHashes.push({p,split:row.split});
 }
 // Compare only cross-split near duplicates; identities never appear in the aggregate report.
 let near=0;
 for(let i=0;i<pHashes.length;i++)for(let j=i+1;j<pHashes.length;j++)if(pHashes[i].split!==pHashes[j].split&&hamming64(pHashes[i].p,pHashes[j].p)<=n){near++;if(near<=10)errors.push('near-duplicate visual fingerprint across splits');}
 const sparse=Object.values(counts).some(v=>Math.min(v.train,v.validation,v.test)<2);
 const status=errors.length?'FAIL_LEAKAGE':missingGroups||missingFingerprints?'BLOCKED_INCOMPLETE_METADATA':sparse?'BLOCKED_TOO_FEW_SAMPLES':'PASS_SPLIT_GUARD_ONLY';
 return {status,samples:raw.length,classes:Object.keys(counts).sort(),split_counts:counts,distinct_vehicle_groups:subject.size,distinct_capture_sessions:session.size,missing_required_group_fields:missingGroups,missing_fingerprint_fields:missingFingerprints,near_duplicate_cross_split_pairs:near,error_categories:[...new Set(errors)],not_model_accuracy:true,note:'No images decoded. Perceptual hashes must be generated locally; guard does not measure vehicle classification accuracy.'};
}
function arg(flag,d){const n=process.argv.indexOf(flag);return n<0?d:process.argv[n+1];}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
 try {
 const input=arg('--input');if(!input)throw Error('--input required');
 const max=2*1024*1024;if(fs.statSync(input).size>max)throw Error('manifest exceeds 2 MiB');
 const lines=fs.readFileSync(input,'utf8').split(/\r?\n/).filter(x=>x.trim());
 const report=checkDataset(lines.map(line=>JSON.parse(line)),{nearHamming:Number(arg('--near-hamming','4'))});
 const output=arg('--output');if(output){fs.mkdirSync(path.dirname(path.resolve(output)),{recursive:true});fs.writeFileSync(output,JSON.stringify(report,null,2)+'\n');}
 console.log(`Dataset split guard: ${report.status}, samples=${report.samples}, nearPairs=${report.near_duplicate_cross_split_pairs}`);
 // Block promotion of a dataset with leakage or missing evidence. No data is ever uploaded.
 if(report.status!=='PASS_SPLIT_GUARD_ONLY')process.exitCode=3;
 }catch(e){console.error(`DATASET_CHECK_FAIL: ${e.message}`);process.exitCode=2;}
}
