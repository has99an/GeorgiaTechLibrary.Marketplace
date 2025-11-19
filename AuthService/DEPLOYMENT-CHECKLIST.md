# AuthService - Deployment & Testing Checklist

## Pre-Deployment Checklist

### ✅ Code Quality
- [x] Build succeeds (0 warnings, 0 errors)
- [x] Linter clean (0 errors)
- [x] No duplicate code
- [x] All TODOs resolved
- [x] Code reviewed

### ✅ Architecture
- [x] Clean Architecture implemented
- [x] Dependency injection configured
- [x] Interfaces properly abstracted
- [x] SOLID principles followed
- [x] No business logic in controllers

### ✅ Security
- [x] Password hashing (BCrypt)
- [x] JWT token generation
- [x] Rate limiting implemented
- [x] Audit logging enabled
- [x] Input validation
- [x] Account lockout (5 attempts)
- [x] Email masking in logs

### ✅ Configuration
- [x] appsettings.json configured
- [x] appsettings.Development.json configured
- [x] Connection strings set
- [x] JWT key configured (change in production!)
- [x] RabbitMQ settings configured
- [x] Rate limiting settings configured

### ✅ Database
- [x] Migrations created
- [x] Database schema validated
- [x] Seed data implemented
- [x] Idempotent seeding
- [x] Transaction safety

### ✅ Integration
- [x] RabbitMQ event publishing
- [x] UserCreated event format
- [x] ApiGateway token validation endpoint
- [x] Health checks configured

### ✅ Documentation
- [x] README.md comprehensive
- [x] ARCHITECTURE.md detailed
- [x] IMPLEMENTATION-SUMMARY.md complete
- [x] API endpoints documented
- [x] Swagger configured

---

## Testing Checklist

### Unit Tests (Domain Layer)
- [ ] `Email.Create()` validates format
- [ ] `Email.Create()` throws on invalid email
- [ ] `Password.Create()` validates strength
- [ ] `Password.Create()` throws on weak password
- [ ] `AuthUser.RecordFailedLogin()` increments counter
- [ ] `AuthUser.RecordFailedLogin()` locks after 5 attempts
- [ ] `AuthUser.IsLockedOut()` returns true when locked
- [ ] `AuthUser.RecordSuccessfulLogin()` resets counter

### Integration Tests (Application Layer)
- [ ] `AuthService.RegisterAsync()` creates user
- [ ] `AuthService.RegisterAsync()` throws on duplicate email
- [ ] `AuthService.LoginAsync()` returns tokens on valid credentials
- [ ] `AuthService.LoginAsync()` throws on invalid credentials
- [ ] `AuthService.LoginAsync()` locks account after 5 failures
- [ ] `AuthService.RefreshTokenAsync()` returns new tokens
- [ ] `AuthService.ValidateToken()` validates valid token
- [ ] `AuthService.ValidateToken()` rejects invalid token

### API Tests (Controller Layer)
- [ ] `POST /register` returns 200 with tokens
- [ ] `POST /register` returns 400 on invalid email
- [ ] `POST /register` returns 400 on weak password
- [ ] `POST /register` returns 409 on duplicate email
- [ ] `POST /login` returns 200 with tokens
- [ ] `POST /login` returns 401 on invalid credentials
- [ ] `POST /login` returns 401 on locked account
- [ ] `POST /refresh` returns 200 with new tokens
- [ ] `POST /refresh` returns 401 on invalid token
- [ ] `POST /validate` returns 200 on valid token
- [ ] `POST /validate` returns 401 on invalid token
- [ ] `GET /health` returns 200 with status

### Security Tests
- [ ] Rate limiting: 6 login attempts in 1 minute (6th fails)
- [ ] Rate limiting: 4 register attempts in 1 hour (4th fails)
- [ ] Account lockout: 5 failed logins locks account
- [ ] Account lockout: Lockout expires after 15 minutes
- [ ] Audit logging: All auth operations logged
- [ ] Password hashing: Passwords stored as bcrypt hashes
- [ ] Email validation: Invalid emails rejected
- [ ] Password strength: Weak passwords rejected

### Data Seeding Tests
- [ ] CSV file exists at `Data/AuthUsers.csv`
- [ ] Seeding loads 1,963 users
- [ ] All users have bcrypt password hashes
- [ ] Default password is `Password123!`
- [ ] Seeding is idempotent (run twice, no duplicates)
- [ ] Seeding uses transactions (rollback on error)
- [ ] Seeding logs progress
- [ ] Can login with seeded user

