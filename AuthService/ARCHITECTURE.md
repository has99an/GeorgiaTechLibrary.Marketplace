# AuthService - Clean Architecture Documentation

## Architecture Overview

AuthService implements Clean Architecture (also known as Onion Architecture or Hexagonal Architecture) with Domain-Driven Design (DDD) principles.

## Layer Dependencies

```
┌─────────────────────────────────────────────────────────┐
│                      API Layer                          │
│  (Controllers, Middleware, Extensions)                  │
│  - Thin controllers                                     │
│  - Cross-cutting concerns (middleware)                  │
│  - Dependency injection configuration                   │
└────────────────┬────────────────────────────────────────┘
                 │ depends on ↓
┌────────────────▼────────────────────────────────────────┐
│                 Application Layer                       │
│  (Services, Interfaces, DTOs)                          │
│  - Business logic orchestration                         │
│  - Use case implementations                             │
│  - Interface definitions (contracts)                    │
└────────────────┬────────────────────────────────────────┘
                 │ depends on ↓
┌────────────────▼────────────────────────────────────────┐
│                   Domain Layer                          │
│  (Entities, Value Objects, Exceptions)                 │
│  - Core business rules                                  │
│  - Enterprise logic                                     │
│  - No external dependencies                             │
└─────────────────────────────────────────────────────────┘
                 ▲
                 │ implements interfaces from
┌────────────────┴────────────────────────────────────────┐
│              Infrastructure Layer                       │
│  (Persistence, Messaging, External Services)           │
│  - Database access (EF Core)                           │
│  - Message broker (RabbitMQ)                           │
│  - External service integrations                        │
└─────────────────────────────────────────────────────────┘
```

## Dependency Rule

**The Dependency Rule:** Source code dependencies must point inward only. Inner layers know nothing about outer layers.

- **Domain** has no dependencies
- **Application** depends only on Domain
- **Infrastructure** implements interfaces from Application
- **API** depends on Application (and uses Infrastructure via DI)

## Layer Details

### 1. Domain Layer (Core)

**Purpose:** Contains enterprise business rules and domain logic.

**Characteristics:**
- No dependencies on other layers
- No dependencies on frameworks
- Pure C# classes
- Highly testable

**Components:**

#### Entities
- `AuthUser` - Rich domain entity
  - Encapsulates authentication user data
  - Contains business logic (login attempts, lockout)
  - Factory methods for creation
  - Validation rules

#### Value Objects
- `Email` - Immutable email with validation
  - Format validation (regex)
  - Case normalization
  - Masking for logs
- `Password` - Immutable password with strength rules
  - Length requirements (8-100 chars)
  - Complexity validation (3 of 4 character types)

#### Domain Exceptions
- `DomainException` - Base exception
- `AuthenticationException` - Authentication failures
- `InvalidCredentialsException` - Invalid login
- `DuplicateEmailException` - Email already exists

**Example:**
```csharp
// Rich domain entity with business logic
public class AuthUser
{
    public void RecordFailedLogin()
    {
        FailedLoginAttempts++;
        if (FailedLoginAttempts >= 5)
        {
            LockoutEndDate = DateTime.UtcNow.AddMinutes(15);
        }
    }
}
```

---

### 2. Application Layer (Use Cases)

**Purpose:** Contains application-specific business rules and orchestrates domain logic.

**Characteristics:**
- Depends only on Domain layer
- Defines interfaces for external concerns
- Contains DTOs for data transfer
- Implements use cases

**Components:**

#### Interfaces (Contracts)
- `IAuthUserRepository` - Data access contract
- `ITokenService` - JWT operations contract
- `IPasswordHasher` - Password hashing contract
- `IMessageProducer` - Messaging contract

#### Services (Use Case Implementations)
- `AuthService` - Authentication orchestration
  - Register user
  - Login user
  - Refresh token
  - Validate token
- `TokenService` - JWT token operations
  - Generate access/refresh tokens
  - Validate tokens
  - Extract claims
- `PasswordHasher` - BCrypt operations
  - Hash passwords
  - Verify passwords

#### DTOs (Data Transfer Objects)
- `RegisterDto`, `LoginDto` - Input DTOs
- `TokenDto` - Output DTO
- `UserEventDto` - Event DTO

**Example:**
```csharp
// Application service orchestrates domain logic
public class AuthService : IAuthService
{
    public async Task<TokenDto> LoginAsync(LoginDto loginDto)
    {
        // 1. Validate input (value object)
        var email = Email.Create(loginDto.Email);
        
        // 2. Get entity from repository
        var authUser = await _repository.GetAuthUserByEmailAsync(email);
        
        // 3. Business logic (domain entity)
        if (authUser.IsLockedOut()) throw new AuthenticationException(...);
        
        // 4. Verify password (infrastructure service)
        if (!_passwordHasher.VerifyPassword(...)) 
        {
            authUser.RecordFailedLogin(); // Domain logic
            throw new InvalidCredentialsException(...);
        }
        
        // 5. Generate tokens (infrastructure service)
        return _tokenService.GenerateTokens(authUser);
    }
}
```

