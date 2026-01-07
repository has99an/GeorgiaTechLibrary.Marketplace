import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const searchResponseTime = new Trend('search_response_time');
const shortSearchResponseTime = new Trend('short_search_response_time');
const complexSearchResponseTime = new Trend('complex_search_response_time');
const errorRate = new Rate('error_rate');

// Configuration
export const options = {
  stages: [
    { duration: '2m', target: 25 },   // Ramp up to 25 req/sec (1500 req/min)
    { duration: '5m', target: 25 },   // Sustain 25 req/sec for 5 minutes
    { duration: '1m', target: 0 },    // Ramp down
  ],
  thresholds: {
    // Response time must be < 200ms for 95th and 99th percentile
    'search_response_time': ['p(95)<200', 'p(99)<200'],
    'short_search_response_time': ['p(95)<200', 'p(99)<200'],
    'complex_search_response_time': ['p(95)<200', 'p(99)<200'],
    // Error rate must be < 1%
    'error_rate': ['rate<0.01'],
    // HTTP errors must be < 1%
    'http_req_failed': ['rate<0.01'],
    // Total requests should reach target
    'http_reqs': ['rate>=25'], // At least 25 requests per second
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5004';

// Test data - Short searches (single word)
const shortSearchQueries = [
  'python',
  'java',
  'fiction',
  'science',
  'history',
  'math',
  'physics',
  'engineering',
  'programming',
  'database',
  'algorithm',
  'design',
  'network',
  'security',
  'web',
  'mobile',
  'cloud',
  'data',
  'machine',
  'artificial',
];

// Test data - Complex searches (multiple words)
const complexSearchQueries = [
  'python programming',
  'computer science',
  'data structures',
  'machine learning',
  'web development',
  'software engineering',
  'database systems',
  'network security',
  'cloud computing',
  'artificial intelligence',
  'object oriented',
  'system design',
  'algorithm analysis',
  'distributed systems',
  'cyber security',
];

// Sort options for complex searches
const sortOptions = ['relevance', 'price', 'title'];

// Helper function to get random short search query
function getRandomShortQuery() {
  return shortSearchQueries[Math.floor(Math.random() * shortSearchQueries.length)];
}

// Helper function to get random complex search query
function getRandomComplexQuery() {
  return complexSearchQueries[Math.floor(Math.random() * complexSearchQueries.length)];
}

// Helper function to get random sort option
function getRandomSortOption() {
  return sortOptions[Math.floor(Math.random() * sortOptions.length)];
}

// Short search test (70% of traffic)
// Simple single-word query: GET /search?query=python
function performShortSearch() {
  const query = getRandomShortQuery();
  const url = `${BASE_URL}/search?query=${encodeURIComponent(query)}`;
  
  const response = http.get(url, {
    tags: { name: 'ShortSearch', query: query },
    timeout: '5s',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    },
  });

  const success = check(response, {
    'short search status is 200': (r) => r.status === 200,
    'short search response time < 200ms': (r) => r.timings.duration < 200,
    'short search has valid JSON': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body !== null && body !== undefined;
      } catch {
        return false;
      }
    },
    'short search has results structure': (r) => {
      try {
        const body = JSON.parse(r.body);
        // SearchService returns { books: { items: [...], page: 1, ... }, suggestions: [...] }
        // Accept both old array format and new PagedResult format
        if (Array.isArray(body)) {
          return true; // Old format (array)
        }
        if (body && typeof body === 'object') {
          // New format: check for books.items or just books
          if (body.books && (Array.isArray(body.books) || (body.books.items && Array.isArray(body.books.items)))) {
            return true;
          }
          // Also accept empty results (valid response)
          if (body.books && body.books.totalCount !== undefined) {
            return true;
          }
        }
        return false;
      } catch {
        return false;
      }
    },
  });

  const responseTime = response.timings.duration;
  searchResponseTime.add(responseTime);
  shortSearchResponseTime.add(responseTime);
  // Only count as error if status is not 200 (allow empty results)
  errorRate.add(response.status !== 200);
  
  // Minimal think time to achieve target throughput
  sleep(0.1);
}