### Integration Tests (External Services)
- [ ] RabbitMQ: UserCreated event published on registration
- [ ] RabbitMQ: Event has correct format
- [ ] RabbitMQ: Graceful degradation if unavailable
- [ ] Database: Connection retries on startup
- [ ] Database: Migrations run automatically
- [ ] UserService: Receives UserCreated event
- [ ] ApiGateway: Token validation works

### Performance Tests
- [ ] Login: < 500ms response time
- [ ] Register: < 1000ms response time
- [ ] Validate: < 100ms response time
- [ ] Seeding: 1,963 users in < 60 seconds
- [ ] Rate limiting: No performance degradation

---

## Manual Testing Steps

### 1. Register New User
```bash
curl -X POST http://localhost:5000/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "newuser@gatech.edu",
    "password": "SecurePass123!"
  }'
```

**Expected:**
- HTTP 200 OK
- Response contains `accessToken`, `refreshToken`, `expiresIn`
- UserCreated event published to RabbitMQ
- User created in database

### 2. Login with Valid Credentials
```bash
curl -X POST http://localhost:5000/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "newuser@gatech.edu",
    "password": "SecurePass123!"
  }'
```

**Expected:**
- HTTP 200 OK
- Response contains tokens
- LastLoginDate updated in database
- Audit log entry created

### 3. Login with Invalid Credentials
```bash
curl -X POST http://localhost:5000/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "newuser@gatech.edu",
    "password": "WrongPassword"
  }'
```

**Expected:**
- HTTP 401 Unauthorized
- Error message: "Invalid email or password"
- FailedLoginAttempts incremented
- Audit log entry created

### 4. Test Account Lockout
```bash
# Run 5 times with wrong password
for i in {1..5}; do
  curl -X POST http://localhost:5000/login \
    -H "Content-Type: application/json" \
    -d '{
      "email": "newuser@gatech.edu",
      "password": "WrongPassword"
    }'
done
```

**Expected:**
- First 4 attempts: HTTP 401
- 5th attempt: HTTP 401 with lockout message
- LockoutEndDate set in database
- Subsequent attempts fail even with correct password

### 5. Refresh Token
```bash
# Use refreshToken from login response
curl -X POST http://localhost:5000/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
  }'
```

**Expected:**
- HTTP 200 OK
- New accessToken and refreshToken
- Old tokens still valid until expiration

### 6. Validate Token
```bash
# Use accessToken from login response
curl -X POST http://localhost:5000/validate \
  -H "Content-Type: application/json" \
  -d '{
    "token": "eyJhbGciOiJIUzI1NiIs..."
  }'
```

**Expected:**
- HTTP 200 OK
- Response: `{ "valid": true }`

### 7. Test Rate Limiting
```bash
# Attempt 6 logins in quick succession
for i in {1..6}; do
  echo "Attempt $i:"
  curl -X POST http://localhost:5000/login \
    -H "Content-Type: application/json" \
    -d '{
      "email": "test@gatech.edu",
      "password": "wrong"
    }'
  echo ""
done
```

**Expected:**
- First 5 attempts: HTTP 401 (invalid credentials)
- 6th attempt: HTTP 429 (rate limit exceeded)
- Response header: `Retry-After: 60`

### 8. Test Data Seeding
```bash
# Check database after startup
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P 'YourStrong!Passw0rd' \
  -Q "SELECT COUNT(*) FROM AuthServiceDb.dbo.AuthUsers"
```

**Expected:**
- Count: 1963 (or 1964 if you registered a new user)

### 9. Login with Seeded User
```bash
curl -X POST http://localhost:5000/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "student1@gatech.edu",
    "password": "Password123!"
  }'
```

**Expected:**
- HTTP 200 OK
- Tokens returned
- Seeded user can authenticate

### 10. Health Check
```bash
curl http://localhost:5000/health
```

**Expected:**
- HTTP 200 OK
- Response shows database and self checks as "Healthy"

### 11. Swagger Documentation
```bash
# Open in browser
http://localhost:5000/swagger
```

**Expected:**
- Swagger UI loads
- All endpoints documented
- Can test endpoints interactively

---

## Deployment Steps

### Local Development

1. **Prerequisites:**
   ```bash
   # Install .NET 8.0 SDK
   dotnet --version  # Should be 8.0.x
   
   # Start SQL Server
   docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Passw0rd" \
     -p 1433:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2022-latest
   
   # Start RabbitMQ
   docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
   ```

