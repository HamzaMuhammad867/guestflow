// k6 load test: simulates front-desk screens polling availability and staff logging requests.
// Run:  k6 run load/availability-load-test.js            (against http://localhost:3000)
//       BASE_URL=https://<your-azure-url> k6 run load/availability-load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const BASE = __ENV.BASE_URL || 'http://localhost:3000';
export const cacheHits = new Rate('cache_hit_rate');

export const options = {
  scenarios: {
    front_desk_polling: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '30s', target: 50 },  // ramp up to 50 concurrent screens
        { duration: '1m', target: 50 },   // hold
        { duration: '15s', target: 0 },   // ramp down
      ],
      exec: 'pollAvailability',
    },
    staff_requests: {
      executor: 'constant-arrival-rate',
      rate: 5, timeUnit: '1s', duration: '1m45s',
      preAllocatedVUs: 10,
      exec: 'createRequest',
    },
  },
  // Service-level objectives – the test fails if these are breached
  thresholds: {
    http_req_failed: ['rate<0.01'],                                   // < 1% errors
    'http_req_duration{scenario:front_desk_polling}': ['p(95)<200'],  // p95 < 200 ms (cached)
    'http_req_duration{scenario:staff_requests}': ['p(95)<500'],      // p95 < 500 ms (writes)
    cache_hit_rate: ['rate>0.9'],                                     // Redis serves > 90%
  },
};

export function pollAvailability() {
  const res = http.get(`${BASE}/api/rooms/availability`);
  check(res, { 'status 200': (r) => r.status === 200 });
  cacheHits.add(res.headers['X-Cache'] === 'HIT');
  sleep(1);
}

const categories = ['Housekeeping', 'Maintenance', 'Dining', 'Concierge'];
export function createRequest() {
  const room = `${1 + Math.floor(Math.random() * 4)}0${1 + Math.floor(Math.random() * 9)}`;
  const res = http.post(`${BASE}/api/requests`, JSON.stringify({
    roomNumber: room,
    category: categories[Math.floor(Math.random() * categories.length)],
    description: 'Load test request',
  }), { headers: { 'Content-Type': 'application/json' } });
  check(res, { 'created 201': (r) => r.status === 201 });
}
