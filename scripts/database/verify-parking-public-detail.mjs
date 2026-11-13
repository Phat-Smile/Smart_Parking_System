#!/usr/bin/env node
// Read-only QA check. USAGE: node scripts/database/verify-parking-public-detail.mjs http://localhost:5502
const base = (process.argv[2] ?? 'http://localhost:5502').replace(/\/$/, '');
const id = process.argv[3] ?? '10000000-0000-0000-0000-000000000002';
const issues = [];
async function json(path) {
  const response = await fetch(`${base}${path}`);
  if (!response.ok) throw new Error(`${path} HTTP ${response.status}`);
  return response.json();
}
function check(cond, message) { if (!cond) issues.push(message); console.log(`${cond ? 'PASS' : 'FAIL'} ${message}`); }
try {
  const catalog = await json('/api/parking/lots');
  const detail = await json(`/api/parking/lots/${id}`);
  const nearby = await json(`/api/parking/lots/nearby?latitude=${detail.latitude}&longitude=${detail.longitude}&radiusKm=5`);
  const avail = await json(`/api/parking/lots/${id}/availability`);
  const pricing = await json(`/api/parking/lots/${id}/pricing`);
  check(catalog.some(x => x.id === id && Number.isFinite(x.latitude) && Number.isFinite(x.longitude)), 'Catalog has valid marker coordinates');
  check(nearby.some(x => x.id === id && x.distanceKm === 0), 'Nearby finds the lot at own coordinates');
  check(detail.openingHours?.days?.length === 7, 'Opening hours configured for all 7 days');
  check(Array.isArray(detail.compatibility) && Array.isArray(detail.amenities), 'Detail exposes compatibility and amenities');
  check(Array.isArray(detail.bookableVehicleTypes) && !detail.bookableVehicleTypes.includes('motorbike'), 'No unsupported motorbike booking');
  check((detail.floorCount ?? 0) > 0 && (detail.carCapacity ?? 0) >= 0 && (detail.motorbikeCapacity ?? 0) >= 0, 'Layout floor and capacities present');
  check(detail.publicInfo?.description?.includes('[DEMO]') && detail.publicInfo?.photosAreIllustrative === true, 'Demo metadata is explicitly labeled');
  check(avail.car && typeof avail.car.available === 'number', 'Car current availability provided');
  check(avail.motorbike && typeof avail.motorbike.available === 'number', 'Motorbike zone availability provided');
  check(pricing.items?.some(x => x.vehicleType?.toUpperCase() === 'CAR'), 'Car price published');
  check(detail.pricing?.length === pricing.items?.length, 'Detail/pricing consistent');
  console.log(`\n${11 - issues.length}/11 checks passed (read-only, API level; not FE E2E).`);
  if (issues.length) process.exitCode = 1;
} catch (error) {
  console.error('QA verification failed:', error.message);
  process.exitCode = 2;
}
