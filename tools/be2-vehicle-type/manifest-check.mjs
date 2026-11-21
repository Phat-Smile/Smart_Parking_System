#!/usr/bin/env node
// Offline metadata/encoded-file preflight for a private local vehicle image manifest.
// This is NOT an image decoder or model acceptance gate. No network, no raw IDs or paths in output.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {pathToFileURL} from 'node:url';

const safeLabel = /^[a-z][a-z0-9_-]{1,31}$/;
const safeId = /^[A-Za-z0-9_-]{1,80}$/;
const splits = ['train','validation','test'];
export function secureImage(root, name) {
  if (typeof name !== 'string' || !name || name.includes('\\') || name.includes('\0') || path.isAbsolute(name) ||
      name.split('/').some(p=>p==='..' || p==='.' || p==='')) throw new Error('Invalid relative image path');
  if (!/\.(jpg|jpeg|png|webp)$/i.test(name)) throw new Error('Unsupported image extension');
  const base=fs.realpathSync(root);
  const real=fs.realpathSync(path.join(base,name));
  const rel=path.relative(base,real);
  if (!rel || rel.startsWith('..'+path.sep) || rel==='..' || path.isAbsolute(rel)) throw new Error('Image escapes dataset root');
  const stat=fs.statSync(real);
  if(!stat.isFile() || stat.size>8*1024*1024)throw new Error('Image missing or exceeds 8 MiB');
  return real;
}
export function preflight(text,root,{minPerSplit=2}={}) {
  if (Buffer.byteLength(text,'utf8')>2*1024*1024)throw new Error('Manifest too large');
  if(!Number.isInteger(minPerSplit)||minPerSplit<2||minPerSplit>30)throw new Error('Invalid class minimum');
  const lines=text.split(/\r?\n/).filter(x=>x.trim());
  if(!lines.length||lines.length>2000)throw new Error('Manifest sample limit exceeded or empty');
  const ids=new Set(),images=new Set(),hashes=new Set(),counts={train:0,validation:0,test:0};
  const perClass=new Map();
  for(const line of lines){
    let r;try{r=JSON.parse(line)}catch{throw new Error('Invalid JSONL');}
    if(!r||typeof r!=='object'||Array.isArray(r)||typeof r.id!=='string'||!safeId.test(r.id))throw new Error('Invalid anonymous sample id');
    if(ids.has(r.id))throw new Error('Duplicate sample id');ids.add(r.id);
    if(!safeLabel.test(r.vehicle_type)||!splits.includes(r.split))throw new Error('Invalid class/split');
    if(r.group!==undefined&&!safeLabel.test(r.group))throw new Error('Invalid group');
    const image=secureImage(root,r.image);
    if(images.has(image))throw new Error('Same image path repeated'); images.add(image);
    // Compressed-file duplicates, as an early signal. Python audit detects decoded pixel duplicates as well.
    const hash=crypto.createHash('sha256').update(fs.readFileSync(image)).digest('hex');
    if(hashes.has(hash))throw new Error('Duplicate encoded image content');hashes.add(hash);
    counts[r.split]++;
    const item=perClass.get(r.vehicle_type)||{train:0,validation:0,test:0};item[r.split]++;perClass.set(r.vehicle_type,item);
  }
  if(perClass.size<2||perClass.size>5)throw new Error('Require 2-5 vehicle classes');
  for(const [label,stat] of perClass){
    if(splits.some(s=>stat[s]<minPerSplit))throw new Error('Insufficient per-class split samples');
  }
  return {schema_version:1,status:'METADATA_PREFLIGHT_ONLY',samples:lines.length,
    classes:[...perClass.keys()].sort(),split_counts:counts,duplicate_encoded_files:0,
    warning:'Not model acceptance; Python audit also validates image decoding and duplicate pixels.'};
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
  try{
    const args=process.argv.slice(2);
    if(args.length!==6||args[0]!=='--input'||args[2]!=='--root'||args[4]!=='--output')throw new Error('Usage: --input manifest.jsonl --root images --output aggregate.json');
    if(fs.statSync(args[1]).size>2*1024*1024)throw new Error('Manifest too large');
    const report=preflight(fs.readFileSync(args[1],'utf8'),args[3]);
    const output=path.resolve(args[5]);fs.mkdirSync(path.dirname(output),{recursive:true});
    fs.writeFileSync(output,JSON.stringify(report,null,2)+'\n');
    console.log(JSON.stringify({status:report.status,samples:report.samples}));
  }catch(err){console.error('DATA_PREFLIGHT_BLOCKED: '+err.message);process.exitCode=2;}
}