// Complex search test (30% of traffic)
// Multiple words + query parameters: GET /search?query=python programming&page=1&pageSize=20&sortBy=price
function performComplexSearch() {
  const query = getRandomComplexQuery();
  const page = Math.floor(Math.random() * 5) + 1; // Random page 1-5
  const pageSize = 20;
  const sortBy = getRandomSortOption();
  
  const url = `${BASE_URL}/search?query=${encodeURIComponent(query)}&page=${page}&pageSize=${pageSize}&sortBy=${sortBy}`;
  
  const response = http.get(url, {
    tags: { name: 'ComplexSearch', query: query, page: page, sortBy: sortBy },
    timeout: '5s',
    headers: {
      'Content-Type': 'application/json',
      'Accept': 'application/json',
    },
  });

  const success = check(response, {
    'complex search status is 200': (r) => r.status === 200,
    'complex search response time < 200ms': (r) => r.timings.duration < 200,
    'complex search has valid JSON': (r) => {
      try {
        const body = JSON.parse(r.body);
        return body !== null && body !== undefined;
      } catch {
        return false;
      }
    },
    'complex search has pagination data': (r) => {
      try {
        const body = JSON.parse(r.body);
        // SearchService returns { books: { items: [...], page: 1, pageSize: 20, totalCount: X, ... }, suggestions: [...] }
        if (Array.isArray(body)) {
          return true; // Old format (array)
        }
        if (body && typeof body === 'object') {
          // New format: check for books object with pagination properties
          if (body.books) {
            // Check if it has pagination properties
            if (body.books.page !== undefined || body.books.totalCount !== undefined || body.books.items !== undefined) {
              return true;
            }
            // Or if books is an array (old format)
            if (Array.isArray(body.books)) {
              return true;
            }
          }
        }
        return false;
      } catch {
        return false;
      }
    },
  });

  const responseTime = response.timings.duration;
  searchResponseTime.add(responseTime);
  complexSearchResponseTime.add(responseTime);
  // Only count as error if status is not 200 (allow empty results)
  errorRate.add(response.status !== 200);
  
  // Slightly longer think time for complex searches
  sleep(0.15);
}

// Main test function - simulates realistic traffic mix
// 70% short searches, 30% complex searches
export default function () {
  const testType = Math.random();
  
  if (testType < 0.7) {
    // 70% of requests are short searches
    performShortSearch();
  } else {
    // 30% of requests are complex searches
    performComplexSearch();
  }
}

// Setup function - runs once at the start
export function setup() {
  console.log(`Starting search load test against ${BASE_URL}`);
  console.log('Target: 1500+ requests/minute (25 requests/second)');
  console.log('Response time requirement: < 200ms (p95 and p99)');
  console.log('Traffic mix: 70% short searches, 30% complex searches');
  
  // Verify API Gateway is accessible
  const healthCheck = http.get(`${BASE_URL}/health`, { 
    timeout: '10s',
    headers: {
      'Accept': 'application/json',
    },
  });
  if (healthCheck.status !== 200) {
    console.warn(`Warning: API Gateway health check returned status ${healthCheck.status}`);
    console.warn('Test will continue, but API Gateway may not be fully operational');
  } else {
    console.log('✓ API Gateway health check passed');
  }
  
  return { baseUrl: BASE_URL };
}

// Teardown function - runs once at the end
export function teardown(data) {
  console.log(`Search load test completed for ${data.baseUrl}`);
  console.log('Review the metrics above for:');
  console.log('- Response times (p95, p99 should be < 200ms)');
  console.log('- Error rates (should be < 1%)');
  console.log('- Total requests (should reach 1500+ per minute)');
}


