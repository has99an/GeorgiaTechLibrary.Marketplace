# UserService - Clean Architecture Implementation

## Overview

The UserService manages user profiles and roles in the Georgia Tech Library Marketplace. It follows Clean Architecture principles with proper layer separation, comprehensive security features, and production-ready implementation.

## Architecture

### Clean Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                      API Layer                              │
│  Controllers, Middleware, Extensions                        │
│  Dependencies: Application Layer                            │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                  Application Layer                          │
│  Services, DTOs, Interfaces, Mappings                       │
│  Dependencies: Domain Layer                                 │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│                    Domain Layer                             │
│  Entities, Value Objects, Exceptions                        │
│  Dependencies: None (Pure Business Logic)                   │
└─────────────────────────────────────────────────────────────┘
                            
┌─────────────────────────────────────────────────────────────┐
│                Infrastructure Layer                         │
│  Persistence, Messaging, External Services                  │
│  Dependencies: Application Layer, Domain Layer              │
└─────────────────────────────────────────────────────────────┘
```

### Folder Structure

```
UserService/
├── API/                          # Presentation Layer
│   ├── Controllers/              # HTTP endpoints
│   │   └── UsersController.cs
│   ├── Middleware/               # Cross-cutting concerns
│   │   ├── ExceptionHandlingMiddleware.cs
│   │   ├── AuditLoggingMiddleware.cs
│   │   ├── RateLimitingMiddleware.cs
│   │   └── RoleAuthorizationMiddleware.cs
│   └── Extensions/               # Service registration
│       └── ServiceCollectionExtensions.cs
├── Application/                  # Application Layer
│   ├── DTOs/                     # Data Transfer Objects
│   │   ├── UserDto.cs
│   │   ├── CreateUserDto.cs
│   │   ├── UpdateUserDto.cs
│   │   ├── UserSearchDto.cs
│   │   ├── PagedResultDto.cs
│   │   └── UserEventDto.cs
│   ├── Interfaces/               # Repository contracts
│   │   ├── IUserRepository.cs
│   │   └── IMessageProducer.cs
│   ├── Mappings/                 # AutoMapper profiles
│   │   └── UserMappingProfile.cs
│   └── Services/                 # Business logic
│       ├── IUserService.cs
│       └── UserService.cs
├── Domain/                       # Domain Layer (Core)
│   ├── Entities/                 # Domain entities
│   │   └── User.cs
│   ├── ValueObjects/             # Immutable value objects
│   │   ├── Email.cs
│   │   └── UserRole.cs
│   └── Exceptions/               # Domain exceptions
│       ├── DomainException.cs
│       ├── ValidationException.cs
│       ├── UserNotFoundException.cs
│       └── DuplicateEmailException.cs
├── Infrastructure/               # Infrastructure Layer
│   ├── Persistence/              # Database implementations
│   │   ├── AppDbContext.cs
│   │   ├── UserRepository.cs
│   │   └── SeedData.cs
│   └── Messaging/                # Event-driven communication
│       ├── RabbitMQProducer.cs
│       └── RabbitMQConsumer.cs
├── Data/                         # CSV seed data
│   └── Users.csv                 # 1963 user records
├── Program.cs                    # Application entry point
└── appsettings.json              # Configuration
```

## Features

### Core Functionality

- **User Management**: CRUD operations with validation
- **Role-Based Access**: Student, Seller, Admin roles with permissions
- **Profile Management**: Update user information with validation
- **Search & Filtering**: Search users by name, email, role with pagination
- **Data Export**: GDPR-compliant user data export

### Security Features

- **Input Validation**: Comprehensive validation on all endpoints
- **Rate Limiting**: 
  - General: 100 requests/minute per IP
  - User creation: 5 requests/hour per IP
  - User updates: 20 requests/minute per user
- **Audit Logging**: All user modifications logged with correlation IDs
- **Role Authorization**: Middleware-enforced permission checks
- **Exception Handling**: Sanitized error messages, no information leakage

### GDPR Compliance

- **Data Export**: `/api/users/{userId}/export` - Export all user data
- **Right to be Forgotten**: `/api/users/{userId}/anonymize` - Anonymize user data
- **Data Minimization**: Only essential fields stored
- **Audit Trail**: All data access logged

### Integration

- **AuthService Integration**: Consumes `UserCreated` events to sync profiles
- **ApiGateway Compatible**: Accepts `X-User-Id` header for JWT authentication
- **Event Publishing**: Publishes user lifecycle events (Created, Updated, Deleted, RoleChanged)

## API Endpoints

### User Operations

#### Get All Users (Paginated)
```http
GET /api/users?page=1&pageSize=20
```

#### Get User by ID
```http
GET /api/users/{userId}
```

#### Get Current User
```http
GET /api/users/me
Headers: X-User-Id: {userId}
```

#### Search Users
```http
GET /api/users/search?searchTerm=john&role=Student&page=1&pageSize=20
```

#### Get Users by Role
```http
GET /api/users/role/Student
```

#### Create User
```http
POST /api/users
Content-Type: application/json