---

### 3. Infrastructure Layer (External Concerns)

**Purpose:** Implements interfaces defined in Application layer. Handles external concerns.

**Characteristics:**
- Implements Application interfaces
- Depends on Application and Domain
- Contains framework-specific code
- Database, messaging, external APIs

**Components:**

#### Persistence
- `AppDbContext` - EF Core database context
  - Entity configurations
  - Value object conversions
  - Constraints and indexes
- `AuthUserRepository` - Repository implementation
  - CRUD operations
  - Query methods
- `SeedData` - Data seeding from CSV
  - Loads 1,963 users
  - Bcrypt password hashing
  - Batch processing

#### Messaging
- `RabbitMQProducer` - Message broker implementation
  - Publishes events
  - Connection management
  - Graceful degradation

**Example:**
```csharp
// Infrastructure implements Application interface
public class AuthUserRepository : IAuthUserRepository
{
    private readonly AppDbContext _context;
    
    public async Task<AuthUser?> GetAuthUserByEmailAsync(string email)
    {
        return await _context.AuthUsers
            .FirstOrDefaultAsync(u => u.Email.Value == email.ToLower());
    }
}
```

---

### 4. API Layer (Presentation)

**Purpose:** Handles HTTP requests, middleware, and dependency injection.

**Characteristics:**
- Depends on Application layer
- Thin controllers (no business logic)
- Middleware for cross-cutting concerns
- DI configuration

**Components:**

#### Controllers
- `AuthController` - HTTP endpoints
  - Register, Login, Refresh, Validate
  - Delegates to `IAuthService`
  - Model validation
  - Swagger annotations

#### Middleware
- `ExceptionHandlingMiddleware` - Global exception handling
  - Catches all exceptions
  - Maps to HTTP status codes
  - Sanitized error messages
- `AuditLoggingMiddleware` - Security audit logging
  - Logs authentication operations
  - IP address, duration, status
- `RateLimitingMiddleware` - Rate limiting
  - Per-endpoint limits
  - In-memory sliding window
  - HTTP 429 responses

#### Extensions
- `ServiceCollectionExtensions` - DI registration
  - Database configuration
  - Service registration
  - Health checks

**Example:**
```csharp
// Thin controller delegates to application service
[ApiController]
[Route("")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    
    [HttpPost("login")]
    public async Task<ActionResult<TokenDto>> Login([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        
        var tokens = await _authService.LoginAsync(loginDto);
        return Ok(tokens);
    }
}
```

---

## Request Flow

### Example: User Login

```
1. HTTP Request
   ↓
2. API Layer: AuthController.Login()
   - Validates model state
   - Calls IAuthService.LoginAsync()
   ↓
3. Application Layer: AuthService.LoginAsync()
   - Creates Email value object (Domain)
   - Calls IAuthUserRepository.GetAuthUserByEmailAsync()
   ↓
4. Infrastructure Layer: AuthUserRepository
   - Queries database via EF Core
   - Returns AuthUser entity
   ↓
5. Application Layer: AuthService (continued)
   - Checks authUser.IsLockedOut() (Domain logic)
   - Calls IPasswordHasher.VerifyPassword()
   ↓
6. Infrastructure Layer: PasswordHasher
   - Verifies password with BCrypt
   - Returns true/false
   ↓
7. Application Layer: AuthService (continued)
   - If invalid: authUser.RecordFailedLogin() (Domain logic)
   - If valid: authUser.RecordSuccessfulLogin() (Domain logic)
   - Calls ITokenService.GenerateTokens()
   ↓
8. Infrastructure Layer: TokenService
   - Generates JWT tokens
   - Returns TokenDto
   ↓
9. Application Layer: AuthService (continued)
   - Returns TokenDto to controller
   ↓
10. API Layer: AuthController (continued)
    - Returns HTTP 200 with TokenDto
```

### Middleware Pipeline

```
HTTP Request
   ↓
1. ExceptionHandlingMiddleware (catches exceptions)
   ↓
2. AuditLoggingMiddleware (logs auth operations)
   ↓
3. RateLimitingMiddleware (enforces limits)
   ↓
4. UseHttpsRedirection
   ↓
5. UseAuthentication
   ↓
6. UseAuthorization
   ↓
7. Controller (AuthController)
   ↓
HTTP Response
```

---

## Design Patterns

### 1. Repository Pattern
**Purpose:** Abstracts data access logic.

**Implementation:**
- Interface: `IAuthUserRepository` (Application)
- Implementation: `AuthUserRepository` (Infrastructure)

**Benefits:**
- Testable (mock repository)
- Swappable data source
- Clean separation

### 2. Dependency Injection
**Purpose:** Inversion of control for loose coupling.

**Implementation:**
- Interfaces in Application layer
- Implementations in Infrastructure layer
- Registration in API layer (`ServiceCollectionExtensions`)

**Benefits:**
- Testable (inject mocks)
- Flexible (swap implementations)
- SOLID principles

### 3. Factory Pattern
**Purpose:** Encapsulates object creation logic.

