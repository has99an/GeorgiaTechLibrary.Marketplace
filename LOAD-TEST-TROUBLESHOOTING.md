# Load Test Troubleshooting Guide

## Problem: 40% Fejlrate i K6 Load Test

Dette dokument beskriver løsninger til strukturelle problemer i load testing.

## Identificerede Problemer og Løsninger

### 1. ✅ CORS Problem - Løst

**Problem**: SearchService CORS blokerede requests uden Origin header (K6 sender ikke Origin).

**Løsning**: Opdateret `SearchService/Program.cs` til at tillade requests uden origin i development mode:

```csharp
if (isDevelopment)
{
    policy.SetIsOriginAllowed(origin =>
    {
        // Allow requests without origin (null/empty) in development
        if (string.IsNullOrEmpty(origin))
            return true;
        // ... rest of logic
    });
}
```

**Status**: ✅ Implementeret

### 2. ✅ Manglende Headers - Løst

**Problem**: K6-testen sendte ikke Content-Type og Accept headers.

**Løsning**: Tilføjet headers til alle HTTP requests i `load-test/search-load-test.js`:

```javascript
headers: {
  'Content-Type': 'application/json',
  'Accept': 'application/json',
}
```

**Status**: ✅ Implementeret

### 3. ✅ HTTPS Redirection Problem - Løst

**Problem**: API Gateway redirected HTTP til HTTPS i development, hvilket forårsagede problemer i Docker.

**Løsning**: Deaktiveret HTTPS redirection i development mode i `ApiGateway/Program.cs`:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
```

**Status**: ✅ Implementeret

### 4. ✅ Direkte Test Mod SearchService - Tilføjet

**Problem**: Svært at isolere om problemet er i API Gateway eller SearchService.

**Løsning**: Oprettet `load-test/search-load-test-direct.js` der tester direkte mod SearchService (port 5002).

**Brug**:
```bash
cd load-test
k6 run search-load-test-direct.js
```

**Status**: ✅ Implementeret

### 5. ✅ DEBUG Logging - Tilføjet

**Problem**: Svært at se om requests når frem til SearchService.

**Løsning**: Opdateret `SearchService/appsettings.Development.json` med DEBUG logging:

```json
{
  "Logging": {
    "LogLevel": {
      "SearchService": "Debug",
      "Microsoft.AspNetCore.Routing": "Information"
    }
  }
}
```

**Status**: ✅ Implementeret

## Test Strategi

### Trin 1: Test Direkte Mod SearchService

Først, test direkte mod SearchService for at isolere problemet:

```bash
cd load-test
k6 run search-load-test-direct.js
```

**Forventet Resultat**: Hvis dette virker, er problemet i API Gateway routing eller konfiguration.

**Hvis det virker**: Gå til Trin 2.
**Hvis det ikke virker**: Tjek SearchService logs og Redis connectivity.

### Trin 2: Test Gennem API Gateway

Hvis direkte test virker, test gennem API Gateway:

```bash
cd load-test
k6 run search-load-test.js
```

**Forventet Resultat**: Hvis dette fejler, er problemet i API Gateway.

**Hvis det fejler**: Tjek:
- API Gateway routing konfiguration (`ApiGateway/appsettings.json`)
- API Gateway logs
- YARP reverse proxy konfiguration

### Trin 3: Verificer Routing

Verificer at API Gateway router korrekt:

```bash
# Test direkte mod SearchService
curl "http://localhost:5002/api/search?query=test"

# Test gennem API Gateway
curl "http://localhost:5004/search?query=test"
```

Begge skal returnere samme resultat.

## Verificering af Konfiguration

### 1. API Gateway Routing

Tjek `ApiGateway/appsettings.json`:

```json
{
  "ReverseProxy": {
    "Routes": {
      "search-route": {
        "ClusterId": "search-cluster",
        "Match": {
          "Path": "/search/{**catch-all}"
        },
        "Transforms": [
          { "PathRemovePrefix": "/search" },
          { "PathPrefix": "/api/search" }
        ]
      }
    },
    "Clusters": {
      "search-cluster": {
        "Destinations": {
          "search-destination": {
            "Address": "http://searchservice:8080"
          }
        }
      }
    }
  }
}
```

**Verificer**:
- Route matcher `/search/*`
- Transform fjerner `/search` og tilføjer `/api/search`
- Cluster destination er `http://searchservice:8080` (Docker service navn)

### 2. SearchService CORS

Tjek `SearchService/Program.cs` - CORS skal tillade requests uden origin i development.

### 3. SearchService Rate Limiting

Tjek `SearchService/API/Middleware/RateLimitingMiddleware.cs`:

- MaxRequestsPerMinute: 3000 (tilstrækkeligt for 1500 req/min target)
- MaxRequestsPerHour: 200000

### 4. API Gateway Rate Limiting

Tjek `ApiGateway/appsettings.json`:

```json
{
  "Security": {
    "RateLimit": {
      "EndpointLimits": {
        "/search": {
          "Limit": 2000,
          "PeriodInSeconds": 60
        }
      }
    }
  }
}
```

**Note**: API Gateway limit er 2000/min, hvilket er højere end target (1500/min).

## Debugging Steps

### 1. Tjek Service Status

```bash
# Docker services
docker-compose ps

# Health checks
curl http://localhost:5004/health  # API Gateway
curl http://localhost:5002/health  # SearchService
```

### 2. Tjek Logs

```bash
# API Gateway logs
docker-compose logs apigateway

# SearchService logs
docker-compose logs searchservice
```

### 3. Test Endpoints Manuelt

```bash
# Test SearchService direkte
curl "http://localhost:5002/api/search?query=test"

# Test gennem API Gateway
curl "http://localhost:5004/search?query=test"

# Test med headers
curl -H "Content-Type: application/json" -H "Accept: application/json" "http://localhost:5004/search?query=test"
```

### 4. Tjek Redis Connectivity

SearchService bruger Redis til rate limiting og caching:

```bash
# Tjek Redis
docker-compose exec redis redis-cli ping

# Tjek SearchService Redis connection
curl http://localhost:5002/api/search/debug
```

## Forventede Resultater

### Succesfuld Test

- ✅ Error rate < 1%
- ✅ P95 response time < 200ms
- ✅ P99 response time < 200ms
- ✅ Throughput >= 25 req/sec (1500+ req/min)

### Fejl Indikatorer

- ❌ Error rate > 1% → Tjek CORS, routing, eller service availability
- ❌ Response time > 200ms → Tjek Redis, database, eller service performance
- ❌ Throughput < 25 req/sec → Tjek rate limiting eller system resources

## Yderligere Ressourcer

- [K6 Documentation](https://k6.io/docs/)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [ASP.NET Core CORS](https://docs.microsoft.com/en-us/aspnet/core/security/cors)

## Noter

- Alle ændringer er implementeret og klar til test
- DEBUG logging er aktiveret i development mode
- CORS tillader nu requests uden origin i development
- HTTPS redirection er deaktiveret i development/Docker
- Direkte test mod SearchService er tilgængelig for isoleret debugging

