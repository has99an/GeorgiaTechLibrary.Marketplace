# Database Setup og Data Seeding Guide

## Oversigt

Dette dokument beskriver processen for at genoprette alle databases og seed data efter container rens.

## Services og deres databases

1. **AuthService** → `AuthServiceDb`
2. **UserService** → `UserServiceDb`
3. **BookService** → `BookDb`
4. **WarehouseService** → `WarehouseServiceDb`
5. **OrderService** → `OrderServiceDb`
6. **NotificationService** → `NotificationServiceDb`
7. **SearchService** → Ingen database (bruger Redis)

## Migrations rækkefølge

Services kører automatisk migrations når de starter. Rækkefølgen er vigtig pga. dependencies:

1. **AuthService** (først - skaber brugere)
2. **UserService** (bruger AuthService data via events)
3. **BookService** (uafhængig)
4. **WarehouseService** (bruger BookService data)
5. **OrderService** (bruger UserService og BookService)
6. **NotificationService** (uafhængig)

## Seed Data Oversigt

### AuthService
- **Kilde**: CSV fil (`AuthService/Data/users.csv`)
- **Tabeller seeded**: `AuthUsers`
- **Metode**: `SeedData.InitializeAsync()` i `Program.cs`
- **Default password**: `Password123!` (skal reset ved første login)

### UserService
- **Kilde**: CSV fil (`UserService/Data/users.csv`)
- **Tabeller seeded**: `Users`, `SellerProfiles` (automatisk for Seller role)
- **Metode**: `SeedData.InitializeAsync()` i `Program.cs`
- **Antal**: 500 users

### BookService
- **Kilde**: CSV fil (`BookService/Data/Books_Small.csv`)
- **Tabeller seeded**: `Books`
- **Metode**: `SeedData.Initialize()` i `Program.cs`

### WarehouseService
- **Kilde**: CSV fil (`WarehouseService/Data/WarehouseItems_Small.csv`)
- **Tabeller seeded**: `WarehouseItems`
- **Metode**: `SeedData.Initialize()` i `Program.cs`

### OrderService
- **Seed data**: Ingen (tom database efter migrations)
- **Tabeller**: `Orders`, `OrderItems`, `ShoppingCarts`, `CartItems`

### NotificationService
- **Seed data**: Ingen (tom database efter migrations)
- **Tabeller**: `Notifications`

## Sync Endpoints

### SearchService StartupSyncService
- **Automatisk**: Kører ved startup og checker Redis state
- **Manuel sync endpoints** (hvis nødvendigt):
  - `POST /api/books/sync-events` (BookService)
  - `POST /api/warehouse/sync-events` (WarehouseService)

## Start Process

### 1. Start alle services
```bash
docker-compose up --build -d
```

### 2. Overvåg migrations
```bash
# Tjek AuthService migrations
docker-compose logs -f authservice

# Tjek UserService migrations
docker-compose logs -f userservice

# Tjek BookService migrations
docker-compose logs -f bookservice

# Tjek WarehouseService migrations
docker-compose logs -f warehouseservice

# Tjek OrderService migrations
docker-compose logs -f orderservice

# Tjek NotificationService migrations
docker-compose logs -f notificationservice
```

### 3. Verificer seed data

#### AuthService
```bash
# Tjek antal auth users
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d AuthServiceDb -Q "SELECT COUNT(*) as AuthUsers FROM AuthUsers"
```

#### UserService
```bash
# Tjek antal users
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d UserServiceDb -Q "SELECT COUNT(*) as Users FROM Users; SELECT COUNT(*) as SellerProfiles FROM SellerProfiles"
```

#### BookService
```bash
# Tjek antal books
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d BookDb -Q "SELECT COUNT(*) as Books FROM Books"
```

#### WarehouseService
```bash
# Tjek antal warehouse items
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d WarehouseServiceDb -Q "SELECT COUNT(*) as WarehouseItems FROM WarehouseItems"
```

## Verificering af komplet setup

### 1. Tjek alle services er kørende
```bash
docker-compose ps
```

### 2. Verificer seed data i databaserne

