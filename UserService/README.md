# UserService

## Description

The UserService manages user profiles and extended user information in the Georgia Tech Library Marketplace. It complements the AuthService by storing additional user details beyond authentication credentials. The service handles user profile management and maintains user data consistency across the system. The service is designed to handle:

- **User Profile Management**: Store and manage detailed user information
- **Event-Driven Synchronization**: Create user profiles when users register
- **User Data Enrichment**: Provide additional user context for other services
- **Profile Updates**: Allow users to update their profile information

**Note:** This service is currently in skeleton form with basic setup but no implemented controllers or models. It is designed to consume `UserCreated` events from AuthService and provide user profile APIs.

The UserService fits into the overall architecture as the user data authority, providing profile information to services that need user details beyond authentication.

## API Endpoints

*This service is not yet fully implemented. The following are planned endpoints:*

### Get User Profile
- `GET /api/users/{userId}` - Retrieve user profile information

### Update User Profile
- `POST /api/users/{userId}` - Update user profile

### Create User Profile
- `POST /api/users` - Create a new user profile (typically triggered by events)

### Health Check
- `GET /health` - Service health status

## Database Model

*Database models are not yet implemented. Planned structure:*

### Users Table

| Column | Type | Description |
|--------|------|-------------|
| UserId | UNIQUEIDENTIFIER (PK) | Unique user identifier (matches AuthService) |
| Email | NVARCHAR(255) | User email address |
| Name | NVARCHAR(200) | Full name |
| Role | NVARCHAR(20) | User role (Student, Seller, Admin) |
| CreatedDate | DATETIME | Profile creation timestamp |
| UpdatedDate | DATETIME | Last profile update timestamp |

**Additional planned fields:**
- Phone number
- Address information
- Profile picture URL
- Preferences
- Account status

## Events

### Consumed Events

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
*Consumed when:* User registers via AuthService
*Action:* Create user profile record

### Planned Published Events

**UserProfileUpdated** (Future implementation)
- Published when user profile is updated
- Consumed by services needing user data updates

## Dependencies

- **SQL Server**: For storing user profile data (UserServiceDb)
- **RabbitMQ**: For consuming user creation events
- **AuthService**: Publishes user creation events
- **ApiGateway**: May route user profile requests (future)

## Running

### Docker

Build and run using Docker Compose:

```bash
# Build the service
docker-compose build userservice

# Run the service
docker-compose up userservice
```

The service will be available at `http://localhost:5005`

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Set to `Development` or `Production`
- `ConnectionStrings__DefaultConnection`: SQL Server connection string
- `RabbitMQ__Host`: RabbitMQ hostname (default: `rabbitmq`)
- `RabbitMQ__Port`: RabbitMQ port (default: `5672`)
- `RabbitMQ__Username`: RabbitMQ username (default: `guest`)
- `RabbitMQ__Password`: RabbitMQ password (default: `guest`)

### Database Migration

The service automatically runs EF Core migrations on startup and seeds initial data.

## Testing

### Current Status

As this service is in skeleton form, testing is limited to:

```bash
# Health check
curl http://localhost:5005/health
```

### Future Testing

Once implemented, testing will include:

- **Event Consumption**: Verify `UserCreated` events create profiles
- **Profile APIs**: Test CRUD operations on user profiles
- **Integration**: Verify profile data availability to other services

### RabbitMQ Testing

Monitor RabbitMQ management UI at `http://localhost:15672` to verify event consumption from the `user_events` exchange.

## Implementation Status

- ✅ Basic service setup and configuration
- ✅ Database context and migrations
- ✅ Message consumer infrastructure
- ❌ Controllers and API endpoints
- ❌ Data models and DTOs
- ❌ Business logic implementation
- ❌ Event publishing

## Next Steps

1. Implement User entity and related models
2. Create Controllers for user profile management
3. Implement IUserRepository and repository logic
4. Add AutoMapper profiles for DTO mapping
5. Implement event consumption for UserCreated
6. Add comprehensive API testing
7. Implement user profile update workflows
