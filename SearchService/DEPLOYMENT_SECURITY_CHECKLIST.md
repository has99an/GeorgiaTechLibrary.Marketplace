# SearchService Security Deployment Checklist

## Pre-Deployment Verification

### ✅ Code Review
- [ ] All security features implemented and tested
- [ ] No hardcoded secrets or credentials
- [ ] All TODOs and FIXMEs resolved
- [ ] Code follows security best practices
- [ ] All validators are registered and working

### ✅ Configuration
- [ ] `appsettings.Production.json` configured with production values
- [ ] CORS allowed origins set to production domains only
- [ ] Redis connection string configured
- [ ] RabbitMQ connection string configured
- [ ] Logging configured (separate security log file)
- [ ] Rate limits reviewed and adjusted for production traffic

### ✅ Build & Test
- [ ] Project builds without errors: `dotnet build`
- [ ] No compiler warnings
- [ ] All unit tests pass (if available)
- [ ] Integration tests pass (if available)
- [ ] Manual security testing completed

## Deployment Configuration

### 1. appsettings.Production.json

Create `appsettings.Production.json` with:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://yourdomain.com",
      "https://www.yourdomain.com",
      "https://api.yourdomain.com"
    ]
  },
  "Redis": {
    "ConnectionString": "your-redis-server:6379,password=your-password,ssl=true"
  },
  "RabbitMQ": {
    "HostName": "your-rabbitmq-server",
    "UserName": "your-username",
    "Password": "your-password",
    "Port": 5672
  },
  "Security": {
    "RateLimiting": {
      "PerMinuteLimit": 100,
      "PerHourLimit": 1000
    },
    "AnomalyDetection": {
      "Enabled": true,
      "BlockDurationMinutes": 15
    }
  }
}
```

### 2. Environment Variables

Set these environment variables:

```bash
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000
Redis__ConnectionString=your-redis-connection-string
RabbitMQ__Password=your-rabbitmq-password
```

### 3. HTTPS Configuration

- [ ] SSL/TLS certificate installed
- [ ] HTTPS enabled in production
- [ ] HTTP to HTTPS redirect enabled
- [ ] HSTS header enabled (automatic when HTTPS is detected)

## Security Verification

### 1. Security Headers

Test with curl:

```bash
curl -I https://your-domain.com/api/search/health
```

Verify these headers are present:
- [ ] `X-Frame-Options: DENY`
- [ ] `X-Content-Type-Options: nosniff`
- [ ] `X-XSS-Protection: 1; mode=block`
- [ ] `Content-Security-Policy: ...`
- [ ] `Referrer-Policy: no-referrer`
- [ ] `Strict-Transport-Security: max-age=31536000; includeSubDomains; preload`

Verify these headers are NOT present:
- [ ] `Server` (should be removed)
- [ ] `X-Powered-By` (should be removed)
- [ ] `X-AspNet-Version` (should be removed)

### 2. Input Validation

Test with malicious inputs:

```bash
# SQL Injection attempt
curl "https://your-domain.com/api/search?query='; DROP TABLE books--"
# Expected: 400 Bad Request with validation error

# XSS attempt
curl "https://your-domain.com/api/search?query=<script>alert('xss')</script>"
# Expected: 400 Bad Request with validation error

# Command injection attempt
curl "https://your-domain.com/api/search?query=test; ls -la"
# Expected: 400 Bad Request with validation error
```

- [ ] All injection attempts are blocked
- [ ] Appropriate error messages returned (no internal details)
- [ ] Security events logged

### 3. Rate Limiting

Test rate limits:

```bash
# Send 101 requests in quick succession
for i in {1..101}; do curl "https://your-domain.com/api/search?query=test"; done
```

- [ ] Request 101 returns 429 Too Many Requests
- [ ] Rate limit headers present in responses
- [ ] Retry-After header present in 429 response

### 4. Request Size Limits

Test size limits:

```bash
# Oversized query string (>2KB)
curl "https://your-domain.com/api/search?query=$(python -c 'print("a"*3000)')"
# Expected: 414 URI Too Long

# Oversized request body (>1MB) - for POST endpoints
curl -X POST "https://your-domain.com/api/search/advanced" \
  -H "Content-Type: application/json" \
  -d "$(python -c 'print("{"data":"' + 'a'*2000000 + '"}")')"
# Expected: 413 Payload Too Large
```

- [ ] Oversized requests are rejected
- [ ] Appropriate HTTP status codes returned

### 5. CORS

Test CORS:

```bash
# From allowed origin
curl -H "Origin: https://yourdomain.com" \
  -H "Access-Control-Request-Method: GET" \
  -X OPTIONS "https://your-domain.com/api/search?query=test"
# Expected: Access-Control-Allow-Origin header present

# From disallowed origin
curl -H "Origin: https://evil.com" \
  -H "Access-Control-Request-Method: GET" \
  -X OPTIONS "https://your-domain.com/api/search?query=test"
