#!/usr/bin/env node
// Offline evaluation only: no images uploaded, no network, no raw plates in reports.
import fs from 'node:fs';
import readline from 'node:readline';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

export function plateKey(value) {
  if (typeof value !== 'string') return '';
  // Normalize separators only. DO NOT infer unreadable characters or guess a plate.
  return value.normalize('NFKC').toUpperCase().replace(/[\s.\-]/g, '');
}
export function vehicleKey(value) {
  return typeof value === 'string' ? value.trim().toLowerCase() : '';
}
export function boundedConfidence(value) {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 && value <= 1;
}
export function evaluateRows(rows, opts = {}) {
  const minGroup = opts.minGroup ?? 5;
  if (!Number.isInteger(minGroup) || minGroup < 2) throw new Error('minGroup must be >= 2');
  const unique = new Set();
  const result = { total: 0, predicted: 0, abstained: 0, plate_correct: 0, vehicle_correct: 0, joint_correct: 0,
    confidence_missing: 0, invalid_samples: 0, by_group: {}, bins: [0, 0, 0, 0, 0] };
  const groups = new Map();
  for (const row of rows) {
    if (!row || typeof row !== 'object' || Array.isArray(row) || typeof row.id !== 'string' || !row.id.trim()) {
      throw new Error('Invalid sample: id required');
    }
    if (unique.has(row.id)) throw new Error('Duplicate sample id');
    unique.add(row.id);
    const truth = row.ground_truth;
    const prediction = row.prediction;
    const truePlate = plateKey(truth?.plate);
    const trueVehicle = vehicleKey(truth?.vehicle_type);
    if (!truePlate || !trueVehicle) throw new Error('Incomplete ground truth');
    if (!prediction || typeof prediction !== 'object' || Array.isArray(prediction)) throw new Error('Missing prediction object');
    if (prediction.confidence !== null && prediction.confidence !== undefined && !boundedConfidence(prediction.confidence)) {
      throw new Error('Confidence must be between 0 and 1');
    }
    const predictedPlate = plateKey(prediction.plate);
    const predictedVehicle = vehicleKey(prediction.vehicle_type);
    const accepted = predictedPlate.length > 0 && predictedVehicle.length > 0;
    const group = typeof row.group === 'string' && /^[a-zA-Z0-9_-]{1,48}$/.test(row.group) ? row.group : 'unspecified';
    const bucket = groups.get(group) || { samples: 0, predicted: 0, joint_correct: 0 };
    bucket.samples++;
    result.total++;
    if (!accepted) { result.abstained++; groups.set(group, bucket); continue; }
    result.predicted++; bucket.predicted++;
    if (predictedPlate === truePlate) result.plate_correct++;
    if (predictedVehicle === trueVehicle) result.vehicle_correct++;
    if (predictedPlate === truePlate && predictedVehicle === trueVehicle) { result.joint_correct++; bucket.joint_correct++; }
    if (prediction.confidence === null || prediction.confidence === undefined) result.confidence_missing++;
    else result.bins[Math.min(4, Math.floor(prediction.confidence * 5))]++;
    groups.set(group, bucket);
  }
  if (!result.total) throw new Error('Dataset is empty');
  const fraction = (num, den) => den ? Math.round(num / den * 10000) / 10000 : null;
  const byGroup = {};
  for (const [key, stat] of [...groups].sort((a, b) => a[0].localeCompare(b[0]))) {
    // Do not expose small groups or individual predictions.
    if (stat.samples < minGroup) continue;
    byGroup[key] = { count: stat.samples, coverage: fraction(stat.predicted, stat.samples), joint_accuracy_accepted: fraction(stat.joint_correct, stat.predicted) };
  }
  return {
    schema_version: 1,
    status: 'EVIDENCE_ONLY',
    warning: 'Synthetic or offline predictions only. Metrics are not live camera/model acceptance and are not NFR approval.',
    samples: result.total,
    accepted: result.predicted,
    abstained: result.abstained,
    coverage: fraction(result.predicted, result.total),
    plate_accuracy_accepted: fraction(result.plate_correct, result.predicted),
    vehicle_accuracy_accepted: fraction(result.vehicle_correct, result.predicted),
    joint_accuracy_accepted: fraction(result.joint_correct, result.predicted),
    confidence_missing: result.confidence_missing,
    confidence_buckets_0_to_1: result.bins,
    groups_above_privacy_threshold: byGroup
  };
}
export function applyThresholds(report, thresholds = {}) {
  const required = ['minSamples', 'minCoverage', 'minJointAccuracy'];
  const supplied = required.filter(k => thresholds[k] !== undefined);
  if (!supplied.length) return report;
  if (supplied.length !== required.length) throw new Error('All three thresholds must be provided together');
  if (!Number.isInteger(thresholds.minSamples) || thresholds.minSamples <= 0) throw new Error('minSamples must be a positive integer');
  for (const key of ['minCoverage','minJointAccuracy']) {
    if (!boundedConfidence(thresholds[key])) throw new Error(key + ' must be between 0 and 1');
  }
  const passes = report.samples >= thresholds.minSamples && report.coverage >= thresholds.minCoverage &&
    report.joint_accuracy_accepted !== null && report.joint_accuracy_accepted >= thresholds.minJointAccuracy;
  return {...report, status: passes ? 'MEETS_INPUT_THRESHOLDS' : 'BELOW_INPUT_THRESHOLDS', thresholds, disclaimer:'Only user-provided gates, not official product acceptance'};
}
export async function loadJsonLines(filename, maxBytes=32 * 1024 * 1024) {
  const stat = fs.statSync(filename);
  if (!stat.isFile() || stat.size > maxBytes) throw new Error('Input must be a file <= 32 MiB');
  const rows=[];
  const rl=readline.createInterface({input:fs.createReadStream(filename, {encoding:'utf8'}),crlfDelay:Infinity});
  for await (const line of rl) {
    if(!line.trim()) continue;
    if(rows.length >= 50000) throw new Error('Too many samples (limit 50000)');
    let item;
    try { item=JSON.parse(line); } catch { throw new Error('Invalid JSONL input'); }
    rows.push(item);
  }
  return rows;
}
function parseArgs(args) {
  const opts={};
  for(let i=0;i<args.length;i++){
    if(!args[i].startsWith('--') || i+1===args.length) throw new Error('Expected --key value');
    const k=args[i++].slice(2);
    if (!['input','output','min-samples','min-coverage','min-joint-accuracy','min-group'].includes(k)) throw new Error('Unknown option');
    if(opts[k]!==undefined) throw new Error('Duplicate option');
    opts[k]=args[i];
  }
  if(!opts.input || !opts.output) throw new Error('Usage: --input samples.jsonl --output report.json [--min-samples N --min-coverage 0.8 --min-joint-accuracy 0.9]');
  return opts;
}
async function main() {
  const opts=parseArgs(process.argv.slice(2));
  const minGroup = opts['min-group'] === undefined ? 5 : Number(opts['min-group']);
  const data = await loadJsonLines(opts.input);
  const result=evaluateRows(data, {minGroup});
  const thresholds={};
  if(opts['min-samples']!==undefined) thresholds.minSamples=Number(opts['min-samples']);
  if(opts['min-coverage']!==undefined) thresholds.minCoverage=Number(opts['min-coverage']);
  if(opts['min-joint-accuracy']!==undefined) thresholds.minJointAccuracy=Number(opts['min-joint-accuracy']);
  const scored=applyThresholds(result,thresholds);
  // Reports are aggregate-only. Do not include source path, raw plate, sample id or image.
  fs.mkdirSync(path.dirname(path.resolve(opts.output)),{recursive:true});
  fs.writeFileSync(opts.output,JSON.stringify(scored,null,2)+'\n',{encoding:'utf8',flag:'w'});
  console.log(JSON.stringify({status:scored.status,samples:scored.samples,accepted:scored.accepted,output:path.basename(opts.output)}));
  if(scored.status==='BELOW_INPUT_THRESHOLDS') process.exitCode=2;
}
if(process.argv[1] && import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){
  main().catch(err=>{console.error('ANPR evaluation failed: '+err.message);process.exitCode=2;});
}
