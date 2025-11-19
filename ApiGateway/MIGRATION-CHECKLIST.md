# API Gateway v2.0 - Migration Checklist

**Fra:** v1.0 (Basic YARP Gateway)  
**Til:** v2.0 (Production-Ready Gateway)  
**Dato:** 19. November 2025

---

## 📋 Pre-Migration

### Backup
- [ ] Backup current `ApiGateway/` folder
- [ ] Backup `docker-compose.yml`
- [ ] Backup current configuration files
- [ ] Tag current version in git: `git tag v1.0-backup`

### Documentation
- [ ] Document current endpoints
- [ ] Document current behavior
- [ ] Note any custom configurations
- [ ] List all dependent services

### Testing
- [ ] Run all existing tests
- [ ] Document current test results
- [ ] Take performance baseline metrics
- [ ] Test all endpoints manually

---

## 🚀 Migration Steps

### Step 1: Verify Files (✅ Already Done)
All new files have been created in the correct locations:

**New Files:**
- [ ] ✅ `Configuration/SecuritySettings.cs`
- [ ] ✅ `Middleware/ExceptionHandlingMiddleware.cs`
- [ ] ✅ `Middleware/RequestLoggingMiddleware.cs`
- [ ] ✅ `Middleware/SecurityHeadersMiddleware.cs`
- [ ] ✅ `Middleware/RateLimitingMiddleware.cs`
- [ ] ✅ `Services/ITokenValidationService.cs`
- [ ] ✅ `Services/TokenValidationService.cs`
- [ ] ✅ `Services/ISwaggerAggregationService.cs`
- [ ] ✅ `Services/SwaggerAggregationService.cs`
- [ ] ✅ `Extensions/ServiceCollectionExtensions.cs`
- [ ] ✅ `Extensions/YarpExtensions.cs`
- [ ] ✅ `Policies/ResiliencePolicies.cs`

**Updated Files:**
- [ ] ✅ `Program.cs` (completely rewritten)
- [ ] ✅ `Middleware/JwtAuthenticationMiddleware.cs` (refactored)
- [ ] ✅ `appsettings.json` (updated with Security section)
- [ ] ✅ `ApiGateway.csproj` (added packages)
- [ ] ✅ `README.md` (completely rewritten)

**New Documentation:**
- [ ] ✅ `ARCHITECTURE-ANALYSIS.md`
- [ ] ✅ `IMPLEMENTATION-SUMMARY-DA.md`
- [ ] ✅ `appsettings.Production.json`
- [ ] ✅ `MIGRATION-CHECKLIST.md` (this file)

### Step 2: Build and Restore
```bash
cd ApiGateway
dotnet restore
dotnet build
```

**Verify:**
- [ ] No compilation errors
- [ ] All packages restored successfully
- [ ] No missing dependencies

### Step 3: Configuration Review

**Review `appsettings.json`:**
- [ ] `Security:Cors:AllowedOrigins` - Update with your frontend URLs
- [ ] `Security:RateLimit:Enabled` - Set to `true` or `false`
- [ ] `Security:RateLimit:GeneralLimit` - Adjust as needed
- [ ] `Security:RateLimit:EndpointLimits` - Add/modify endpoint-specific limits
- [ ] `Security:Jwt:AuthServiceUrl` - Verify correct URL
- [ ] `ReverseProxy:Routes` - Verify all routes are correct
- [ ] `ReverseProxy:Clusters` - Verify all cluster addresses

**Review `appsettings.Production.json`:**
- [ ] Update production CORS origins
- [ ] Adjust production rate limits
- [ ] Verify logging levels

### Step 4: Docker Build
```bash
docker-compose build apigateway
```

**Verify:**
- [ ] Build succeeds
- [ ] No Docker errors
- [ ] Image created successfully

### Step 5: Start Services
```bash
docker-compose up -d sqlserver rabbitmq redis
docker-compose up -d authservice bookservice warehouseservice searchservice orderservice userservice
docker-compose up apigateway
```

**Watch logs:**
```bash
docker-compose logs -f apigateway
```

