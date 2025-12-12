# Load Testing

This directory contains load testing scripts for validating the Georgia Tech Library Marketplace performance requirements.

## Requirements

- **1000 requests per minute**: System must handle 1000 requests/min
- **Search response time < 1 second**: Search endpoint must respond in < 1 second (95th percentile)

## Prerequisites

Install k6:
```bash
# Windows (using Chocolatey)
choco install k6

# macOS
brew install k6

# Linux
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

## Running Load Tests

### Basic Test
```bash
k6 run load-tests/k6-load-test.js
```

### Custom Base URL
```bash
k6 run --env BASE_URL=http://localhost:5004 load-tests/k6-load-test.js
```

### Production Test
```bash
k6 run --env BASE_URL=https://api.production.com load-tests/k6-load-test.js
```

## Test Scenarios

### 1. Search Performance Test
- **Target**: 1000 requests/min
- **Response Time**: < 1 second (p95)
- **Endpoint**: `GET /search?query={query}`
- **Validation**: Status 200, response time < 1000ms, has results

### 2. Available Books Test
- **Target**: 500 requests/min
- **Response Time**: < 500ms (p95)
- **Endpoint**: `GET /search/available?page={page}&pageSize=20`
- **Validation**: Status 200, response time < 500ms, has pagination

### 3. Order Test
- **Target**: 200 requests/min
- **Response Time**: < 2 seconds (p95)
- **Endpoint**: `GET /orders`
- **Validation**: Endpoint responds (200 or 401 OK)

## Test Stages

The load test uses the following stages:
1. **Ramp up**: 0 → 50 users over 1 minute
2. **Sustain**: 50 users for 3 minutes
3. **Ramp up**: 50 → 100 users over 1 minute
4. **Sustain**: 100 users for 5 minutes (target: 1000 req/min)
5. **Ramp down**: 100 → 0 users over 1 minute

## Metrics

The test tracks:
- **search_response_time**: Response time for search requests
- **available_books_response_time**: Response time for available books requests
- **order_response_time**: Response time for order requests
- **errors**: Error rate
- **http_req_failed**: HTTP request failure rate

## Thresholds

- Search response time: p95 < 1000ms, p99 < 2000ms
- Available books response time: p95 < 500ms, p99 < 1000ms
- Order response time: p95 < 2000ms, p99 < 5000ms
- Error rate: < 1%
- HTTP failure rate: < 1%

## Results

After running the test, review:
1. **Response times**: Ensure p95 < 1 second for search
2. **Error rates**: Should be < 1%
3. **Throughput**: Should achieve ~1000 requests/min
4. **Bottlenecks**: Identify services with high response times

## Continuous Integration

The load tests can be integrated into CI/CD:
```yaml
- name: Run Load Tests
  run: |
    k6 run --out json=load-test-results.json load-tests/k6-load-test.js
```