**Implementation:**
- `AuthUser.Create()` - Creates new user
- `AuthUser.CreateWithId()` - Reconstructs from database
- `Email.Create()` - Creates validated email
- `Password.Create()` - Creates validated password

**Benefits:**
- Validation at creation
- Immutability (value objects)
- Domain logic encapsulation

### 4. Value Object Pattern
**Purpose:** Immutable objects with validation.

**Implementation:**
- `Email` - Email address with validation
- `Password` - Password with strength rules

**Benefits:**
- Validation logic in one place
- Immutability (thread-safe)
- Type safety

### 5. Middleware Pattern
**Purpose:** Cross-cutting concerns in request pipeline.

**Implementation:**
- `ExceptionHandlingMiddleware`
- `AuditLoggingMiddleware`
- `RateLimitingMiddleware`

**Benefits:**
- Separation of concerns
- Reusable components
- Pipeline composition

---

## SOLID Principles

### Single Responsibility Principle (SRP)
✅ Each class has one reason to change:
- `AuthUser` - Domain entity logic
- `AuthService` - Authentication orchestration
- `AuthUserRepository` - Data access
- `TokenService` - JWT operations

### Open/Closed Principle (OCP)
✅ Open for extension, closed for modification:
- Interfaces allow new implementations
- Middleware pipeline extensible
- New validators can be added

### Liskov Substitution Principle (LSP)
✅ Subtypes are substitutable:
- All exceptions derive from `DomainException`
- Repository implementations are interchangeable

### Interface Segregation Principle (ISP)
✅ Clients depend on specific interfaces:
- `IAuthUserRepository` - Only data access methods
- `ITokenService` - Only token operations
- `IPasswordHasher` - Only password operations

### Dependency Inversion Principle (DIP)
✅ Depend on abstractions, not concretions:
- Application depends on interfaces, not implementations
- Infrastructure implements Application interfaces
- API uses interfaces via DI

---

## Testing Strategy

### Unit Tests (Domain Layer)
```csharp
[Fact]
public void AuthUser_RecordFailedLogin_LocksAfter5Attempts()
{
    // Arrange
    var authUser = AuthUser.Create("test@gatech.edu", "hash");
    
    // Act
    for (int i = 0; i < 5; i++)
        authUser.RecordFailedLogin();
    
    // Assert
    Assert.True(authUser.IsLockedOut());
}
```

### Integration Tests (Application Layer)
```csharp
[Fact]
public async Task AuthService_Login_ReturnsTokens()
{
    // Arrange
    var mockRepo = new Mock<IAuthUserRepository>();
    var authService = new AuthService(mockRepo.Object, ...);
    
    // Act
    var result = await authService.LoginAsync(new LoginDto { ... });
    
    // Assert
    Assert.NotNull(result.AccessToken);
}
```

### End-to-End Tests (API Layer)
```csharp
[Fact]
public async Task AuthController_Login_Returns200()
{
    // Arrange
    var client = _factory.CreateClient();
    
    // Act
    var response = await client.PostAsJsonAsync("/login", new { ... });
    
    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

---

## Benefits of This Architecture

### 1. Testability
- Domain logic testable without infrastructure
- Application services testable with mocks
- Controllers testable with integration tests

### 2. Maintainability
- Changes isolated to specific layers
- Clear separation of concerns
- Easy to locate and fix bugs

### 3. Flexibility
- Swap implementations (e.g., SQL Server → PostgreSQL)
- Add new features without modifying existing code
- Support multiple UI layers (Web API, gRPC, etc.)

### 4. Scalability
- Stateless design (JWT tokens)
- Horizontal scaling possible
- Microservice-ready

### 5. Security
- Validation at multiple layers
- Domain rules enforced
- Infrastructure concerns isolated

---

## Comparison with Original Architecture

### Before (Flat Architecture)
```
AuthService/
├── Controllers/
│   └── AuthController.cs (business logic + HTTP)
├── Models/
│   └── AuthUser.cs (anemic model)
├── Data/
│   └── AppDbContext.cs
├── Repositories/
│   └── AuthUserRepository.cs
└── Services/
    └── RabbitMQProducer.cs
```

**Problems:**
- Business logic in controller
- Anemic domain model
- No validation
- Tight coupling
- Hard to test

### After (Clean Architecture)
```
AuthService/
├── Domain/ (business rules)
├── Application/ (use cases)
├── Infrastructure/ (external concerns)
└── API/ (presentation)
```

**Benefits:**
- Rich domain model
- Testable business logic
- Loose coupling
- Clear boundaries
- Easy to maintain

---

## Conclusion

This Clean Architecture implementation provides:
- ✅ Clear separation of concerns
- ✅ Testable business logic
- ✅ Flexible and maintainable codebase
- ✅ SOLID principles adherence
- ✅ Production-ready security
- ✅ Scalable design

The architecture supports future growth and changes while maintaining code quality and developer productivity.

---

**Author:** Georgia Tech Library Development Team  
**Date:** November 19, 2024  
**Version:** 1.0

