# AuthService - Clean Architecture Implementation

## Overview

AuthService is a microservice responsible for authentication and authorization in the Georgia Tech Library Marketplace. It implements Clean Architecture principles with Domain-Driven Design (DDD) patterns.

## Architecture

### Clean Architecture Layers

```
AuthService/
├── Domain/                     # Enterprise business rules
│   ├── Entities/              # Rich domain entities
│   │   └── AuthUser.cs        # Authentication user entity
│   ├── ValueObjects/          # Immutable value objects
│   │   ├── Email.cs           # Email with validation
│   │   └── Password.cs        # Password with strength rules
│   └── Exceptions/            # Domain-specific exceptions
│       ├── DomainException.cs
│       ├── AuthenticationException.cs
│       ├── InvalidCredentialsException.cs
│       └── DuplicateEmailException.cs
│
├── Application/               # Application business rules
│   ├── Interfaces/           # Contracts
│   │   ├── IAuthUserRepository.cs
│   │   ├── ITokenService.cs
│   │   ├── IPasswordHasher.cs
│   │   └── IMessageProducer.cs
│   ├── DTOs/                 # Data transfer objects
│   │   ├── RegisterDto.cs
│   │   ├── LoginDto.cs
│   │   ├── TokenDto.cs
│   │   ├── RefreshTokenDto.cs
│   │   ├── ValidateTokenDto.cs
│   │   └── UserEventDto.cs
│   └── Services/             # Application services
│       ├── IAuthService.cs
│       ├── AuthService.cs    # Business logic orchestration
│       ├── TokenService.cs   # JWT token generation/validation
│       └── PasswordHasher.cs # BCrypt password hashing
│
├── Infrastructure/            # External concerns
│   ├── Persistence/          # Database
│   │   ├── AppDbContext.cs   # EF Core context
│   │   ├── AuthUserRepository.cs
│   │   └── SeedData.cs       # CSV data seeding
│   └── Messaging/            # RabbitMQ
│       └── RabbitMQProducer.cs
│
└── API/                       # Presentation layer
    ├── Controllers/          # Thin controllers
    │   └── AuthController.cs
    ├── Middleware/           # Cross-cutting concerns
    │   ├── ExceptionHandlingMiddleware.cs
    │   ├── AuditLoggingMiddleware.cs
    │   └── RateLimitingMiddleware.cs
    └── Extensions/
        └── ServiceCollectionExtensions.cs
```

## Features

### Core Authentication
- ✅ **User Registration** - Email/password with validation
- ✅ **User Login** - Credential verification with lockout
- ✅ **JWT Token Generation** - Access & refresh tokens
- ✅ **Token Validation** - For ApiGateway integration
- ✅ **Token Refresh** - Extend session without re-login

### Security
- ✅ **BCrypt Password Hashing** - Industry-standard hashing
- ✅ **Password Strength Validation** - Complexity requirements
- ✅ **Account Lockout** - 5 failed attempts = 15 min lockout
- ✅ **Rate Limiting** - Protect against brute force
  - Login: 5 attempts/minute per IP
  - Register: 3 attempts/hour per IP
  - Refresh: 10 attempts/minute per IP
  - Validate: 100 attempts/minute per IP
- ✅ **Audit Logging** - All authentication events logged
- ✅ **Input Validation** - Email format, password strength

### Data Seeding
- ✅ **CSV-Based Seeding** - Loads 1,963 users from `Data/AuthUsers.csv`
- ✅ **Real BCrypt Hashes** - Default password: `Password123!`
- ✅ **Idempotent** - Safe to run multiple times
- ✅ **Batch Processing** - 100 records per batch
- ✅ **Transaction Safety** - Rollback on failure

### Integration
- ✅ **RabbitMQ Events** - Publishes `UserCreated` events
- ✅ **UserService Integration** - Event consumption for profile creation
- ✅ **ApiGateway Integration** - JWT validation endpoint

## API Endpoints

### POST `/register`
Register a new user.

**Request:**
```json
{
  "email": "student@gatech.edu",
  "password": "SecurePass123!"
}
```

**Response:** `200 OK`
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresIn": 3600
}
```

**Errors:**
- `400 Bad Request` - Invalid email/password format
- `409 Conflict` - Email already exists

---

### POST `/login`
Authenticate a user.

**Request:**
```json
{
  "email": "student@gatech.edu",
  "password": "SecurePass123!"
}
```

**Response:** `200 OK`
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresIn": 3600
}
```