#### AuthServiceDb
```bash
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d AuthServiceDb -Q "SELECT COUNT(*) as AuthUsers FROM AuthUsers"
```
**Forventet resultat**: 500 auth users

#### UserServiceDb
```bash
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d UserServiceDb -Q "SELECT COUNT(*) as Users FROM Users; SELECT COUNT(*) as SellerProfiles FROM SellerProfiles"
```
**Forventet resultat**: 500 users, ~100 seller profiles (automatisk oprettet for Seller role)

#### BookDb
```bash
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d BookDb -Q "SELECT COUNT(*) as Books FROM Books"
```
**Forventet resultat**: 1000 books

#### WarehouseServiceDb
```bash
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d WarehouseServiceDb -Q "SELECT COUNT(*) as WarehouseItems FROM WarehouseItems"
```
**Forventet resultat**: 1000 warehouse items

#### OrderServiceDb
```bash
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d OrderServiceDb -Q "SELECT COUNT(*) as Orders FROM Orders"
```
**Forventet resultat**: 0 orders (tom database efter migrations)

#### NotificationServiceDb
```bash
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d NotificationServiceDb -Q "SELECT COUNT(*) as Notifications FROM Notifications"
```
**Forventet resultat**: 0 notifications (tom database efter migrations)

### 3. Tjek health endpoints
```bash
# AuthService
curl http://localhost:5006/health

# UserService
curl http://localhost:5005/health

# BookService
curl http://localhost:5000/health

# WarehouseService
curl http://localhost:5001/health

# OrderService
curl http://localhost:5003/health

# NotificationService
curl http://localhost:5007/health

# SearchService
curl http://localhost:5002/health

# ApiGateway
curl http://localhost:5004/health
```

### 4. Tjek Swagger dokumentation
- AuthService: http://localhost:5006/swagger
- UserService: http://localhost:5005/swagger
- BookService: http://localhost:5000/swagger
- WarehouseService: http://localhost:5001/swagger
- OrderService: http://localhost:5003/swagger
- NotificationService: http://localhost:5007/swagger
- SearchService: http://localhost:5002/swagger
- ApiGateway: http://localhost:5004/swagger

## Troubleshooting

### Problem: Database ikke fundet
**Løsning**: Opret database manuelt:
```bash
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -Q "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DatabaseName') CREATE DATABASE DatabaseName"
```

### Problem: Migrations fejler
**Løsning**: 
1. Tjek SQL Server er healthy: `docker-compose ps sqlserver`
2. Vent på SQL Server er klar (30 sekunder)
3. Restart service: `docker-compose restart servicename`

### Problem: Seed data ikke loaded
**Løsning**:
1. Tjek CSV filer eksisterer i service Data mappe
2. Tjek logs for seed errors: `docker-compose logs servicename`
3. Seed data kører kun hvis tabeller er tomme (idempotent)

## Verificerede Seed Data Resultater

Efter genoprettelse af alle databases og kørsel af migrations:

| Service | Database | Tabel | Antal Rækker | Status |
|---------|----------|-------|--------------|--------|
| AuthService | AuthServiceDb | AuthUsers | 500 | ✅ Seeded |
| UserService | UserServiceDb | Users | 500 | ✅ Seeded |
| UserService | UserServiceDb | SellerProfiles | 100 | ✅ Seeded (auto) |
| BookService | BookDb | Books | 1000 | ✅ Seeded |
| WarehouseService | WarehouseServiceDb | WarehouseItems | 1000 | ✅ Seeded |
| OrderService | OrderServiceDb | Orders | 0 | ✅ Tom (forventet) |
| NotificationService | NotificationServiceDb | Notifications | 0 | ✅ Tom (forventet) |

## Noter

- Alle migrations kører automatisk ved service startup
- Seed data er idempotent - kører kun hvis tabeller er tomme
- Services venter på SQL Server health check før migrations
- Event-driven sync mellem services (BookAddedForSale, UserCreated, etc.)
- UserService opretter automatisk SellerProfiles for brugere med Seller role
- SearchService bruger Redis (ingen SQL database) og syncer automatisk via events

