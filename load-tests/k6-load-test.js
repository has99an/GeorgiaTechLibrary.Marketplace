import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const searchResponseTime = new Trend('search_response_time');
const availableBooksResponseTime = new Trend('available_books_response_time');
const orderResponseTime = new Trend('order_response_time');
const errorRate = new Rate('errors');

// Configuration
export const options = {
  stages: [
    { duration: '1m', target: 50 },   // Ramp up to 50 users
    { duration: '3m', target: 50 },   // Stay at 50 users
    { duration: '1m', target: 100 },  // Ramp up to 100 users
    { duration: '5m', target: 100 },  // Stay at 100 users (1000 req/min target)
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    // Search must be < 1 second (1000ms) for 95th percentile
    'search_response_time': ['p(95)<1000', 'p(99)<2000'],
    // Available books should be < 500ms
    'available_books_response_time': ['p(95)<500', 'p(99)<1000'],
    // Order creation should be < 2 seconds
    'order_response_time': ['p(95)<2000', 'p(99)<5000'],
    // Error rate should be < 1%
    'errors': ['rate<0.01'],
    // HTTP errors should be < 1%
    'http_req_failed': ['rate<0.01'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5004';

// Test data
const searchQueries = [
  'harry potter',
  'computer science',
  'mathematics',
  'physics',
  'engineering',
  'programming',
  'algorithms',
  'data structures',
];

// Helper function to get random search query
function getRandomSearchQuery() {
  return searchQueries[Math.floor(Math.random() * searchQueries.length)];
}

// Scenario 1: Search Performance Test
// Target: 1000 requests/min, response time < 1 second
export function searchPerformanceTest() {
  const query = getRandomSearchQuery();
  const url = `${BASE_URL}/search?query=${encodeURIComponent(query)}`;
  
  const response = http.get(url, {
    tags: { name: 'Search' },
    timeout: '5s',
  });

  const success = check(response, {
    'search status is 200': (r) => r.status === 200,
    'search response time < 1000ms': (r) => r.timings.duration < 1000,
    'search has results': (r) => {
      try {
        const body = JSON.parse(r.body);
        return Array.isArray(body) && body.length > 0;
      } catch {
        return false;
      }
    },
  });

  searchResponseTime.add(response.timings.duration);
  errorRate.add(!success);
  
  sleep(0.1); // ~10 requests per second per user = 1000 req/min with 100 users
}

// Scenario 2: Available Books Test
// Target: 500 requests/min, response time < 500ms
export function availableBooksTest() {
  const page = Math.floor(Math.random() * 10) + 1;
  const url = `${BASE_URL}/search/available?page=${page}&pageSize=20&sortBy=title&sortOrder=asc`;
  
  const response = http.get(url, {
    tags: { name: 'AvailableBooks' },
    timeout: '5s',
  });

  const success = check(response, {
    'available books status is 200': (r) => r.status === 200,
    'available books response time < 500ms': (r) => r.timings.duration < 500,
    'available books has pagination': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body.page !== undefined && body.pageSize !== undefined;
      } catch {
        return false;
      }
    },
  });

  availableBooksResponseTime.add(response.timings.duration);
  errorRate.add(!success);
  
  sleep(0.2); // ~5 requests per second per user = 500 req/min with 100 users
}

// Scenario 3: Order Creation Test (simplified - no actual order creation without auth)
// Target: 200 requests/min
export function orderTest() {
  // Just test order endpoint availability
  const url = `${BASE_URL}/orders`;
  
  const response = http.get(url, {
    tags: { name: 'Orders' },
    timeout: '5s',
  });

  const success = check(response, {
    'order endpoint responds': (r) => r.status === 200 || r.status === 401, // 401 is OK (needs auth)
    'order response time < 2000ms': (r) => r.timings.duration < 2000,
  });

  orderResponseTime.add(response.timings.duration);
  errorRate.add(!success);
  
  sleep(0.5); // ~2 requests per second per user = 200 req/min with 100 users
}

// Main test function - simulates realistic traffic mix
export default function () {
  const testType = Math.random();
  
  if (testType < 0.5) {
    // 50% of requests are searches
    searchPerformanceTest();
  } else if (testType < 0.8) {
    // 30% of requests are available books
    availableBooksTest();
  } else {
    // 20% of requests are other operations (orders, etc.)
    orderTest();
  }
}

// Setup function - runs once at the start
export function setup() {
  console.log(`Starting load test against ${BASE_URL}`);
  console.log('Target: 1000 requests/min');
  console.log('Search response time target: < 1 second (p95)');
  
  // Verify API Gateway is accessible
  const healthCheck = http.get(`${BASE_URL}/health`, { timeout: '10s' });
  if (healthCheck.status !== 200) {
    throw new Error(`API Gateway health check failed: ${healthCheck.status}`);
  }
  
  return { baseUrl: BASE_URL };
}

// Teardown function - runs once at the end
export function teardown(data) {
  console.log(`Load test completed for ${data.baseUrl}`);
}




