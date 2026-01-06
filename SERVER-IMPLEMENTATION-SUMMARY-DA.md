# Server Implementering - Opsummering

**Projekt:** Georgia Tech Library Marketplace  
**Dato:** 2025-01-21  
**Version:** 2.0

---

## Oversigt

Systemet er en microservices-arkitektur bestående af 8 services, der kommunikerer gennem en API Gateway. Alle services er implementeret i .NET og kører i Docker-containere.

---

## Arkitektur

### API Gateway
- **Teknologi:** .NET med YARP (Yet Another Reverse Proxy)
- **Port:** 5004
- **Funktioner:**
  - Routing til alle microservices
  - JWT token validering
  - Rate limiting
  - CORS konfiguration
  - Security headers
  - Request logging
  - Exception handling
  - Health checks for alle services
  - Swagger dokumentation aggregation

### Microservices

Systemet består af 7 microservices:

1. **AuthService** - Autentificering og autorisering
2. **UserService** - Brugeradministration
3. **BookService** - Bogkatalog
4. **SearchService** - Søgning og opdagelse
5. **WarehouseService** - Lagerstyring
6. **OrderService** - Ordrehåndtering og indkøbskurv
7. **NotificationService** - Notifikationer og emails

---

## AuthService

**Port:** 5006  
**Database:** AuthServiceDb

### Implementerede Features
- Brugerregistrering
- Login med JWT tokens
- Token refresh
- Password hashing (BCrypt)
- Roller: Customer, Seller, Admin
- Database migrations
- RabbitMQ event publishing (UserCreated events)
- Health checks
- Rate limiting
- Audit logging

### Endpoints
- `POST /auth/register` - Registrer ny bruger
- `POST /auth/login` - Login
- `POST /auth/refresh` - Refresh token
- `POST /auth/validate` - Valider token

---

## UserService

**Port:** 5005  
**Database:** UserServiceDb

### Implementerede Features
- Brugerprofiler
- CRUD operationer på brugere
- Søgning efter brugere
- Filtrering efter rolle
- GDPR compliance (data export, anonymisering)
- Rollestatistikker
- Event-driven integration (lytter til UserCreated events)
- Rate limiting
- Audit logging

### Endpoints
- `GET /api/users` - Hent alle brugere (paginering)
- `GET /api/users/{userId}` - Hent bruger
- `GET /api/users/me` - Hent aktuel bruger
- `GET /api/users/search` - Søg brugere
- `GET /api/users/role/{role}` - Hent brugere efter rolle
- `POST /api/users` - Opret bruger
- `PUT /api/users/{userId}` - Opdater bruger
- `DELETE /api/users/{userId}` - Slet bruger (soft delete)
- `PUT /api/users/{userId}/role` - Skift rolle (admin)
- `GET /api/users/{userId}/export` - Eksporter brugerdata (GDPR)
- `POST /api/users/{userId}/anonymize` - Anonymiser bruger (GDPR)
- `GET /api/users/statistics/roles` - Rollestatistikker

---

## BookService

**Port:** 5000  
**Database:** BookDb

### Implementerede Features
- Bogkatalog management
- CRUD operationer på bøger
- Seed data fra CSV
- RabbitMQ event publishing (BookCreated, BookUpdated, BookDeleted)
- Database migrations

### Endpoints
- `GET /api/books` - Hent alle bøger
- `GET /api/books/{isbn}` - Hent bog efter ISBN
- `POST /api/books` - Opret bog
- `PUT /api/books/{isbn}` - Opdater bog
- `DELETE /api/books/{isbn}` - Slet bog

---

## SearchService

**Port:** 5002  
**Database:** Ingen (bruger Redis cache)

### Implementerede Features
- Avanceret bogsøgning
- Fuzzy search
- Facet search (kategorier, priser, etc.)
- Autocomplete
- Søgestatistikker og analytics
- Redis caching for performance
- CQRS pattern (MediatR)
- Clean Architecture
- RabbitMQ event consumption (BookCreated, BookUpdated, BookDeleted, WarehouseStockEvent)
- Rate limiting
- Request size limiting

### Endpoints
- `GET /api/search` - Søg efter bøger
- `GET /api/search/autocomplete` - Autocomplete forslag
- `GET /api/search/facets` - Hent søgefacetter
- `GET /api/search/analytics` - Søgestatistikker
- `GET /api/search/popular` - Populære søgninger
- `GET /api/search/trending` - Trendende søgninger
- `GET /api/search/suggestions` - Søgeforslag
- `GET /api/search/statistics` - Generelle statistikker

---

## WarehouseService