2. **Update Configuration:**
   ```bash
   # Edit appsettings.Development.json
   # Set connection strings to localhost
   ```

3. **Run Migrations:**
   ```bash
   cd AuthService
   dotnet ef database update
   ```

4. **Run Service:**
   ```bash
   dotnet run
   ```

5. **Verify:**
   - Service starts on http://localhost:5000
   - Swagger available at http://localhost:5000/swagger
   - Health check: http://localhost:5000/health

### Docker Deployment

1. **Build Image:**
   ```bash
   cd AuthService
   docker build -t authservice:latest .
   ```

2. **Run with Docker Compose:**
   ```bash
   cd ..
   docker-compose up authservice
   ```

3. **Verify:**
   - Service starts on http://authservice:8080
   - Logs show "Successfully seeded 1963 auth users"
   - Health check: http://authservice:8080/health

### Production Deployment

1. **Security:**
   - [ ] Change JWT key in appsettings.json
   - [ ] Use strong SQL Server password
   - [ ] Configure HTTPS certificates
   - [ ] Enable firewall rules
   - [ ] Set up monitoring/alerting

2. **Configuration:**
   - [ ] Update connection strings
   - [ ] Configure RabbitMQ credentials
   - [ ] Set appropriate rate limits
   - [ ] Configure logging levels

3. **Database:**
   - [ ] Run migrations
   - [ ] Verify seeding completed
   - [ ] Set up backups
   - [ ] Configure connection pooling

4. **Monitoring:**
   - [ ] Set up health check monitoring
   - [ ] Configure log aggregation
   - [ ] Set up performance monitoring
   - [ ] Configure alerting

---

## Troubleshooting

### Issue: Database Connection Fails
**Symptoms:** Service fails to start, logs show connection errors

**Solutions:**
1. Verify SQL Server is running
2. Check connection string
3. Wait for SQL Server to fully start (30 retries)
4. Check firewall rules

### Issue: RabbitMQ Connection Fails
**Symptoms:** Logs show RabbitMQ connection errors

**Solutions:**
1. Verify RabbitMQ is running
2. Check RabbitMQ credentials
3. Service continues without messaging (logged as warning)

### Issue: Seeding Fails
**Symptoms:** Database empty, logs show seeding errors

**Solutions:**
1. Verify `Data/AuthUsers.csv` exists
2. Check CSV format (UserId, Email, PasswordHash, CreatedDate)
3. Check database permissions
4. Review error logs for specific issues

### Issue: Rate Limiting Not Working
**Symptoms:** Can make unlimited requests

**Solutions:**
1. Verify middleware is registered in Program.cs
2. Check rate limiting configuration
3. Verify IP address extraction works

### Issue: Tokens Not Validating
**Symptoms:** ApiGateway rejects tokens

**Solutions:**
1. Verify JWT key matches between services
2. Check token expiration
3. Verify issuer and audience match
4. Check clock skew

---

## Post-Deployment Verification

### Smoke Tests
- [ ] Service starts successfully
- [ ] Health check returns 200
- [ ] Swagger UI accessible
- [ ] Can register new user
- [ ] Can login with new user
- [ ] Can login with seeded user
- [ ] Rate limiting works
- [ ] Audit logs being written

### Integration Verification
- [ ] UserService receives UserCreated events
- [ ] ApiGateway can validate tokens
- [ ] Database contains 1,963 seeded users
- [ ] RabbitMQ shows published messages

### Performance Verification
- [ ] Response times acceptable
- [ ] No memory leaks
- [ ] CPU usage normal
- [ ] Database queries optimized

---

## Rollback Plan

If deployment fails:

1. **Stop Service:**
   ```bash
   docker-compose stop authservice
   ```

2. **Restore Database:**
   ```bash
   # Restore from backup
   ```

3. **Revert to Previous Version:**
   ```bash
   docker-compose up authservice:previous
   ```

4. **Verify:**
   - Old version running
   - Database restored
   - Services integrated

---

## Success Criteria

Deployment is successful when:
- ✅ Service starts without errors
- ✅ Health check returns 200
- ✅ All manual tests pass
- ✅ 1,963 users seeded from CSV
- ✅ Can register and login
- ✅ Rate limiting works
- ✅ Audit logging enabled
- ✅ Integration with UserService works
- ✅ Integration with ApiGateway works
- ✅ No errors in logs (except expected warnings)

---

**Prepared by:** Georgia Tech Library Development Team  
**Date:** November 19, 2024  
**Version:** 1.0

