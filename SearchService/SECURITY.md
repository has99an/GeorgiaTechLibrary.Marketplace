# SearchService Security Documentation

## Overview

SearchService implements comprehensive security measures to protect against common web vulnerabilities and attacks while maintaining anonymous public access for search functionality.

## Security Features Implemented

### 1. Input Validation & Sanitization

**Location**: `Application/Common/Validators/InputSanitizer.cs`

- **Centralized sanitization** for all user inputs
- **Suspicious pattern detection** (SQL injection, XSS, command injection)
- **ISBN validation** with checksum verification (ISBN-10 and ISBN-13)
- **Whitelist-based validation** for sort fields, time windows, and other enums
- **Length limits** on all string inputs (max 200 chars for search terms)
- **Special character filtering** to prevent injection attacks

**Protected Against**:
- SQL/NoSQL injection
- Command injection
- XSS attacks
- Path traversal
- LDAP injection

### 2. Redis Injection Protection

**Location**: `Infrastructure/Common/RedisKeyBuilder.cs`

- **Centralized key construction** with automatic sanitization
- **Escape special Redis characters** (*, ?, [, ], {, }, |, etc.)
- **Key pattern validation** to prevent enumeration
- **IP anonymization** for privacy compliance (GDPR)

**Example**:
```csharp
// Safe key construction
var key = RedisKeyBuilder.BuildBookKey(userIsbn);
// Output: "book:9780123456789" (sanitized)
```

### 3. Enhanced Validators

**Location**: `Application/Queries/*/Validators/`

All query endpoints have FluentValidation validators:
- `SearchBooksQueryValidator` - Search term, pagination, sort validation
- `GetBookByIsbnQueryValidator` - ISBN format and checksum validation
- `SearchBooksWithFiltersQueryValidator` - Multi-filter validation with limits
- `GetAutocompleteQueryValidator` - Prefix sanitization
- `GetPopularSearchesQueryValidator` - Time window validation

**Features**:
- Maximum item limits (e.g., max 20 genres, max 100 page size)
- Suspicious pattern detection
- Whitelist-based validation

### 4. Security Headers

**Location**: `API/Middleware/SecurityHeadersMiddleware.cs`

Implements all OWASP recommended security headers:

```
X-Frame-Options: DENY
X-Content-Type-Options: nosniff
X-XSS-Protection: 1; mode=block
Content-Security-Policy: default-src 'self'; script-src 'self'; ...
Referrer-Policy: no-referrer
Permissions-Policy: geolocation=(), microphone=(), camera=(), ...
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload (HTTPS only)
Cross-Origin-Embedder-Policy: require-corp
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Resource-Policy: same-origin
```

**Removes sensitive headers**:
- Server
- X-Powered-By
- X-AspNet-Version
- X-AspNetMvc-Version

### 5. CORS Configuration

**Location**: `Program.cs`

- **Configurable allowed origins** via appsettings.json
- **Specific methods and headers** (no wildcards in production)
- **Exposed rate limit headers** for client-side handling
- **Preflight caching** (10 minutes)

**Configuration**:
```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "https://yourdomain.com"
    ]
  }
}
```

### 6. Rate Limiting

**Location**: `API/Middleware/RateLimitingMiddleware.cs`

Multi-tier rate limiting with Redis-backed counters:

- **Per-minute limit**: 100 requests
- **Per-hour limit**: 1000 requests
- **Automatic expiry** of rate limit keys
- **IP-based tracking** with anonymization
- **Informative headers**: X-RateLimit-Limit-Minute, X-RateLimit-Remaining-Minute, etc.

**Response on violation**:
```json
{
  "StatusCode": 429,
  "Message": "Rate limit exceeded. Maximum 100 requests per minute allowed.",
  "RetryAfter": "60 seconds"
}
```

### 7. Request Size Limits

**Location**: `API/Middleware/RequestSizeLimitMiddleware.cs`

Prevents DoS attacks through oversized requests:

- **Request body**: Max 1 MB
- **Query string**: Max 2 KB
- **Headers**: Max 8 KB

**HTTP Status Codes**:
- 413 Payload Too Large
- 414 URI Too Long
- 431 Request Header Fields Too Large

### 8. Security Audit Logging

**Location**: `Infrastructure/Logging/SecurityAuditLogger.cs`

Structured logging for all security events:

- **Rate limit violations**
- **Invalid input attempts**
- **Suspicious activity patterns**
- **IP blocking events**
- **Correlation IDs** for request tracking
- **IP anonymization** for privacy

**Log Format**:
```
SECURITY_EVENT: RateLimitViolation | {"EventType":"RateLimitViolation","Timestamp":"2024-11-19T10:30:00Z","ClientIp":"192.168.1.*","Endpoint":"/api/search","Details":{"RequestCount":101}}
```

### 9. Anomaly Detection

**Location**: `Infrastructure/Security/AnomalyDetector.cs`

Real-time detection of suspicious patterns:

**Detected Patterns**:
- **Rapid searches**: >20 searches in 10 seconds
- **Sequential ISBNs**: >5 sequential ISBNs in 1 minute (enumeration attempts)
- **Zero-result searches**: >10 zero-result searches in 5 minutes (scraping)