{
  "email": "user@gatech.edu",
  "name": "John Doe",
  "role": "Student"
}
```

#### Update User
```http
PUT /api/users/{userId}
Content-Type: application/json

{
  "email": "newemail@gatech.edu",
  "name": "Jane Doe",
  "role": "Seller"
}
```

#### Delete User (Soft Delete)
```http
DELETE /api/users/{userId}
```

#### Change User Role (Admin Only)
```http
PUT /api/users/{userId}/role
Content-Type: application/json

{
  "role": "Admin"
}
```

#### Export User Data (GDPR)
```http
GET /api/users/{userId}/export
```

#### Anonymize User (GDPR)
```http
POST /api/users/{userId}/anonymize
```

#### Get Role Statistics
```http
GET /api/users/statistics/roles
```

### Health Check
```http
GET /health
```

## Database Schema

### Users Table

| Column | Type | Description |
|--------|------|-------------|
| UserId | UNIQUEIDENTIFIER (PK) | Unique user identifier |
| Email | NVARCHAR(255) | User email address (unique) |
| Name | NVARCHAR(200) | Full name |
| Role | NVARCHAR(MAX) | User role (Student, Seller, Admin) |
| CreatedDate | DATETIME2 | Profile creation timestamp |
| UpdatedDate | DATETIME2 | Last profile update timestamp |
| IsDeleted | BIT | Soft delete flag |

**Indexes:**
- Unique index on Email
- Index on Role for faster role-based queries
- Query filter to exclude deleted users by default

## Seller Endpoints

### Get Sold Books
```http
GET /api/sellers/{sellerId}/books/sold
```
Returns all books that have been sold by the seller, including buyer information.

**Response:**
```json
[
  {
    "listingId": "550e8400-e29b-41d4-a716-446655440000",
    "sellerId": "660e8400-e29b-41d4-a716-446655440001",
    "bookISBN": "9780134685991",
    "price": 45.99,
    "quantity": 0,
    "condition": "New",
    "isActive": false,
    "isSold": true,
    "soldDate": "2025-01-15T10:30:00Z",
    "sales": [
      {
        "saleId": "770e8400-e29b-41d4-a716-446655440002",
        "listingId": "550e8400-e29b-41d4-a716-446655440000",
        "orderId": "880e8400-e29b-41d4-a716-446655440003",
        "orderItemId": "990e8400-e29b-41d4-a716-446655440004",
        "buyerId": "buyer123",
        "bookISBN": "9780134685991",
        "sellerId": "660e8400-e29b-41d4-a716-446655440001",
        "quantity": 1,
        "price": 45.99,
        "condition": "New",
        "saleDate": "2025-01-15T10:30:00Z"
      }
    ]
  }
]
```

### Update Book Listing
```http
PUT /api/sellers/{sellerId}/books/{listingId}
```
**Note:** Returns 400 Bad Request if the listing has been sold. Sold listings cannot be edited.

### Remove Book from Sale
```http
DELETE /api/sellers/{sellerId}/books/{listingId}
```
**Note:** Returns 400 Bad Request if the listing has been sold. Sold listings cannot be deleted.

## Database Schema

### BookSales Table

| Column | Type | Description |
|--------|------|-------------|
| SaleId | UNIQUEIDENTIFIER (PK) | Unique sale identifier |
| ListingId | UNIQUEIDENTIFIER (FK) | Reference to SellerBookListing |
| OrderId | UNIQUEIDENTIFIER | Order ID from OrderService |
| OrderItemId | UNIQUEIDENTIFIER | Order item ID |
| BuyerId | NVARCHAR(100) | ID of the buyer |
| BookISBN | NVARCHAR(13) | ISBN of the sold book |
| SellerId | UNIQUEIDENTIFIER (FK) | Reference to SellerProfile |
| Quantity | INT | Number of copies sold |
| Price | DECIMAL(10,2) | Sale price |
| Condition | NVARCHAR(50) | Book condition |
| SaleDate | DATETIME2 | Date of sale |
| CreatedDate | DATETIME2 | Record creation timestamp |

**Indexes:**
- Index on ListingId
- Index on SellerId
- Index on OrderId
- Index on BuyerId
- Index on BookISBN
- Index on SaleDate

### SellerBookListings Table Updates

| Column | Type | Description |
|--------|------|-------------|
| IsSold | BIT | Indicates if listing has been sold |
| SoldDate | DATETIME2 (nullable) | Date when listing was marked as sold |

**Indexes:**
- Index on IsSold

## Events

### Consumed Events

**UserCreated** (from AuthService)
- Exchange: `user_events`
- Routing Key: `UserCreated`
- Action: Creates user profile when auth user registers

```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@gatech.edu",
  "name": "John Doe",
  "role": "Student",
  "createdDate": "2025-11-19T00:00:00Z"
}
```

**OrderPaid** (from OrderService)
- Exchange: `book_events`
- Routing Key: `OrderPaid`
- Action: Updates listing quantities, creates BookSale records, and marks listings as sold when quantity reaches 0

```json
{
  "orderId": "880e8400-e29b-41d4-a716-446655440003",
  "customerId": "buyer123",
  "totalAmount": 45.99,
  "paidDate": "2025-01-15T10:30:00Z",
  "orderItems": [
    {
      "orderItemId": "990e8400-e29b-41d4-a716-446655440004",
      "bookISBN": "9780134685991",
      "sellerId": "660e8400-e29b-41d4-a716-446655440001",
      "quantity": 1,
      "unitPrice": 45.99
    }
  ]
}
```

### Published Events

**UserUpdated**
- Published when user profile is updated
- Routing Key: `UserUpdated`
- Exchange: `user_events`

**UserDeleted**
- Published when user is deleted
- Routing Key: `UserDeleted`
- Exchange: `user_events`

**UserRoleChanged**
- Published when user role changes
- Routing Key: `UserRoleChanged`
- Exchange: `user_events`

**BookSold**
- Published when a book listing is marked as sold (quantity reaches 0)
- Routing Key: `BookSold`
- Exchange: `book_events`
- Action: Notifies SearchService to remove sold books from available listings

```json
{
  "listingId": "550e8400-e29b-41d4-a716-446655440000",
  "sellerId": "660e8400-e29b-41d4-a716-446655440001",
  "bookISBN": "9780134685991",
  "buyerId": "buyer123",
  "orderId": "880e8400-e29b-41d4-a716-446655440003",
  "orderItemId": "990e8400-e29b-41d4-a716-446655440004",
  "quantity": 1,
  "price": 45.99,
  "condition": "New",
  "soldDate": "2025-01-15T10:30:00Z"
}
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver;Database=UserServiceDb;..."
  },
  "RabbitMQ": {
    "Host": "rabbitmq",
    "Port": "5672",
    "Username": "guest",
    "Password": "guest"
  },
  "RateLimiting": {
    "GeneralLimitPerMinute": 100,
    "CreateUserLimitPerHour": 5,
    "UpdateUserLimitPerMinute": 20
  },
  "Security": {
    "EnableAuditLogging": true,
    "EnableRateLimiting": true,
    "EnableRoleAuthorization": true
  }
}
```

## Running the Service

### Docker Compose

```bash
docker-compose up userservice
```

The service will be available at `http://localhost:5005`

