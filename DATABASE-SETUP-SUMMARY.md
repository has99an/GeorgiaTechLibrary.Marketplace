# Database Setup Summary - Genoprettelse Efter Container Rens

## Dato: 2025-12-10

## Udført Process

### 1. Container Rens
- Alle containere og volumes blev renset med `docker-compose down -v`
- Alle databases blev slettet

### 2. Infrastructure Services Start
- SQL Server startet først
- RabbitMQ startet
- Redis startet
- Ventetid: ~30 sekunder for SQL Server health check

### 3. Application Services Start
Alle services startede automatisk og kørte migrations i korrekt rækkefølge:

#### Rækkefølge
1. **AuthService** (først - skaber brugere)
2. **UserService** (bruger AuthService data via events)
3. **BookService** (uafhængig)
4. **WarehouseService** (bruger BookService data)
5. **OrderService** (bruger UserService og BookService)
6. **NotificationService** (uafhængig)
7. **SearchService** (ingen database, bruger Redis)
8. **ApiGateway** (routing service)

## Migrations Status

### ✅ AuthService
- **Database**: AuthServiceDb
- **Migrations**: Kørt automatisk
- **Status**: SUCCESS
- **Applied Migrations**: 
  - 20251119050936_InitialCreate
  - 20251209212724_AddRoleToAuthUser

### ✅ UserService
- **Database**: UserServiceDb
- **Migrations**: Kørt automatisk (efter manuel database oprettelse)
- **Status**: SUCCESS
- **Applied Migrations**:
  - 20251119050923_InitialCreate
  - 20251203240000_AddDeliveryAddressToUser
  - 20251209010639_AddSellerProfileAndBookListing
  - 20251209204247_AddSellerReviews

### ✅ BookService
- **Database**: BookDb
- **Migrations**: Kørt automatisk
- **Status**: SUCCESS
- **Applied Migrations**:
  - 20251119050851_InitialCreate

### ✅ WarehouseService
- **Database**: WarehouseServiceDb
- **Migrations**: Kørt automatisk
- **Status**: SUCCESS
- **Applied Migrations**:
  - 20251119050909_InitialCreate

### ✅ OrderService
- **Database**: OrderServiceDb
- **Migrations**: Kørt automatisk
- **Status**: SUCCESS
- **Applied Migrations**:
  - 20251119051033_InitialCreate
- **Manual SQL**: Delivery address columns ensured

### ✅ NotificationService
- **Database**: NotificationServiceDb
- **Migrations**: Kørt automatisk
- **Status**: SUCCESS
- **Applied Migrations**:
  - 20251119051007_InitialCreate

## Seed Data Status

### ✅ AuthService
- **Kilde**: `AuthService/Data/users.csv`
- **Metode**: `SeedData.InitializeAsync()` i `Program.cs`
- **Resultat**: 500 auth users seeded
- **Default Password**: `Password123!` (skal reset ved første login)
- **Tabeller**: `AuthUsers`

### ✅ UserService
- **Kilde**: `UserService/Data/users.csv`
- **Metode**: `SeedData.InitializeAsync()` i `Program.cs`
- **Resultat**: 
  - 500 users seeded
  - 100 seller profiles oprettet automatisk (for Seller role)
- **Tabeller**: `Users`, `SellerProfiles`

### ✅ BookService
- **Kilde**: `BookService/Data/Books_Small.csv`
- **Metode**: `SeedData.Initialize()` i `Program.cs`
- **Resultat**: 1000 books seeded
- **Tabeller**: `Books`

### ✅ WarehouseService
- **Kilde**: `WarehouseService/Data/WarehouseItems_Small.csv`
- **Metode**: `SeedData.Initialize()` i `Program.cs`
- **Resultat**: 1000 warehouse items seeded
- **Tabeller**: `WarehouseItems`

### ✅ OrderService
- **Seed Data**: Ingen
- **Status**: Tom database (forventet)
- **Tabeller**: `Orders`, `OrderItems`, `ShoppingCarts`, `CartItems`

### ✅ NotificationService
- **Seed Data**: Ingen
- **Status**: Tom database (forventet)
- **Tabeller**: `Notifications`

## Event-Driven Sync

### Automatisk Sync via Events
- **BookAddedForSale** → WarehouseService opretter/opdaterer WarehouseItems
- **BookStockUpdated** → SearchService opdaterer Redis cache
- **UserCreated** → UserService opretter User records
- **OrderPaid** → WarehouseService reducerer stock, UserService opdaterer listing quantities

### SearchService StartupSyncService
- Kører automatisk ved startup
- Tjekker Redis state
- Rebuilder sorted sets hvis tomme
- Kan trigger manuel sync hvis nødvendigt (via config)

## Verificerede Data

| Service | Database | Tabel | Antal Rækker | Verificeret |
|---------|----------|-------|--------------|-------------|
| AuthService | AuthServiceDb | AuthUsers | 500 | ✅ |
| UserService | UserServiceDb | Users | 500 | ✅ |
| UserService | UserServiceDb | SellerProfiles | 100 | ✅ |
| BookService | BookDb | Books | 1000 | ✅ |
| WarehouseService | WarehouseServiceDb | WarehouseItems | 1000 | ✅ |
| OrderService | OrderServiceDb | Orders | 0 | ✅ |
| NotificationService | NotificationServiceDb | Notifications | 0 | ✅ |

## Database Tabeller

### AuthServiceDb
- `AuthUsers` (500 rækker)
- `__EFMigrationsHistory`

### UserServiceDb
- `Users` (500 rækker)
- `SellerProfiles` (100 rækker)
- `SellerBookListings` (0 rækker - oprettes når sælgere tilføjer bøger)
- `SellerReviews` (0 rækker - oprettes når kunder anmelder)
- `__EFMigrationsHistory`

### BookDb
- `Books` (1000 rækker)
- `__EFMigrationsHistory`

### WarehouseServiceDb
- `WarehouseItems` (1000 rækker)
- `__EFMigrationsHistory`

### OrderServiceDb
- `Orders` (0 rækker)
- `OrderItems` (0 rækker)
- `ShoppingCarts` (0 rækker)
- `CartItems` (0 rækker)
- `__EFMigrationsHistory`

### NotificationServiceDb
- `Notifications` (0 rækker)
- `EmailTemplates` (0 rækker)
- `NotificationPreferences` (0 rækker)
- `__EFMigrationsHistory`

## Services Status

Alle services er kørende og klar:

- ✅ AuthService (port 5006)
- ✅ UserService (port 5005)
- ✅ BookService (port 5000)
- ✅ WarehouseService (port 5001)
- ✅ OrderService (port 5003)
- ✅ NotificationService (port 5007)
- ✅ SearchService (port 5002)
- ✅ ApiGateway (port 5004)

## Endpoints Brugt

Ingen manuelle sync/initialize endpoints blev brugt - alt kører automatisk via:
- Migrations ved service startup
- Seed data ved service startup
- Event-driven sync mellem services

## Problemer Løst

1. **UserServiceDb manglende**: Oprettet manuelt, derefter restart af UserService
2. **SQL Server health check**: Ventetid tilføjet før service start

## Næste Skridt

Systemet er nu klar til brug:
1. Alle databases er oprettet og migreret
2. Alle seed data er loaded
3. Alle services kører og lytter til events
4. Event-driven kommunikation er sat op

## Start Serveren

For at starte serveren fremover:

```bash
# Start alle services
docker-compose up -d

# Tjek status
docker-compose ps

# Tjek logs for en specifik service
docker-compose logs -f servicename

# Stop alle services
docker-compose down

# Stop og slet data
docker-compose down -v
```