**Verify:**
- [ ] Gateway starts without errors
- [ ] No exception in logs
- [ ] All middleware loaded
- [ ] YARP routes configured

---

## 🧪 Testing

### Basic Connectivity
```bash
# Gateway info
curl http://localhost:5004/

# Health check
curl http://localhost:5004/health
```

**Expected:**
- [ ] Gateway info returns JSON with version 2.0
- [ ] Health check returns status for all 6 services

### Public Endpoints (No Auth)
```bash
# Search books
curl http://localhost:5004/search

# Get books
curl http://localhost:5004/books
```

**Expected:**
- [ ] Returns data successfully
- [ ] Response includes security headers
- [ ] Response is compressed (check headers)

### Authentication
```bash
# Register
curl -X POST http://localhost:5004/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","email":"test@test.com","password":"Test123!"}'

# Login
curl -X POST http://localhost:5004/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"testuser","password":"Test123!"}'
```

**Expected:**
- [ ] Register succeeds
- [ ] Login returns JWT token
- [ ] Rate limiting headers present

### Protected Endpoints
```bash
# Get token from login response
TOKEN="your-jwt-token"

# Test protected endpoint
curl http://localhost:5004/orders \
  -H "Authorization: Bearer $TOKEN"
```

**Expected:**
- [ ] Returns data with valid token
- [ ] Returns 401 without token
- [ ] Returns 401 with invalid token

### Rate Limiting
```bash
# Test rate limiting on login (5 requests/minute)
for i in {1..10}; do
  curl -X POST http://localhost:5004/auth/login \
    -H "Content-Type: application/json" \
    -d '{"username":"test","password":"test"}'
  echo ""
done
```

**Expected:**
- [ ] First 5 requests succeed or return 401 (auth failure)
- [ ] 6th+ requests return 429 Too Many Requests
- [ ] Response includes Retry-After header

### CORS
```bash
# Test CORS preflight
curl -X OPTIONS http://localhost:5004/books \
  -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: GET" \
  -v
```

**Expected:**
- [ ] Returns CORS headers
- [ ] Access-Control-Allow-Origin includes your origin
- [ ] Access-Control-Allow-Methods includes GET

### Security Headers
```bash
# Check security headers
curl -I http://localhost:5004/books
```

**Expected headers:**
- [ ] X-Content-Type-Options: nosniff
- [ ] X-Frame-Options: DENY
- [ ] X-XSS-Protection: 1; mode=block
- [ ] Referrer-Policy: strict-origin-when-cross-origin
- [ ] Content-Security-Policy present

### Swagger
```bash
# Access Swagger UI (Development only)
# Open in browser: http://localhost:5004/swagger

# Get Swagger JSON
curl http://localhost:5004/swagger/auth/swagger.json
curl http://localhost:5004/swagger/books/swagger.json
```

**Expected:**
- [ ] Swagger UI loads (if Development)
- [ ] All 6 services listed
- [ ] Swagger JSON returns for each service

### Circuit Breaker Test
```bash
# Stop a service
docker-compose stop bookservice

# Try to access it multiple times
for i in {1..10}; do
  curl http://localhost:5004/books
  echo ""
done

# Start service again
docker-compose start bookservice

# Wait 30 seconds and try again
sleep 30
curl http://localhost:5004/books
```

**Expected:**
- [ ] First few requests fail with 503
- [ ] Circuit opens (check logs)
- [ ] After service restart + 30s, circuit closes
- [ ] Requests succeed again

---

## 📊 Performance Testing

### Baseline Metrics
```bash
# Install k6 if not already installed
# Run load test
k6 run load-test.js
```

**Measure:**
- [ ] Response time P95 < 100ms
- [ ] Response time P99 < 200ms
- [ ] Throughput > 1000 req/sec
- [ ] Error rate < 0.1%

### Cache Effectiveness
```bash
# Monitor logs for cache hits
docker-compose logs -f apigateway | grep "cache"

# Make repeated requests
for i in {1..10}; do
  curl http://localhost:5004/swagger/auth/swagger.json > /dev/null
done
```