### Local Development

```bash
cd UserService
dotnet run
```

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Development or Production
- `ConnectionStrings__DefaultConnection`: SQL Server connection string
- `RabbitMQ__Host`: RabbitMQ hostname (default: rabbitmq)
- `RabbitMQ__Port`: RabbitMQ port (default: 5672)

## Data Seeding

The service automatically seeds 1,963 users from `Users.csv` on startup:

- **Role Distribution**: 
  - ~80% Students
  - ~15% Sellers
  - ~5% Admins
- **Validation**: All records validated before insertion
- **Idempotent**: Skips seeding if data already exists
- **Batch Processing**: Inserts in batches of 100 for performance

## Middleware Pipeline

1. **ExceptionHandlingMiddleware**: Global exception handling
2. **AuditLoggingMiddleware**: Logs all mutating operations
3. **RateLimitingMiddleware**: Protects against abuse
4. **HTTPS Redirection**: Forces HTTPS
5. **CORS**: Configured for cross-origin requests
6. **Authorization**: ASP.NET Core authorization
7. **RoleAuthorizationMiddleware**: Role-based permission checks

## Authorization Rules

### Admin
- Full access to all endpoints
- Can delete users
- Can change user roles

### Seller
- Can update own profile
- Can read all users
- Cannot delete or change roles

### Student
- Can read all users
- Can update own profile
- Cannot delete or change roles

## Swagger Documentation

Access comprehensive API documentation at:
```
http://localhost:5005/swagger
```

Features:
- Interactive API testing
- Request/response schemas
- Authentication configuration
- Error response examples

## Dependencies

- **SQL Server**: User data persistence
- **RabbitMQ**: Event-driven communication
- **AuthService**: User authentication and registration events
- **ApiGateway**: JWT validation and routing

## Production Readiness

✅ Clean Architecture with proper layer separation  
✅ Comprehensive input validation  
✅ Rate limiting on all endpoints  
✅ Audit logging for all user actions  
✅ GDPR-compliant data handling  
✅ Role-based authorization  
✅ Health checks for monitoring  
✅ Swagger documentation  
✅ Event-driven integration  
✅ Error handling with sanitized messages  
✅ Database connection resilience  
✅ Structured logging  

## Testing

### Health Check
```bash
curl http://localhost:5005/health
```

### Get All Users
```bash
curl http://localhost:5005/api/users
```

### Create User
```bash
curl -X POST http://localhost:5005/api/users \
  -H "Content-Type: application/json" \
  -d '{"email":"test@gatech.edu","name":"Test User","role":"Student"}'
```

## Monitoring

- **Health Endpoint**: `/health` - Database connectivity check
- **Audit Logs**: Structured JSON logs with correlation IDs
- **Performance Metrics**: Request duration tracking
- **Error Tracking**: All exceptions logged with context

## Security Best Practices

- Email validation and sanitization
- Password handling delegated to AuthService
- No sensitive data in error messages
- Rate limiting to prevent abuse
- Audit trail for compliance
- HTTPS enforcement
- CORS properly configured
- Input validation on all endpoints