**Errors:**
- `400 Bad Request` - Invalid request format
- `401 Unauthorized` - Invalid credentials
- `401 Unauthorized` - Account locked (after 5 failed attempts)

---

### POST `/refresh`
Refresh an access token.

**Request:**
```json
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIs..."
}
```

**Response:** `200 OK`
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIs...",
  "expiresIn": 3600
}
```

**Errors:**
- `400 Bad Request` - Missing refresh token
- `401 Unauthorized` - Invalid/expired token

---

### POST `/validate`
Validate a JWT token (used by ApiGateway).

**Request:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs..."
}
```

**Response:** `200 OK`
```json
{
  "valid": true
}
```

**Errors:**
- `401 Unauthorized` - Invalid token
```json
{
  "valid": false
}
```

---

### GET `/health`
Health check endpoint.

**Response:** `200 OK`
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "self": "Healthy"
  }
}
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver;Database=AuthServiceDb;..."
  },
  "RabbitMQ": {
    "Host": "rabbitmq",
    "Port": "5672",
    "Username": "guest",
    "Password": "guest"
  },
  "Jwt": {
    "Key": "YourSuperSecretKey...",
    "Issuer": "GeorgiaTechLibraryMarketplace",
    "Audience": "GeorgiaTechLibraryMarketplace",
    "ExpirationHours": "1",
    "RefreshExpirationDays": "7"
  },
  "RateLimiting": {
    "LoginLimitPerMinute": 5,
    "RegisterLimitPerHour": 3,
    "RefreshLimitPerMinute": 10,
    "ValidateLimitPerMinute": 100
  }
}
```

## Events Published

### UserCreated
Published when a new user registers.

**Routing Key:** `UserCreated`

**Payload:**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "student@gatech.edu",
  "name": "",
  "role": "Student",
  "createdDate": "2024-11-19T10:30:00Z"
}
```

## Data Seeding

### AuthUsers.csv Format
```csv
UserId,Email,PasswordHash,CreatedDate
3fa85f64-5717-4562-b3fc-2c963f66afa6,student1@gatech.edu,simulated_hash_abc123,2024-01-01T00:00:00Z
...
```

### Seeding Process
1. **File Location:** `AuthService/Data/AuthUsers.csv`
2. **Record Count:** 1,963 authentication records
3. **Password Hash:** All users seeded with bcrypt hash of `Password123!`
4. **UserId Mapping:** Must match UserService Users.csv
5. **Batch Size:** 100 records per transaction
6. **Idempotency:** Skips if data already exists

### Default Credentials
All seeded users have the same password for testing:
- **Password:** `Password123!`
- **Action Required:** Users should reset password on first login

## Security Features