**Actions**:
- Automatic logging
- Temporary IP blocking (15 minutes)
- Security audit trail

### 10. Query Parameter Validation

**Location**: `API/Filters/ValidateQueryParametersFilter.cs`

Action filter for parameter whitelisting:

- **Endpoint-specific whitelists** (e.g., SearchBooks only allows: query, page, pageSize, sortBy)
- **Rejects unexpected parameters** with detailed error message
- **Suspicious pattern detection** in parameter values

### 11. Response Sanitization

**Location**: `API/Middleware/ResponseSanitizationMiddleware.cs`

- **Removes sensitive headers** from responses
- **Appropriate cache-control headers** per endpoint type
- **No-cache for errors** (prevents caching of error responses)

**Cache Strategy**:
- Search results: 1 minute
- Autocomplete: 5 minutes
- Facets: 10 minutes
- Analytics: No cache

### 12. Error Message Sanitization

**Location**: `API/Middleware/ExceptionHandlingMiddleware.cs`

- **Generic error messages** for internal errors
- **Path sanitization** (removes file paths)
- **Connection string sanitization** (removes sensitive data)
- **Full error logging server-side** only

**Production Error Response**:
```json
{
  "StatusCode": 500,
  "Message": "An internal server error occurred. Please try again later.",
  "Errors": null
}
```

## Security Best Practices

### For Developers

1. **Always use InputSanitizer** before processing user input
2. **Use RedisKeyBuilder** for all Redis key construction
3. **Add validators** for all new query/command classes
4. **Never expose internal details** in error messages
5. **Log security events** using SecurityAuditLogger

### For Deployment

1. **Configure CORS** with specific allowed origins in production
2. **Enable HTTPS** and HSTS headers
3. **Set up monitoring** for security audit logs
4. **Review rate limits** based on expected traffic
5. **Keep dependencies updated** (especially Redis, ASP.NET Core)

### For Operations

1. **Monitor security logs** for patterns
2. **Review blocked IPs** regularly
3. **Adjust rate limits** based on legitimate traffic patterns
4. **Set up alerts** for anomaly detection events
5. **Regular security audits** of logs

## Middleware Pipeline Order

The middleware order is critical for security:

```
1. SecurityHeadersMiddleware       - Add security headers
2. RequestSizeLimitMiddleware      - Limit request sizes
3. ResponseCompression             - Compress responses
4. CORS                           - Handle cross-origin requests
5. RateLimitingMiddleware         - Enforce rate limits
6. ExceptionHandlingMiddleware    - Catch and sanitize errors
7. ResponseSanitizationMiddleware - Clean up responses
8. [Your application logic]
```

## MediatR Pipeline Behaviors

Security is also enforced at the application layer:

```
1. LoggingBehavior           - Log all requests
2. ValidationBehavior        - Validate with FluentValidation
3. SecurityAuditBehavior     - Detect suspicious patterns
4. PerformanceBehavior       - Track performance + auto-track searches
5. CachingBehavior          - Cache responses
```

## Known Limitations

1. **Anonymous Access**: All endpoints are public by design. If authentication is needed in the future, add JWT bearer authentication.
2. **IP-based Rate Limiting**: Can be bypassed with VPNs/proxies, but anomaly detection helps mitigate this.
3. **Redis Dependency**: Security features (rate limiting, anomaly detection) depend on Redis availability.

## Compliance

- **GDPR**: IP addresses are anonymized in logs
- **OWASP Top 10**: Protected against all major vulnerabilities
- **PCI DSS**: No payment data is processed
- **SOC 2**: Comprehensive audit logging implemented

## Security Testing

### Manual Testing

1. **Input Validation**:
   ```bash
   # Test SQL injection
   curl "http://localhost:5000/api/search?query='; DROP TABLE books--"
   
   # Test XSS
   curl "http://localhost:5000/api/search?query=<script>alert('xss')</script>"
   ```

2. **Rate Limiting**:
   ```bash
   # Send 101 requests in 1 minute
   for i in {1..101}; do curl "http://localhost:5000/api/search?query=test"; done
   ```

3. **Request Size Limits**:
   ```bash
   # Send oversized query string
   curl "http://localhost:5000/api/search?query=$(python -c 'print("a"*3000)')"
   ```

### Automated Testing

Consider using:
- **OWASP ZAP** for vulnerability scanning
- **Burp Suite** for penetration testing
- **Artillery** for load testing rate limits

## Incident Response

If a security incident is detected:

1. **Check security audit logs** in application logs
2. **Review blocked IPs** in Redis (`blocked:ip:*` keys)
3. **Analyze anomaly patterns** (`anomaly:*` keys in Redis)
4. **Adjust rate limits** if needed
5. **Update input validation** if new attack patterns are discovered

## Contact

For security concerns or to report vulnerabilities, please contact the development team.

## Changelog

- **2024-11-19**: Initial comprehensive security implementation
  - Input sanitization
  - Redis injection protection
  - Enhanced validators
  - Security headers
  - CORS configuration
  - Rate limiting
  - Request size limits
  - Security audit logging
  - Anomaly detection
  - Query parameter validation
  - Response sanitization