**Expected:**
- [ ] First request fetches from service
- [ ] Subsequent requests hit cache
- [ ] Cache hit rate > 80%

---

## 🔒 Security Audit

### Vulnerability Scan
```bash
# Scan for vulnerabilities
dotnet list package --vulnerable
```

**Expected:**
- [ ] No critical vulnerabilities
- [ ] No high vulnerabilities

### Dependency Check
```bash
# Check for outdated packages
dotnet list package --outdated
```

**Action:**
- [ ] Review outdated packages
- [ ] Update if necessary
- [ ] Test after updates

### Security Headers Validation
Use online tools:
- [ ] https://securityheaders.com
- [ ] https://observatory.mozilla.org

**Expected score:**
- [ ] A or A+ rating

---

## 📝 Post-Migration

### Verification
- [ ] All endpoints working
- [ ] All tests passing
- [ ] No errors in logs
- [ ] Performance acceptable
- [ ] Security headers present
- [ ] Rate limiting working
- [ ] Circuit breaker working
- [ ] Health checks green

### Documentation
- [ ] Update API documentation
- [ ] Update frontend integration guide
- [ ] Document new features
- [ ] Update team wiki

### Monitoring
- [ ] Set up log monitoring
- [ ] Set up health check monitoring
- [ ] Set up alerting for circuit breaker events
- [ ] Set up alerting for rate limit violations

### Team Communication
- [ ] Notify team of changes
- [ ] Share new documentation
- [ ] Schedule demo/walkthrough
- [ ] Answer questions

---

## 🔄 Rollback Plan

If issues occur:

### Quick Rollback
```bash
# Stop new version
docker-compose stop apigateway

# Restore backup
git checkout v1.0-backup -- ApiGateway/

# Rebuild
docker-compose build apigateway

# Start
docker-compose up -d apigateway
```

### Verify Rollback
- [ ] Gateway starts
- [ ] Endpoints work
- [ ] No errors in logs

---

## ✅ Sign-Off

### Development Team
- [ ] Code reviewed
- [ ] Tests passed
- [ ] Documentation reviewed
- [ ] Ready for staging

### QA Team
- [ ] All tests executed
- [ ] No critical bugs
- [ ] Performance acceptable
- [ ] Ready for staging

### DevOps Team
- [ ] Docker build successful
- [ ] Configuration reviewed
- [ ] Monitoring configured
- [ ] Ready for deployment

### Product Owner
- [ ] Features approved
- [ ] Documentation approved
- [ ] Ready for production

---

## 📅 Timeline

### Recommended Schedule

**Day 1: Preparation**
- Review all changes
- Backup everything
- Plan testing

**Day 2: Staging Deployment**
- Deploy to staging
- Run all tests
- Performance testing

**Day 3: Staging Validation**
- Extended testing
- Security audit
- Bug fixes if needed

**Day 4: Production Preparation**
- Final review
- Update documentation
- Prepare rollback plan

**Day 5: Production Deployment**
- Deploy to production
- Monitor closely
- Be ready to rollback

**Day 6-7: Post-Deployment**
- Monitor metrics
- Gather feedback
- Fine-tune configuration

---

## 🎯 Success Criteria

Migration is successful when:

- [ ] ✅ All endpoints functional
- [ ] ✅ No increase in error rate
- [ ] ✅ Performance within acceptable range
- [ ] ✅ Security features working
- [ ] ✅ No critical bugs
- [ ] ✅ Team trained on new features
- [ ] ✅ Documentation complete
- [ ] ✅ Monitoring in place

---

## 📞 Support

### Issues During Migration
1. Check logs: `docker-compose logs apigateway`
2. Review `ARCHITECTURE-ANALYSIS.md`
3. Review `README.md` troubleshooting section
4. Contact team lead

### Emergency Contacts
- **Architecture:** [Name]
- **DevOps:** [Name]
- **Security:** [Name]

---

**Checklist Version:** 1.0  
**Last Updated:** November 19, 2025  
**Status:** Ready for Use