# Expected: No Access-Control-Allow-Origin header
```

- [ ] Allowed origins work correctly
- [ ] Disallowed origins are blocked
- [ ] Rate limit headers are exposed

### 6. Error Handling

Test error responses:

```bash
# Invalid ISBN
curl "https://your-domain.com/api/search/by-isbn/invalid"
# Expected: 400 Bad Request with sanitized error message

# Non-existent endpoint
curl "https://your-domain.com/api/nonexistent"
# Expected: 404 Not Found with generic error message
```

- [ ] No internal details exposed in error messages
- [ ] No stack traces in production
- [ ] No file paths in error messages
- [ ] Errors logged server-side with full details

## Monitoring Setup

### 1. Log Monitoring

- [ ] Application logs configured and accessible
- [ ] Security audit logs configured (separate file)
- [ ] Log aggregation tool configured (e.g., ELK, Splunk, Azure Monitor)
- [ ] Alerts configured for security events

### 2. Metrics

- [ ] Rate limit violations monitored
- [ ] Anomaly detection events monitored
- [ ] Error rates monitored
- [ ] Response times monitored

### 3. Alerts

Configure alerts for:
- [ ] High rate of 429 (rate limit) responses
- [ ] High rate of 400 (validation) errors
- [ ] Anomaly detection events
- [ ] IP blocking events
- [ ] Redis connection failures
- [ ] RabbitMQ connection failures

## Redis Security

- [ ] Redis password configured
- [ ] Redis SSL/TLS enabled (if available)
- [ ] Redis firewall rules configured (only allow SearchService)
- [ ] Redis persistence configured (AOF or RDB)
- [ ] Redis backup strategy in place

## RabbitMQ Security

- [ ] RabbitMQ user created with minimal permissions
- [ ] RabbitMQ password configured
- [ ] RabbitMQ SSL/TLS enabled (if available)
- [ ] RabbitMQ firewall rules configured
- [ ] RabbitMQ management console secured

## Network Security

- [ ] Firewall rules configured (only allow necessary ports)
- [ ] Internal services not exposed to internet
- [ ] API Gateway/Load Balancer configured (if applicable)
- [ ] DDoS protection enabled (e.g., Cloudflare, AWS Shield)
- [ ] Network segmentation implemented

## Backup & Recovery

- [ ] Database backup strategy (Redis)
- [ ] Configuration backup strategy
- [ ] Disaster recovery plan documented
- [ ] Recovery time objective (RTO) defined
- [ ] Recovery point objective (RPO) defined

## Compliance

- [ ] GDPR compliance verified (IP anonymization)
- [ ] Data retention policy defined
- [ ] Privacy policy updated
- [ ] Terms of service updated
- [ ] Security incident response plan documented

## Post-Deployment Verification

### Immediate (within 1 hour)

- [ ] Service is running and healthy: `/health` endpoint returns 200
- [ ] Redis connectivity verified: `/health/ready` returns 200
- [ ] Search functionality working
- [ ] Rate limiting working
- [ ] Security headers present
- [ ] Logs are being written
- [ ] No errors in logs

### Short-term (within 24 hours)

- [ ] Monitor error rates
- [ ] Monitor response times
- [ ] Monitor rate limit violations
- [ ] Review security audit logs
- [ ] Verify anomaly detection is working
- [ ] Check for any blocked IPs (review if legitimate)

### Medium-term (within 1 week)

- [ ] Review rate limit thresholds (adjust if needed)
- [ ] Review security audit logs for patterns
- [ ] Performance testing completed
- [ ] Load testing completed
- [ ] Security scanning completed (OWASP ZAP, Burp Suite)

### Long-term (ongoing)

- [ ] Regular security audits (monthly)
- [ ] Dependency updates (weekly)
- [ ] Security patch management
- [ ] Penetration testing (quarterly)
- [ ] Review and update security policies

## Rollback Plan

If issues are detected:

1. **Immediate Rollback**:
   ```bash
   # Stop the new version
   docker stop searchservice
   
   # Start the previous version
   docker start searchservice-previous
   ```

2. **Verify Rollback**:
   - [ ] Service is healthy
   - [ ] Search functionality working
   - [ ] No errors in logs

3. **Investigate**:
   - Review logs for root cause
   - Fix issues in development
   - Re-test before re-deploying

## Sign-off

- [ ] Security review completed by: _________________ Date: _______
- [ ] Configuration reviewed by: _________________ Date: _______
- [ ] Deployment tested by: _________________ Date: _______
- [ ] Approved for production by: _________________ Date: _______

## Emergency Contacts

- **Development Team**: [contact info]
- **DevOps Team**: [contact info]
- **Security Team**: [contact info]
- **On-call Engineer**: [contact info]

## Additional Resources

- [SECURITY.md](SECURITY.md) - Detailed security documentation
- [SECURITY_SUMMARY.md](SECURITY_SUMMARY.md) - Security implementation summary
- [README.md](README.md) - General service documentation
- [appsettings.Security.json](appsettings.Security.json) - Security configuration example

---

**Last Updated**: 2024-11-19  
**Version**: 2.0 (Clean Architecture + CQRS with comprehensive security)

