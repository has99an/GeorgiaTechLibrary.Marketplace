# Hvordan Starter Jeg Serveren?

## Hurtig Start

### 1. Start alle services
```bash
docker-compose up -d
```

### 2. Vent på at services starter (30-60 sekunder)
```bash
# Tjek status
docker-compose ps
```

### 3. Verificer at alle services kører
Alle services skal vise status "Up" i `docker-compose ps`

## Detaljeret Start Process

### Trin 1: Start Infrastructure (SQL Server, RabbitMQ, Redis)
```bash
docker-compose up -d sqlserver rabbitmq redis
```

Vent 30 sekunder for SQL Server health check.

### Trin 2: Start alle Application Services
```bash
docker-compose up -d
```

### Trin 3: Overvåg migrations
```bash
# Se alle logs
docker-compose logs -f

# Se specifik service
docker-compose logs -f userservice
docker-compose logs -f authservice
docker-compose logs -f bookservice
```

### Trin 4: Verificer at alt kører
```bash
# Tjek alle services status
docker-compose ps

# Tjek health endpoints
curl http://localhost:5004/health  # ApiGateway
curl http://localhost:5006/health  # AuthService
curl http://localhost:5005/health # UserService
curl http://localhost:5000/health  # BookService
curl http://localhost:5001/health  # WarehouseService
curl http://localhost:5003/health  # OrderService
curl http://localhost:5007/health  # NotificationService
curl http://localhost:5002/health  # SearchService
```

## Services og Ports

| Service | Port | Swagger URL | Health URL |
|---------|------|-------------|------------|
| ApiGateway | 5004 | http://localhost:5004/swagger | http://localhost:5004/health |
| AuthService | 5006 | http://localhost:5006/swagger | http://localhost:5006/health |
| UserService | 5005 | http://localhost:5005/swagger | http://localhost:5005/health |
| BookService | 5000 | http://localhost:5000/swagger | http://localhost:5000/health |
| WarehouseService | 5001 | http://localhost:5001/swagger | http://localhost:5001/health |
| OrderService | 5003 | http://localhost:5003/swagger | http://localhost:5003/health |
| NotificationService | 5007 | http://localhost:5007/swagger | http://localhost:5007/health |
| SearchService | 5002 | http://localhost:5002/swagger | http://localhost:5002/health |

## Hvad Sker Der Ved Start?

### Automatisk Process
1. **SQL Server** starter først (health check tager ~30 sekunder)
2. **RabbitMQ** og **Redis** starter
3. **Application services** starter i parallel:
   - Hver service venter på SQL Server health check
   - Hver service kører automatisk migrations
   - Hver service kører automatisk seed data (hvis tabeller er tomme)
   - Hver service starter event consumers (RabbitMQ)

### Migrations
- Alle migrations kører automatisk ved service startup
- Services har retry logic (10-30 forsøg)
- Migrations kører i korrekt rækkefølge baseret på dependencies

### Seed Data
- Seed data kører automatisk efter migrations
- Seed data er idempotent (kører kun hvis tabeller er tomme)
- CSV filer læses fra service Data mappe

## Stop Serveren

### Stop alle services (bevar data)
```bash
docker-compose down
```

### Stop og slet alle data
```bash
docker-compose down -v
```

## Troubleshooting

### Problem: Service starter ikke
**Løsning**:
```bash
# Tjek logs
docker-compose logs servicename

# Restart service
docker-compose restart servicename

# Tjek SQL Server health
docker-compose ps sqlserver
```

### Problem: Database ikke fundet
**Løsning**: Opret database manuelt:
```bash
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -Q "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'DatabaseName') CREATE DATABASE DatabaseName"
```

### Problem: Migrations fejler
**Løsning**:
1. Vent på SQL Server er healthy (30 sekunder)
2. Restart service: `docker-compose restart servicename`
3. Tjek logs: `docker-compose logs servicename`

### Problem: Seed data ikke loaded
**Løsning**:
1. Tjek CSV filer eksisterer i service Data mappe
2. Tjek logs: `docker-compose logs servicename | Select-String -Pattern "Seed"`
3. Seed data kører kun hvis tabeller er tomme

## Verificer Data

### Tjek seed data i databaserne
```bash
# AuthService - 500 auth users
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d AuthServiceDb -Q "SELECT COUNT(*) as AuthUsers FROM AuthUsers"

# UserService - 500 users, 100 seller profiles
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d UserServiceDb -Q "SELECT COUNT(*) as Users FROM Users; SELECT COUNT(*) as SellerProfiles FROM SellerProfiles"

# BookService - 1000 books
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d BookDb -Q "SELECT COUNT(*) as Books FROM Books"

# WarehouseService - 1000 warehouse items
docker exec -it georgiatechlibrarymarketplace-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d WarehouseServiceDb -Q "SELECT COUNT(*) as WarehouseItems FROM WarehouseItems"
```

## Genopret Alt Data (Efter Container Rens)

Hvis du har renset containere og volumes:

1. Start infrastructure: `docker-compose up -d sqlserver rabbitmq redis`
2. Vent 30 sekunder
3. Start alle services: `docker-compose up -d`
4. Vent 60-90 sekunder for migrations og seed data
5. Verificer data (se ovenfor)

Se `DATABASE-SETUP-SUMMARY.md` for detaljeret dokumentation af genoprettelsesprocessen.





