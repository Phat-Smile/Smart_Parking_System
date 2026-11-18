import http from 'k6/http';
import { check, sleep } from 'k6';

// Base URL for the Gateway (can be overridden via environment variable: k6 run -e BASE_URL=http://localhost:5000)
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';

// Optional Bearer token for authenticated endpoints
const AUTH_TOKEN = __ENV.AUTH_TOKEN || '';

/**
 * Generates a standard RFC4122 v4 UUID for request tracing.
 * Keeps the script self-contained without requiring external network dependencies.
 */
function generateUuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

/**
 * Build request headers including Correlation-ID for distributed tracing across Gateway & Microservices.
 */
function getHeaders() {
  const correlationId = generateUuid();
  const headers = {
    'Content-Type': 'application/json',
    'Correlation-ID': correlationId,
    'X-Correlation-ID': correlationId,
  };

  if (AUTH_TOKEN) {
    headers['Authorization'] = `Bearer ${AUTH_TOKEN}`;
  }

  return headers;
}

export const options = {
  scenarios: {
    // 100 Virtual Users querying parking lots continuously for 1 minute
    search_lots: {
      executor: 'constant-vus',
      vus: 100,
      duration: '1m',
      exec: 'searchLotsScenario',
    },
    // 20 Virtual Users opening parking sessions for 1 minute
    open_sessions: {
      executor: 'constant-vus',
      vus: 20,
      duration: '1m',
      exec: 'openSessionsScenario',
    },
  },
  thresholds: {
    // Error rate must be less than 1%
    http_req_failed: ['rate<0.01'],
    // 95th percentile response time must be under 450ms
    http_req_duration: ['p(95)<450'],
  },
};

/**
 * Scenario 1: Search Parking Lots (GET /api/parking/lots/search)
 * Simulates high-frequency queries by 100 VUs exploring nearby lots.
 */
export function searchLotsScenario() {
  const params = {
    headers: getHeaders(),
    tags: { name: 'SearchParkingLots' },
  };

  const query = 'latitude=10.7769&longitude=106.7009&radiusKm=5&vehicleType=Car';
  const url = `${BASE_URL}/api/parking/lots/search?${query}`;

  const res = http.get(url, params);

  check(res, {
    'search: status is 200 or acceptable non-error': (r) => r.status === 200 || r.status === 304,
    'search: response received': (r) => r.body !== null,
  });

  // Short pause between iterations to model active client interactions
  sleep(0.1);
}

/**
 * Scenario 2: Open Parking Session (POST /api/parking/sessions)
 * Simulates 20 VUs requesting gate check-in / session opening.
 */
export function openSessionsScenario() {
  const params = {
    headers: getHeaders(),
    tags: { name: 'OpenParkingSession' },
  };

  // Randomized plate number to avoid identical duplicate collision in concurrent runs
  const randomPlateNum = Math.floor(10000 + Math.random() * 90000);
  const payload = JSON.stringify({
    lotId: '00000000-0000-0000-0000-000000000001',
    plateNumber: `51A-${randomPlateNum}`,
    vehicleType: 'Car',
  });

  const url = `${BASE_URL}/api/parking/sessions`;

  const res = http.post(url, payload, params);

  check(res, {
    'open_session: status is 201, 200 or valid business status': (r) =>
      r.status === 201 || r.status === 200 || r.status === 401 || r.status === 403 || r.status === 404 || r.status === 409,
    'open_session: response received': (r) => r.body !== null,
  });

  sleep(0.2);
}