**Port:** 5001  
**Database:** WarehouseServiceDb

### Implementerede Features
- Lagerstyring
- Stock tracking
- Inventory operations
- RabbitMQ event publishing (WarehouseStockEvent)
- Database migrations

### Endpoints
- `GET /api/warehouse` - Hent alle lagerposter
- `GET /api/warehouse/{isbn}` - Hent lager for ISBN
- `GET /api/warehouse/seller/{sellerId}` - Hent sælgers lager
- `POST /api/warehouse` - Opret lagerpost
- `PUT /api/warehouse/{warehouseId}` - Opdater lager
- `DELETE /api/warehouse/{warehouseId}` - Slet lagerpost
- `POST /api/warehouse/{warehouseId}/stock/add` - Tilføj stock
- `POST /api/warehouse/{warehouseId}/stock/remove` - Fjern stock
- `GET /api/warehouse/available` - Hent tilgængelige bøger
- `GET /api/warehouse/low-stock` - Hent lavt lager
- `GET /api/warehouse/statistics` - Lagerstatistikker

---

## OrderService

**Port:** 5003  
**Database:** OrderServiceDb

### Implementerede Features
- Indkøbskurv management
- Ordrehåndtering
- Betalingsbehandling (Mock og Stripe support)
- Ordre lifecycle (Pending → Paid → Shipped → Delivered)
- Ordre annullering og refundering
- Inventory integration
- Clean Architecture
- RabbitMQ event publishing (OrderCreated, OrderPaid, OrderShipped, OrderDelivered, OrderCancelled, OrderRefunded)
- Rate limiting
- Audit logging

### Endpoints - Indkøbskurv
- `GET /api/shoppingcart/{customerId}` - Hent kurv
- `POST /api/shoppingcart/{customerId}/items` - Tilføj til kurv
- `PUT /api/shoppingcart/{customerId}/items/{cartItemId}` - Opdater mængde
- `DELETE /api/shoppingcart/{customerId}/items/{cartItemId}` - Fjern fra kurv
- `DELETE /api/shoppingcart/{customerId}` - Tøm kurv
- `POST /api/shoppingcart/{customerId}/checkout` - Checkout (konverter til ordre)

### Endpoints - Ordre
- `POST /api/orders` - Opret ordre
- `GET /api/orders/{orderId}` - Hent ordre
- `GET /api/orders/customer/{customerId}` - Hent kundes ordrer
- `GET /api/orders` - Hent alle ordrer (admin)
- `GET /api/orders/status/{status}` - Hent ordrer efter status
- `POST /api/orders/{orderId}/pay` - Betal ordre
- `POST /api/orders/{orderId}/ship` - Marker som sendt
- `POST /api/orders/{orderId}/deliver` - Marker som leveret
- `POST /api/orders/{orderId}/cancel` - Annuller ordre
- `POST /api/orders/{orderId}/refund` - Refunder ordre

---

## NotificationService

**Port:** 5007  
**Database:** NotificationServiceDb

### Implementerede Features
- Notifikationshåndtering
- Email integration (SendGrid og Mock)
- Brugerpræferencer for notifikationer
- Email templates
- Retry mechanism for failed notifications
- RabbitMQ event consumption (OrderCreated, OrderPaid, OrderShipped, OrderDelivered, OrderCancelled, OrderRefunded, UserCreated)
- Clean Architecture
- GDPR compliance

### Endpoints - Notifikationer
- `POST /api/notifications` - Opret notifikation
- `GET /api/notifications/{notificationId}` - Hent notifikation
- `GET /api/notifications/user/{userId}` - Hent brugerens notifikationer
- `GET /api/notifications/user/{userId}/unread-count` - Hent ulæste antal
- `POST /api/notifications/{notificationId}/mark-read` - Marker som læst
- `POST /api/notifications/{notificationId}/send` - Send notifikation
- `POST /api/notifications/{notificationId}/retry` - Prøv igen ved fejl

### Endpoints - Præferencer
- `GET /api/notifications/preferences/{userId}` - Hent præferencer
- `PUT /api/notifications/preferences/{userId}` - Opdater præferencer
- `POST /api/notifications/preferences/{userId}/disable-all` - Deaktiver alle
- `POST /api/notifications/preferences/{userId}/enable-all` - Aktiver alle

---

## Infrastructure

### Databases
- **SQL Server 2022** - Alle services bruger separate databaser
- **Databaser:**
  - AuthServiceDb
  - UserServiceDb
  - BookDb
  - WarehouseServiceDb
  - OrderServiceDb
  - NotificationServiceDb
  - (SearchService bruger ikke database, kun Redis)

