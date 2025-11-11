# ApiGateway

## Description

The ApiGateway service serves as the single entry point for the Georgia Tech Library Marketplace microservices architecture. It acts as a reverse proxy using YARP (Yet Another Reverse Proxy) to route incoming HTTP requests to the appropriate backend services. The gateway handles cross-cutting concerns such as:

- **Authentication & Authorization**: Validates JWT tokens with the AuthService and adds user context to requests
- **Request Routing**: Routes requests to specific services based on URL paths
- **Health Monitoring**: Provides health check endpoints for all downstream services
- **API Documentation**: Aggregates Swagger documentation from all services into a unified interface

The ApiGateway fits into the overall architecture by providing a unified API surface, enabling client applications to interact with the distributed system as if it were a monolithic application, while maintaining the benefits of microservices.

## API Endpoints

The ApiGateway primarily acts as a proxy and does not expose its own business logic endpoints. Instead, it routes requests to backend services. The main endpoints it provides are:

### Health Checks
- `GET /health` - Returns the health status of all downstream services

### Swagger Documentation Aggregation
- `GET /swagger/auth/swagger.json` - Retrieves Swagger documentation from AuthService
- `GET /swagger/books/swagger.json` - Retrieves Swagger documentation from BookService
- `GET /swagger/warehouse/swagger.json` - Retrieves Swagger documentation from WarehouseService
- `GET /swagger/search/swagger.json` - Retrieves Swagger documentation from SearchService
- `GET /swagger/orders/swagger.json` - Retrieves Swagger documentation from OrderService

### Proxied Endpoints

The gateway routes requests to backend services using the following path mappings:

- `/auth/*` → AuthService
- `/books/*` → BookService
- `/warehouse/*` → WarehouseService
- `/search/*` → SearchService
- `/orders/*` → OrderService

**Example Request:**
```http
GET /books/api/books
Authorization: Bearer <jwt-token>
```

This request gets routed to `http://bookservice:8080/api/books` with the JWT token validated.

## Database Model

The ApiGateway does not maintain its own database. It is a stateless proxy service that relies on backend services for data persistence.

## Events

The ApiGateway does not produce or consume events. Event handling is delegated to the individual microservices.

## Dependencies

The ApiGateway integrates with all backend services:

- **AuthService**: For JWT token validation
- **BookService**: For book-related operations
- **WarehouseService**: For inventory management
- **SearchService**: For search functionality
- **OrderService**: For order processing

## Running

### Docker

Build and run using Docker Compose:

```bash
# Build all services
docker-compose build apigateway

# Run the service
docker-compose up apigateway
```

The service will be available at `http://localhost:5004`

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: Set to `Development` or `Production`
- `AuthService__Url`: URL of the AuthService for token validation (default: `http://authservice:8080`)

### Configuration

The routing configuration is defined in `appsettings.json`:

```json
{
  "ReverseProxy": {
    "Routes": {
      "auth-route": {
        "ClusterId": "auth-cluster",
        "Match": { "Path": "/auth/{**catch-all}" },
        "Transforms": [{ "PathRemovePrefix": "/auth" }]
      }
      // ... other routes
    },
    "Clusters": {
      "auth-cluster": {
        "Destinations": {
          "auth-destination": { "Address": "http://authservice:8080" }
        }
      }
      // ... other clusters
    }
  }
}
```

## Testing

### Health Checks

Test the health of all services:

```bash
curl http://localhost:5004/health
```

Expected response:
```json
{
  "status": "Healthy",
  "results": {
    "BookService": { "status": "Healthy" },
    "WarehouseService": { "status": "Healthy" },
    "SearchService": { "status": "Healthy" },
    "OrderService": { "status": "Healthy" },
    "AuthService": { "status": "Healthy" }
  }
}
```

### Swagger UI

Access the aggregated Swagger documentation at `http://localhost:5004/swagger`

### Authentication Testing

Test JWT validation by making requests to protected endpoints:

```bash
# This should work (auth endpoint)
curl http://localhost:5004/auth/api/auth/login

# This should fail without token
curl http://localhost:5004/books/api/books
# Returns 401 Unauthorized

# This should work with valid token
curl -H "Authorization: Bearer <valid-jwt>" http://localhost:5004/books/api/books