### Password Requirements
- Minimum 8 characters
- Maximum 100 characters
- Must contain 3 of 4:
  - Uppercase letter (A-Z)
  - Lowercase letter (a-z)
  - Digit (0-9)
  - Special character (!@#$%^&*...)

### Email Validation
- Valid email format
- Maximum 255 characters
- Case-insensitive
- Unique constraint

### Account Lockout
- **Trigger:** 5 consecutive failed login attempts
- **Duration:** 15 minutes
- **Reset:** Automatic after lockout period
- **Logging:** All failed attempts logged

### Audit Logging
All authentication operations are logged with:
- Correlation ID
- IP address
- Timestamp
- HTTP method & path
- Status code
- Duration (ms)
- Success/failure

Example log:
```json
{
  "correlationId": "0HN7Q8KVG3J4K",
  "ipAddress": "192.168.1.100",
  "method": "POST",
  "path": "/login",
  "statusCode": 200,
  "durationMs": 145,
  "timestamp": "2024-11-19T10:30:00Z",
  "success": true
}
```

## Dependencies

### NuGet Packages
- `Microsoft.EntityFrameworkCore.SqlServer` (8.0.0) - Database ORM
- `Microsoft.EntityFrameworkCore.Tools` (8.0.0) - Migrations
- `BCrypt.Net-Next` (4.0.3) - Password hashing
- `System.IdentityModel.Tokens.Jwt` (8.0.0) - JWT tokens
- `RabbitMQ.Client` (6.8.1) - Message broker
- `Swashbuckle.AspNetCore` (6.5.0) - API documentation
- `Microsoft.AspNetCore.Mvc.NewtonsoftJson` (8.0.0) - JSON serialization

### External Services
- **SQL Server** - Database (port 1433)
- **RabbitMQ** - Message broker (port 5672)

## Running the Service

### Development (Local)

1. **Prerequisites:**
   - .NET 8.0 SDK
   - SQL Server (localhost:1433)
   - RabbitMQ (localhost:5672)

2. **Update Connection String:**
   ```json
   "DefaultConnection": "Server=localhost,1433;Database=AuthServiceDb;..."
   ```

3. **Run Migrations:**
   ```bash
   dotnet ef database update
   ```

4. **Run Service:**
   ```bash
   dotnet run
   ```

5. **Access Swagger:**
   - http://localhost:5000/swagger

### Production (Docker)

1. **Build Image:**
   ```bash
   docker build -t authservice:latest .
   ```

2. **Run with Docker Compose:**
   ```bash
   docker-compose up authservice
   ```

3. **Service URL:**
   - http://authservice:8080

## Testing

### Manual Testing with Swagger
1. Navigate to `/swagger`
2. Test `/register` endpoint
3. Test `/login` endpoint
4. Copy access token
5. Test `/validate` endpoint with token

### Testing Rate Limiting
```bash
# Attempt 6 logins in 1 minute (should fail on 6th)
for i in {1..6}; do
  curl -X POST http://localhost:5000/login \
    -H "Content-Type: application/json" \
    -d '{"email":"test@gatech.edu","password":"wrong"}'
done
```

### Testing Data Seeding
1. Start service with empty database
2. Check logs for: "Successfully seeded 1963 auth users from CSV"
3. Verify database: `SELECT COUNT(*) FROM AuthUsers`
4. Test login with seeded user: `student1@gatech.edu` / `Password123!`

## Integration with Other Services

### UserService
- **Event:** AuthService publishes `UserCreated` event
- **Consumer:** UserService consumes event and creates user profile
- **Mapping:** UserId must match between services

### ApiGateway
- **Validation:** ApiGateway calls `/validate` endpoint
- **JWT Claims:** ApiGateway extracts `X-User-Id` from token
- **Routing:** All `/auth/*` requests routed to AuthService

## Monitoring

### Health Checks
- **Endpoint:** `/health`
- **Checks:**
  - Database connectivity
  - Self-check (service running)

### Logging
- **Format:** JSON structured logging
- **Levels:**
  - `Information` - Successful operations
  - `Warning` - Failed login attempts, rate limits
  - `Error` - Exceptions, database failures
- **Audit Trail:** All authentication events

## Troubleshooting

### Database Connection Fails
```
Error: A network-related or instance-specific error occurred
```
**Solution:** Wait for SQL Server to start (30 retries with 5s delay)

### RabbitMQ Connection Fails
```
Error: None of the specified endpoints were reachable
```
**Solution:** Service continues without messaging (logged as warning)

### Migration Fails
```
Error: There is already an object named 'AuthUsers' in the database
```
**Solution:** Drop database and re-run migrations

### Rate Limit Exceeded
```
429 Too Many Requests
Retry-After: 60
```
**Solution:** Wait specified seconds before retrying

## Architecture Decisions

### Why Clean Architecture?
- **Separation of Concerns** - Each layer has single responsibility
- **Testability** - Business logic isolated from infrastructure
- **Maintainability** - Changes in one layer don't affect others
- **Dependency Inversion** - Core doesn't depend on external concerns

### Why BCrypt?
- Industry-standard password hashing
- Adaptive cost factor (future-proof)
- Built-in salt generation
- Resistant to rainbow table attacks

### Why JWT?
- Stateless authentication
- Microservice-friendly
- Standard claims support
- ApiGateway compatible

### Why RabbitMQ?
- Reliable message delivery
- Event-driven architecture
- Decouples services
- Supports pub/sub patterns

## Future Enhancements

### Planned Features
- [ ] Password reset flow (email verification)
- [ ] Email verification on registration
- [ ] OAuth2 integration (Google, GitHub)
- [ ] Multi-factor authentication (MFA)
- [ ] Role claims in JWT (requires UserService query)
- [ ] Redis-based rate limiting (distributed)
- [ ] Refresh token rotation
- [ ] Token revocation list

### Performance Optimizations
- [ ] Cache token validation results
- [ ] Connection pooling for RabbitMQ
- [ ] Database query optimization
- [ ] Async logging

## License

Copyright © 2024 Georgia Tech Library. All rights reserved.

## Contact

For questions or issues, contact: library-admin@gatech.edu
