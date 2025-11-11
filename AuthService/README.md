# AuthService

## Description

The AuthService is responsible for user authentication and authorization in the Georgia Tech Library Marketplace. It handles user registration, login, JWT token generation and validation, and token refresh operations. The service plays a critical role in the security architecture by:

- **User Registration**: Creates new user accounts with secure password hashing
- **Authentication**: Validates user credentials and issues JWT tokens
- **Token Management**: Generates access and refresh tokens with proper expiration
- **Event Synchronization**: Publishes user creation events and consumes them to maintain consistency across services

The AuthService fits into the overall architecture as the central authentication authority, ensuring secure access to all protected resources while enabling event-driven synchronization with other services like UserService.

## API Endpoints

### User Registration
- `POST /api/auth/register` - Register a new user account

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "securepassword"
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

### User Login
- `POST /api/auth/login` - Authenticate user and get tokens

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "securepassword"
}
```

**Response (200 OK):** Same as registration

### Token Refresh
- `POST /api/auth/refresh` - Refresh access token using refresh token

**Request Body:**
```json
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Response (200 OK):** New token pair

### Token Validation
- `POST /api/auth/validate` - Validate JWT token (used by ApiGateway)

**Headers:**
```
Authorization: Bearer <jwt-token>
```

**Response (200 OK):**
```json
{
  "valid": true
}
```

### Health Check
- `GET /health` - Service health status

## Database Model

### AuthUsers Table

| Column | Type | Description |
|--------|------|-------------|
| UserId | UNIQUEIDENTIFIER (PK) | Unique user identifier |
| Email | NVARCHAR(255) | User email address (unique) |
| PasswordHash | NVARCHAR(255) | BCrypt hashed password |
| CreatedDate | DATETIME | Account creation timestamp |

**Entity Diagram:**
```
AuthUsers
├── UserId: GUID (Primary Key)
├── Email: String (Unique, Required)
├── PasswordHash: String (Required)
└── CreatedDate: DateTime (Required)
```

## Events

### Published Events

**UserCreated** (Exchange: `user_events`, Routing Key: `UserCreated`)
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "user@example.com",
  "name": "",
  "role": "Student",
  "createdDate": "2025-11-11T04:00:00Z"
}
```
*Published when:* A new user registers successfully
*Consumed by:* UserService for user profile creation

### Consumed Events

**UserCreated** (Exchange: `user_events`, Routing Key: `UserCreated`)
- *Purpose:* Ensures AuthUser record exists when user is created in other services
- *Action:* Creates AuthUser record with empty password hash if not exists

### Event Flow

1. User registers via AuthService → Publishes `UserCreated` event
2. UserService consumes `UserCreated` → Creates user profile
3. AuthService may consume `UserCreated` → Ensures auth record exists

## Dependencies

- **SQL Server**: For storing authentication data (AuthUsers table)
- **RabbitMQ**: For event publishing and consumption
- **ApiGateway**: For token validation requests
- **UserService**: Receives user creation events for profile management

## Running

### Docker

Build and run using Docker Compose:

```bash
# Build the service
docker-compose build authservice

# Run the service
docker-compose up authservice
```

The service will be available at `http://localhost:5006`

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Set to `Development` or `Production`
- `ConnectionStrings__DefaultConnection`: SQL Server connection string
- `RabbitMQ__Host`: RabbitMQ hostname (default: `rabbitmq`)
- `RabbitMQ__Port`: RabbitMQ port (default: `5672`)
- `RabbitMQ__Username`: RabbitMQ username (default: `guest`)
- `RabbitMQ__Password`: RabbitMQ password (default: `guest`)
- `Jwt__Key`: Secret key for JWT signing (32+ characters recommended)

### Database Migration

The service automatically runs EF Core migrations on startup and seeds initial data.

## Testing

### Using .http File

The service includes `AuthService.http` for testing endpoints:

```http
### Register User
POST http://localhost:5006/api/auth/register
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "password123"
}

### Login
POST http://localhost:5006/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "password123"
}

### Validate Token
POST http://localhost:5006/api/auth/validate
Authorization: Bearer <your-jwt-token>
```

### Manual Testing with curl

```bash
# Register user
curl -X POST http://localhost:5006/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password123"}'

# Login
curl -X POST http://localhost:5006/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password123"}'

# Health check
curl http://localhost:5006/health
```

### Event Testing

Monitor RabbitMQ management UI at `http://localhost:15672` to verify event publishing.

### Database Testing

Connect to SQL Server to verify AuthUsers table:

```sql
SELECT * FROM AuthUsers;