### Message Queue
- **RabbitMQ** - Event-driven kommunikation mellem services
- **Port:** 5672 (AMQP), 15672 (Management UI)
- **Events:**
  - UserCreated
  - BookCreated, BookUpdated, BookDeleted
  - WarehouseStockEvent
  - OrderCreated, OrderPaid, OrderShipped, OrderDelivered, OrderCancelled, OrderRefunded

### Cache
- **Redis** - Brugt af SearchService til caching
- **Port:** 6379

### Docker
- Alle services containeriseret
- Docker Compose konfiguration
- Health checks for alle services
- Volume mounting for data persistence

---

## Sikkerhed

### Implementerede Sikkerhedsfeatures
- **JWT Authentication** - Token-baseret autentificering
- **Rate Limiting** - Beskyttelse mod misbrug
- **CORS** - Cross-Origin Resource Sharing konfiguration
- **Security Headers** - XSS, CSRF beskyttelse
- **Input Validation** - DataAnnotations og domain validation
- **Exception Handling** - Global exception handling middleware
- **Audit Logging** - Logging af alle kritiske operationer
- **Password Hashing** - BCrypt hashing
- **Role-based Authorization** - Roller: Customer, Seller, Admin

### Public vs Protected Endpoints
- **Public:** Auth endpoints, GET på bøger, søgning, health checks
- **Protected:** Alle andre endpoints kræver JWT token

---

## API Gateway Routing

Alle requests går gennem API Gateway på port 5004:

| Service | Gateway Route | Destination |
|---------|--------------|-------------|
| AuthService | `/auth/*` | http://authservice:8080 |
| BookService | `/books/*` | http://bookservice:8080/api/books |
| WarehouseService | `/warehouse/*` | http://warehouseservice:8080/api/warehouse |
| SearchService | `/search/*` | http://searchservice:8080/api/search |
| OrderService | `/orders/*` | http://orderservice:8080/api/orders |
| ShoppingCartService | `/cart/*` | http://orderservice:8080/api/shoppingcart |
| UserService | `/users/*` | http://userservice:8080/api/users |
| NotificationService | `/notifications/*` | http://notificationservice:8080/api/notifications |

---

## Total Endpoints

**81+ endpoints** implementeret:

- **Authentication:** 4 endpoints
- **User Management:** 12 endpoints
- **Search & Discovery:** 10 endpoints
- **Book Management:** 6 endpoints
- **Warehouse Management:** 11 endpoints
- **Shopping Cart:** 6 endpoints
- **Order Management:** 10 endpoints
- **Notifications:** 11 endpoints
- **Notification Preferences:** 4 endpoints

---

## Arkitektur Patterns

### Implementerede Patterns
- **Microservices Architecture** - Separate services med egne databaser
- **API Gateway Pattern** - Central entry point
- **Event-Driven Architecture** - RabbitMQ for service kommunikation
- **Clean Architecture** - OrderService, NotificationService, SearchService
- **CQRS** - SearchService bruger MediatR
- **Repository Pattern** - Data access abstraction
- **Dependency Injection** - IoC container
- **Domain-Driven Design** - Rich domain models

---

## Testing

### Test Projekter
- Unit tests for alle services
- Integration tests
- API tests
- Test scripts (PowerShell) til endpoint testing

---

## Deployment

### Docker Compose
Alle services kan startes med:
```bash
docker-compose up --build
```

### Services og Ports
- API Gateway: http://localhost:5004
- BookService: http://localhost:5000
- WarehouseService: http://localhost:5001
- SearchService: http://localhost:5002
- OrderService: http://localhost:5003
- UserService: http://localhost:5005
- AuthService: http://localhost:5006
- NotificationService: http://localhost:5007
- RabbitMQ Management: http://localhost:15672
- SQL Server: localhost:1433
- Redis: localhost:6379

---

## Status

✅ **Alle services er implementeret og funktionelle**

- [x] API Gateway med routing og sikkerhed
- [x] AuthService med JWT authentication
- [x] UserService med GDPR compliance
- [x] BookService med katalog management
- [x] SearchService med avanceret søgning
- [x] WarehouseService med lagerstyring
- [x] OrderService med indkøbskurv og ordrehåndtering
- [x] NotificationService med email integration
- [x] Event-driven kommunikation
- [x] Docker containerisering
- [x] Health checks
- [x] Rate limiting
- [x] Audit logging
- [x] Database migrations

---

**Dokument Version:** 1.0  
**Oprettet:** 2025-01-21  
**Projekt:** Georgia Tech Library Marketplace


