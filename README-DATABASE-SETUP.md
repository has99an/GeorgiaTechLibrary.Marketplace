# Database Setup - Komplet Guide

## Oversigt

Dette dokument beskriver hvordan alle databases og data blev genoprettet efter container rens.

## Status: ✅ Komplet

Alle databases er oprettet, migreret og seeded med test data.

## Dokumentation

1. **DATABASE-SETUP-GUIDE.md** - Detaljeret guide til database setup og seed data
2. **DATABASE-SETUP-SUMMARY.md** - Komplet oversigt over migrations og seed data resultater
3. **HVORDAN-STARTER-JEG-SERVEREN.md** - Hurtig guide til at starte serveren

## Hurtig Reference

### Services og Databases
- **AuthService** → `AuthServiceDb` (500 auth users)
- **UserService** → `UserServiceDb` (500 users, 100 seller profiles)
- **BookService** → `BookDb` (1000 books)
- **WarehouseService** → `WarehouseServiceDb` (1000 warehouse items)
- **OrderService** → `OrderServiceDb` (tom - klar til brug)
- **NotificationService** → `NotificationServiceDb` (tom - klar til brug)
- **SearchService** → Ingen database (bruger Redis)

### Start Serveren
```bash
docker-compose up -d
```

### Verificer Data
```bash
# Se alle services status
docker-compose ps

# Tjek health endpoints
curl http://localhost:5004/health
```

### Stop Serveren
```bash
# Stop (bevar data)
docker-compose down

# Stop og slet data
docker-compose down -v
```

## Verificerede Resultater

✅ Alle 6 databases oprettet og migreret
✅ 500 auth users seeded
✅ 500 users seeded
✅ 100 seller profiles oprettet automatisk
✅ 1000 books seeded
✅ 1000 warehouse items seeded
✅ Alle services kører og er klar til brug

## Næste Skridt

Systemet er nu klar til brug. Du kan:
1. Bruge Swagger endpoints til at teste API'erne
2. Oprette ordrer via OrderService
3. Tilføje bøger til salg via UserService
4. Søge efter bøger via SearchService

Se `HVORDAN-STARTER-JEG-SERVEREN.md` for detaljeret instruktioner.





