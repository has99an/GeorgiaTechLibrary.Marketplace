# API Gateway - Arkitektur Analyse og Forbedringsplan

**Dato:** 19. November 2025  
**Version:** 1.0  
**Status:** Analyse og Anbefalinger

---

## 📋 Executive Summary

API Gateway'en fungerer grundlæggende, men mangler flere kritiske funktioner for en produktionsklar løsning. Analysen identificerer **15 kritiske problemer** og foreslår en omfattende moderniseringsplan.

**Nuværende Status:** ⚠️ Fungerende men ikke produktionsklar  
**Sikkerhedsniveau:** 🔴 Utilstrækkeligt  
**Anbefalinger:** 🔧 Omfattende refaktorering nødvendig

---

## 🔍 Nuværende Arkitektur

### Teknologi Stack
- **Framework:** ASP.NET Core 9.0
- **Reverse Proxy:** YARP (Yet Another Reverse Proxy) 2.2.0
- **Authentication:** Custom JWT Middleware
- **Health Checks:** AspNetCore.HealthChecks.Uris

### Routing Konfiguration
Gateway'en router til 6 services:
1. **AuthService** - `/auth/*` → `http://authservice:8080`
2. **BookService** - `/books/*` → `http://bookservice:8080/api/books`
3. **WarehouseService** - `/warehouse/*` → `http://warehouseservice:8080/api/warehouse`
4. **SearchService** - `/search/*` → `http://searchservice:8080/api/search`
5. **OrderService** - `/orders/*` → `http://orderservice:8080/api/orders`
6. **UserService** - `/users/*` → `http://userservice:8080/api/users`

**Note:** NotificationService har ingen HTTP endpoints og kræver ikke routing.

---

## ❌ Identificerede Problemer

### 🔴 Kritiske Problemer (Høj Prioritet)

#### 1. **JSON Syntax Fejl i appsettings.json**
**Linje 75-81:** Manglende "Destinations" wrapper i auth-cluster
```json
"auth-cluster": {
  "auth-destination": {  // ❌ FEJL: Mangler "Destinations" wrapper
    "Address": "http://authservice:8080"
  }
}
```

**Impact:** Kan forårsage runtime fejl  
**Fix:** Tilføj manglende "Destinations" nøgle

#### 2. **Ingen Rate Limiting**
Gateway'en har ingen beskyttelse mod:
- DDoS attacks
- API abuse
- Brute force attacks på login endpoints

**Impact:** Systemet er sårbart over for overbelastning  
**Anbefaling:** Implementer AspNetCoreRateLimit

#### 3. **Manglende CORS Konfiguration**
Ingen CORS politik er defineret, hvilket kan:
- Blokere legitime frontend requests
- Eller tillade alle origins (usikkert)

**Impact:** Frontend integration problemer eller sikkerhedsrisici  
**Anbefaling:** Implementer restriktiv CORS politik

#### 4. **HttpClient Anti-Pattern i Middleware**
```csharp
private readonly HttpClient _httpClient;
public JwtAuthenticationMiddleware(...) {
    _httpClient = new HttpClient(); // ❌ ANTI-PATTERN
}
```

**Problem:** 
- Socket exhaustion
- Memory leaks
- Performance problemer

**Impact:** Kan crashe applikationen under høj load  
**Fix:** Brug IHttpClientFactory

#### 5. **Ingen Circuit Breaker Pattern**
Hvis en downstream service fejler:
- Gateway fortsætter med at sende requests
- Ingen automatic retry logic
- Ingen fallback mekanisme

**Impact:** Cascade failures kan tage hele systemet ned  
**Anbefaling:** Implementer Polly for resilience

#### 6. **Hardcoded Configuration**
Swagger endpoints er hardcoded i Program.cs med duplikeret kode:
```csharp
app.MapGet("/swagger/auth/swagger.json", async (IConfiguration config) => {
    using var client = new HttpClient(); // ❌ Hver gang!
    // ... 10 linjer duplikeret kode
});
// Gentaget 6 gange for hver service
```

**Impact:** Svært at vedligeholde, fejlprone  
**Fix:** Refaktorer til service-baseret løsning

#### 7. **Ingen Request/Response Logging**
Ingen centraliseret logging af:
- Request details (method, path, headers)
- Response status codes
- Execution time
- Errors og exceptions

**Impact:** Svært at debugge produktionsproblemer  
**Anbefaling:** Implementer Serilog med structured logging

#### 8. **Manglende API Versioning**
Ingen strategi for API versioning:
- Kan ikke introducere breaking changes
- Ingen backward compatibility
- Svært at migrere clients

**Impact:** Fremtidige opdateringer vil være problematiske  
**Anbefaling:** Implementer URL-baseret versioning

### ⚠️ Alvorlige Problemer (Medium Prioritet)

#### 9. **Ingen Request Timeout Configuration**
Ingen timeouts konfigureret for downstream services:
- Requests kan hænge i det uendelige
- Ingen protection mod slow services

**Impact:** Resource exhaustion  
**Fix:** Konfigurer timeouts i YARP

#### 10. **Manglende Response Caching**
Ingen caching strategi for:
- Ofte-forespurgte data (books, search results)
- Static content
- API responses

**Impact:** Unødvendig load på downstream services  
**Anbefaling:** Implementer response caching middleware

#### 11. **Ingen Load Balancing**
Hver service har kun én destination:
- Ingen high availability
- Ingen horizontal scaling
- Single point of failure

**Impact:** Begrænset skalerbarhed  
**Anbefaling:** Konfigurer multiple destinations per service

#### 12. **Manglende Security Headers**
Ingen security headers som:
- X-Content-Type-Options
- X-Frame-Options
- X-XSS-Protection
- Strict-Transport-Security
- Content-Security-Policy

**Impact:** Sårbar over for XSS, clickjacking, etc.  
**Fix:** Tilføj security headers middleware

#### 13. **Ingen Metrics/Monitoring**
Ingen integration med monitoring tools:
- Ingen metrics collection (Prometheus)
- Ingen distributed tracing (OpenTelemetry)
- Ingen application insights

**Impact:** Blind over for performance og problemer  
**Anbefaling:** Implementer OpenTelemetry

### 📝 Mindre Problemer (Lav Prioritet)

#### 14. **Swagger Aggregation Fejlhåndtering**
Swagger endpoints returnerer bare NotFound() ved fejl:
```csharp
catch {
    return Results.NotFound(); // ❌ Ingen logging, ingen detaljer
}
```

**Impact:** Svært at debugge swagger problemer  
**Fix:** Log fejl og returner bedre error responses

#### 15. **Miljø-Specifik Konfiguration**
Docker Compose sætter `ASPNETCORE_ENVIRONMENT=Production`, men:
- Ingen production-specifik konfiguration
- Swagger er kun enabled i Development
- Ingen environment-baseret security policies

**Impact:** Inkonsistent opførsel mellem miljøer  
**Fix:** Opret environment-specific appsettings

---

## 🎯 Anbefalet Arkitektur

### Arkitektur Principper

1. **Security First**
   - Defense in depth
   - Least privilege
   - Fail secure

2. **Resilience**
   - Circuit breakers
   - Retry policies
   - Fallback mechanisms

3. **Observability**
   - Structured logging
   - Distributed tracing
   - Metrics collection

4. **Performance**
   - Response caching
   - Connection pooling
   - Async/await everywhere

5. **Maintainability**
   - Clean code
   - SOLID principles
   - Comprehensive documentation

### Ny Komponent Struktur

```
ApiGateway/
├── Program.cs                          # Minimal startup
├── appsettings.json                    # Base configuration
├── appsettings.Development.json        # Dev overrides
├── appsettings.Production.json         # Prod overrides
├── Configuration/
│   ├── ServiceEndpoints.cs             # Service URL constants
│   ├── SecuritySettings.cs             # Security configuration
│   └── RateLimitSettings.cs            # Rate limit rules
├── Middleware/
│   ├── JwtAuthenticationMiddleware.cs  # Refactored auth
│   ├── RequestLoggingMiddleware.cs     # Request/response logging
│   ├── ExceptionHandlingMiddleware.cs  # Global error handling
│   └── SecurityHeadersMiddleware.cs    # Security headers
├── Services/
│   ├── ITokenValidationService.cs      # Interface
│   ├── TokenValidationService.cs       # JWT validation
│   ├── ISwaggerAggregationService.cs   # Interface
│   └── SwaggerAggregationService.cs    # Swagger aggregation
├── Extensions/
│   ├── ServiceCollectionExtensions.cs  # DI setup
│   ├── YarpExtensions.cs               # YARP configuration
│   └── HealthCheckExtensions.cs        # Health check setup
├── Policies/
│   ├── ResiliencePolicies.cs           # Polly policies
│   └── CachePolicies.cs                # Caching policies
└── Models/
    ├── ErrorResponse.cs                # Standard error format
    └── HealthCheckResponse.cs          # Health check format
```

---

## 🔧 Implementation Plan

### Phase 1: Kritiske Rettelser (Uge 1)

**Prioritet:** 🔴 Høj  
**Estimeret tid:** 2-3 dage

1. **Fix JSON Syntax Error**
   - Ret appsettings.json
   - Test alle routes
   - Verificer health checks

2. **Implementer Rate Limiting**
   ```csharp
   // Install: AspNetCoreRateLimit
   services.AddMemoryCache();
   services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
   services.AddInMemoryRateLimiting();
   ```

3. **Fix HttpClient Anti-Pattern**
   ```csharp
   services.AddHttpClient<ITokenValidationService, TokenValidationService>();
   ```

4. **Tilføj CORS**
   ```csharp
   services.AddCors(options => {
       options.AddPolicy("AllowFrontend", builder => {
           builder.WithOrigins("http://localhost:3000")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
       });
   });
   ```

### Phase 2: Sikkerhed og Resilience (Uge 2)

**Prioritet:** 🔴 Høj  
**Estimeret tid:** 3-4 dage

1. **Circuit Breaker med Polly**
   ```csharp
   services.AddHttpClient("downstream")
       .AddPolicyHandler(GetRetryPolicy())
       .AddPolicyHandler(GetCircuitBreakerPolicy());
   ```

2. **Security Headers**
   ```csharp
   app.UseSecurityHeaders(policies => {
       policies.AddDefaultSecurityHeaders();
       policies.AddStrictTransportSecurityMaxAgeIncludeSubDomains();
       policies.AddContentSecurityPolicy(builder => {
           builder.DefaultSources(s => s.Self());
       });
   });
   ```

3. **Request/Response Logging**
   ```csharp
   services.AddSerilog((services, lc) => lc
       .ReadFrom.Configuration(Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console()
       .WriteTo.Seq("http://seq:5341"));
   ```

4. **Global Exception Handling**
   ```csharp
   app.UseMiddleware<ExceptionHandlingMiddleware>();
   ```

### Phase 3: Performance og Observability (Uge 3)

**Prioritet:** ⚠️ Medium  
**Estimeret tid:** 3-4 dage

1. **Response Caching**
   ```csharp
   services.AddResponseCaching();
   services.AddOutputCache(options => {
       options.AddBasePolicy(builder => builder.Cache());
       options.AddPolicy("books", builder => 
           builder.Cache().Expire(TimeSpan.FromMinutes(5)));
   });
   ```

2. **OpenTelemetry**
   ```csharp
   services.AddOpenTelemetry()
       .WithTracing(builder => builder
           .AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
           .AddJaegerExporter())
       .WithMetrics(builder => builder
           .AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
           .AddPrometheusExporter());
   ```

3. **Health Checks Forbedringer**
   ```csharp
   services.AddHealthChecks()
       .AddUrlGroup(uri, name, timeout: TimeSpan.FromSeconds(5))
       .AddCheck<CustomHealthCheck>("custom");
   
   app.MapHealthChecks("/health", new HealthCheckOptions {
       ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
   });
   ```

4. **YARP Timeout Configuration**
   ```json
   "Clusters": {
       "auth-cluster": {
           "HttpRequest": {
               "Timeout": "00:00:30"
           }
       }
   }
   ```

### Phase 4: Advanced Features (Uge 4)

**Prioritet:** 📝 Lav  
**Estimeret tid:** 2-3 dage

1. **API Versioning**
   ```csharp
   services.AddApiVersioning(options => {
       options.DefaultApiVersion = new ApiVersion(1, 0);
       options.AssumeDefaultVersionWhenUnspecified = true;
       options.ReportApiVersions = true;
   });
   ```

2. **Load Balancing**
   ```json
   "Clusters": {
       "books-cluster": {
           "LoadBalancingPolicy": "RoundRobin",
           "Destinations": {
               "books-1": { "Address": "http://bookservice-1:8080" },
               "books-2": { "Address": "http://bookservice-2:8080" }
           }
       }
   }
   ```

3. **Request Transformation**
   ```csharp
   .AddTransforms(context => {
       context.AddRequestHeader("X-Gateway-Version", "1.0");
       context.AddRequestHeader("X-Forwarded-For", 
           context.HttpContext.Connection.RemoteIpAddress?.ToString());
   });
   ```

4. **Swagger Refactoring**
   ```csharp
   services.AddSingleton<ISwaggerAggregationService, SwaggerAggregationService>();
   
   app.MapGet("/swagger/{service}/swagger.json", 
       async (string service, ISwaggerAggregationService swaggerService) => 
           await swaggerService.GetSwaggerDocumentAsync(service));
   ```

---

## 🔒 Sikkerhedsanbefalinger

### 1. Authentication & Authorization

**Nuværende:** Basic JWT validation  
**Anbefalet:**
- JWT validation med proper key management
- Role-based access control (RBAC)
- Scope-based authorization
- Token refresh mechanism

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.Authority = "https://authservice";
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero
        };
    });

services.AddAuthorization(options => {
    options.AddPolicy("AdminOnly", policy => 
        policy.RequireRole("Admin"));
    options.AddPolicy("SellerOrAdmin", policy => 
        policy.RequireRole("Seller", "Admin"));
});
```

### 2. Rate Limiting Strategi

**Anbefalet konfiguration:**
```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Real-IP",
    "ClientIdHeader": "X-ClientId",
    "HttpStatusCode": 429,
    "GeneralRules": [
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 100
      },
      {
        "Endpoint": "*/auth/login",
        "Period": "1m",
        "Limit": 5
      },
      {
        "Endpoint": "*/auth/register",
        "Period": "1h",
        "Limit": 3
      }
    ]
  }
}
```

### 3. CORS Politik

**Anbefalet:**
```csharp
services.AddCors(options => {
    options.AddPolicy("Production", builder => {
        builder.WithOrigins(
                "https://georgiatech-library.com",
                "https://www.georgiatech-library.com")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials()
               .SetIsOriginAllowedToAllowWildcardSubdomains();
    });
    
    options.AddPolicy("Development", builder => {
        builder.WithOrigins("http://localhost:3000", "http://localhost:3001")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});
```

### 4. Security Headers

**Anbefalet headers:**
```csharp
app.Use(async (context, next) => {
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    
    if (context.Request.IsHttps) {
        context.Response.Headers.Add("Strict-Transport-Security", 
            "max-age=31536000; includeSubDomains; preload");
    }
    
    await next();
});
```

### 5. Input Validation

**Anbefalet:**
- Validér alle query parameters
- Sanitize path parameters
- Limit request body size
- Validate content types

```csharp
services.Configure<FormOptions>(options => {
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
});

app.Use(async (context, next) => {
    if (context.Request.ContentLength > 10 * 1024 * 1024) {
        context.Response.StatusCode = 413; // Payload Too Large
        await context.Response.WriteAsync("Request body too large");
        return;
    }
    await next();
});
```

---

## 📊 Performance Anbefalinger

### 1. Connection Pooling

**YARP konfiguration:**
```json
{
  "Clusters": {
    "books-cluster": {
      "HttpClient": {
        "MaxConnectionsPerServer": 100,
        "DangerousAcceptAnyServerCertificate": false,
        "RequestHeaderEncoding": "utf-8"
      }
    }
  }
}
```

### 2. Response Compression

```csharp
services.AddResponseCompression(options => {
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

services.Configure<BrotliCompressionProviderOptions>(options => {
    options.Level = CompressionLevel.Optimal;
});
```

### 3. Output Caching Strategi

```csharp
services.AddOutputCache(options => {
    // Public endpoints - cache aggressively
    options.AddPolicy("books-list", builder => builder
        .Cache()
        .Expire(TimeSpan.FromMinutes(5))
        .SetVaryByQuery("page", "pageSize", "sortBy"));
    
    options.AddPolicy("search-results", builder => builder
        .Cache()
        .Expire(TimeSpan.FromMinutes(2))
        .SetVaryByQuery("*"));
    
    // User-specific - cache per user
    options.AddPolicy("user-data", builder => builder
        .Cache()
        .Expire(TimeSpan.FromMinutes(1))
        .SetVaryByHeader("Authorization"));
});
```

### 4. Async Best Practices

**Alle I/O operationer skal være async:**
```csharp
// ❌ BAD
var response = httpClient.GetStringAsync(url).Result;

// ✅ GOOD
var response = await httpClient.GetStringAsync(url);
```

---

## 🔍 Monitoring og Observability

### 1. Structured Logging

**Serilog konfiguration:**
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.Seq"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Yarp": "Information"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      { 
        "Name": "Seq",
        "Args": { "serverUrl": "http://seq:5341" }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"],
    "Properties": {
      "Application": "ApiGateway",
      "Environment": "Production"
    }
  }
}
```

### 2. Custom Metrics

```csharp
public class MetricsMiddleware {
    private static readonly Counter RequestCounter = Metrics
        .CreateCounter("gateway_requests_total", "Total requests",
            new CounterConfiguration {
                LabelNames = new[] { "method", "endpoint", "status_code" }
            });
    
    private static readonly Histogram RequestDuration = Metrics
        .CreateHistogram("gateway_request_duration_seconds", "Request duration",
            new HistogramConfiguration {
                LabelNames = new[] { "method", "endpoint" }
            });
    
    public async Task InvokeAsync(HttpContext context) {
        var sw = Stopwatch.StartNew();
        
        try {
            await _next(context);
        } finally {
            sw.Stop();
            
            RequestCounter
                .WithLabels(context.Request.Method, 
                           context.Request.Path, 
                           context.Response.StatusCode.ToString())
                .Inc();
            
            RequestDuration
                .WithLabels(context.Request.Method, context.Request.Path)
                .Observe(sw.Elapsed.TotalSeconds);
        }
    }
}
```

### 3. Health Check Dashboard

```csharp
services.AddHealthChecksUI(options => {
    options.SetEvaluationTimeInSeconds(30);
    options.MaximumHistoryEntriesPerEndpoint(50);
    options.AddHealthCheckEndpoint("API Gateway", "/health");
})
.AddInMemoryStorage();

app.MapHealthChecksUI(options => {
    options.UIPath = "/health-ui";
});
```

### 4. Distributed Tracing

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => {
        builder
            .AddAspNetCoreInstrumentation(options => {
                options.RecordException = true;
                options.Filter = (httpContext) => {
                    // Don't trace health checks
                    return !httpContext.Request.Path.StartsWithSegments("/health");
                };
            })
            .AddHttpClientInstrumentation()
            .AddSource("ApiGateway")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("ApiGateway"))
            .AddJaegerExporter(options => {
                options.AgentHost = "jaeger";
                options.AgentPort = 6831;
            });
    });
```

---

## 🧪 Testing Strategi

### 1. Unit Tests

**Test middleware:**
```csharp
[Fact]
public async Task JwtMiddleware_ValidToken_AllowsRequest() {
    // Arrange
    var context = new DefaultHttpContext();
    context.Request.Headers["Authorization"] = $"Bearer {validToken}";
    
    // Act
    await middleware.InvokeAsync(context);
    
    // Assert
    Assert.Equal(200, context.Response.StatusCode);
}
```

### 2. Integration Tests

**Test routing:**
```csharp
[Fact]
public async Task Gateway_RoutesToBookService() {
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", validToken);
    
    // Act
    var response = await client.GetAsync("/books");
    
    // Assert
    response.EnsureSuccessStatusCode();
}
```

### 3. Load Tests

**K6 script:**
```javascript
import http from 'k6/http';
import { check } from 'k6';

export let options = {
    stages: [
        { duration: '2m', target: 100 },
        { duration: '5m', target: 100 },
        { duration: '2m', target: 0 },
    ],
};

export default function() {
    let response = http.get('http://localhost:5004/books');
    check(response, {
        'status is 200': (r) => r.status === 200,
        'response time < 500ms': (r) => r.timings.duration < 500,
    });
}
```

---

## 📝 Refaktorering Checklist

### Før Refaktorering
- [ ] Backup nuværende kode
- [ ] Dokumenter alle endpoints
- [ ] Kør alle eksisterende tests
- [ ] Tag performance baseline metrics
- [ ] Informer team om planlagte ændringer

### Under Refaktorering
- [ ] Fix JSON syntax error først
- [ ] Implementer én feature ad gangen
- [ ] Skriv tests for hver ny feature
- [ ] Test efter hver ændring
- [ ] Commit ofte med beskrivende messages
- [ ] Opdater dokumentation løbende

### Efter Refaktorering
- [ ] Kør alle tests
- [ ] Verificer alle endpoints virker
- [ ] Sammenlign performance metrics
- [ ] Opdater API dokumentation
- [ ] Opdater README
- [ ] Code review
- [ ] Deploy til staging
- [ ] Smoke test i staging
- [ ] Deploy til production

### Cleanup
- [ ] Fjern gamle/unused filer
- [ ] Fjern commented-out kode
- [ ] Fjern debug logging
- [ ] Verificer ingen duplikerede filer
- [ ] Opdater .gitignore hvis nødvendigt

---

## 🚀 Deployment Anbefalinger

### 1. Docker Configuration

**Optimeret Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ApiGateway/ApiGateway.csproj", "ApiGateway/"]
RUN dotnet restore "ApiGateway/ApiGateway.csproj"
COPY . .
WORKDIR "/src/ApiGateway"
RUN dotnet build "ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Security: Run as non-root
USER $APP_UID

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ApiGateway.dll"]
```

### 2. Docker Compose Updates

```yaml
apigateway:
  build:
    context: .
    dockerfile: ApiGateway/Dockerfile
  ports:
    - "5004:8080"
  depends_on:
    bookservice:
      condition: service_healthy
    warehouseservice:
      condition: service_healthy
    searchservice:
      condition: service_healthy
    orderservice:
      condition: service_healthy
    userservice:
      condition: service_healthy
    authservice:
      condition: service_healthy
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ASPNETCORE_URLS=http://+:8080
  restart: unless-stopped
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
    interval: 30s
    timeout: 10s
    retries: 3
    start_period: 40s
  networks:
    - backend
  deploy:
    resources:
      limits:
        cpus: '1'
        memory: 512M
      reservations:
        cpus: '0.5'
        memory: 256M
```

### 3. Kubernetes (Fremtid)

**Deployment manifest:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: apigateway
spec:
  replicas: 3
  selector:
    matchLabels:
      app: apigateway
  template:
    metadata:
      labels:
        app: apigateway
    spec:
      containers:
      - name: apigateway
        image: georgiatech/apigateway:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        resources:
          requests:
            memory: "256Mi"
            cpu: "500m"
          limits:
            memory: "512Mi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: apigateway
spec:
  selector:
    app: apigateway
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: LoadBalancer
```

---

## 📚 Dokumentation Updates

### Nye dokumenter der skal oprettes:

1. **ApiGateway/SECURITY.md**
   - Security policies
   - Authentication flow
   - Authorization rules
   - Rate limiting details

2. **ApiGateway/MONITORING.md**
   - Metrics overview
   - Logging strategy
   - Alerting rules
   - Dashboard links

3. **ApiGateway/TROUBLESHOOTING.md**
   - Common issues
   - Debug procedures
   - Performance tuning
   - FAQ

4. **ApiGateway/DEVELOPMENT.md**
   - Local setup
   - Testing procedures
   - Contribution guidelines
   - Code standards

### Updates til eksisterende dokumenter:

1. **ApiGateway/README.md**
   - Opdater med nye features
   - Tilføj security sektion
   - Tilføj monitoring sektion
   - Opdater configuration eksempler

2. **API-DOCUMENTATION.md**
   - Tilføj rate limiting info
   - Tilføj versioning info
   - Opdater error responses
   - Tilføj security headers info

---

## 🎯 Success Metrics

### Performance Targets
- **Response Time:** P95 < 100ms, P99 < 200ms
- **Throughput:** > 1000 req/sec
- **Error Rate:** < 0.1%
- **Availability:** > 99.9%

### Security Targets
- **No Critical Vulnerabilities:** 0 critical CVEs
- **Rate Limiting:** Effective against DDoS
- **Authentication:** 100% of protected endpoints
- **Security Headers:** All recommended headers present

### Observability Targets
- **Log Coverage:** 100% of requests logged
- **Trace Coverage:** 100% of requests traced
- **Metrics Coverage:** All key metrics collected
- **Alert Coverage:** Alerts for all critical issues

---

## 🔄 Migration Plan

### Step-by-Step Migration

**Week 1: Preparation**
1. Review current implementation
2. Set up test environment
3. Create baseline metrics
4. Backup current configuration

**Week 2: Core Fixes**
1. Fix JSON syntax error
2. Implement HttpClient factory
3. Add rate limiting
4. Add CORS

**Week 3: Security & Resilience**
1. Implement circuit breakers
2. Add security headers
3. Implement proper logging
4. Add exception handling

**Week 4: Performance & Monitoring**
1. Add response caching
2. Implement OpenTelemetry
3. Optimize health checks
4. Add metrics collection

**Week 5: Testing & Documentation**
1. Comprehensive testing
2. Update all documentation
3. Create runbooks
4. Team training

**Week 6: Deployment**
1. Deploy to staging
2. Smoke testing
3. Performance testing
4. Production deployment

---

## ⚠️ Risks og Mitigation

### Risk 1: Breaking Changes
**Risk:** Refactoring kan bryde eksisterende clients  
**Mitigation:**
- Maintain backward compatibility
- Version all APIs
- Comprehensive testing
- Gradual rollout

### Risk 2: Performance Degradation
**Risk:** Nye features kan påvirke performance  
**Mitigation:**
- Performance testing før deployment
- Monitoring i real-time
- Rollback plan klar
- Circuit breakers for protection

### Risk 3: Security Vulnerabilities
**Risk:** Nye features kan introducere sårbarheder  
**Mitigation:**
- Security review af al ny kode
- Automated security scanning
- Penetration testing
- Regular security audits

### Risk 4: Downtime
**Risk:** Deployment kan forårsage downtime  
**Mitigation:**
- Blue-green deployment
- Health checks før cutover
- Automated rollback
- Maintenance window kommunikation

---

## 📞 Support og Resources

### Team Contacts
- **Architecture Lead:** [Navn]
- **Security Lead:** [Navn]
- **DevOps Lead:** [Navn]

### External Resources
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [ASP.NET Core Security](https://docs.microsoft.com/en-us/aspnet/core/security/)
- [Polly Documentation](https://github.com/App-vNext/Polly)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)

### Tools
- **Monitoring:** Prometheus + Grafana
- **Logging:** Seq / ELK Stack
- **Tracing:** Jaeger
- **Testing:** K6 / JMeter

---

## ✅ Conclusion

API Gateway'en har et solidt fundament med YARP, men kræver betydelige forbedringer for at være produktionsklar. De kritiske problemer (JSON fejl, HttpClient anti-pattern, manglende rate limiting) skal fixes omgående.

Den foreslåede arkitektur adresserer alle identificerede problemer og følger industry best practices for:
- Security
- Resilience
- Performance
- Observability
- Maintainability

Med den foreslåede implementation plan kan gateway'en transformeres til en robust, sikker og skalerbar løsning inden for 4-6 uger.

**Anbefaling:** Start med Phase 1 (kritiske rettelser) omgående, og fortsæt derefter med de andre phases i prioriteret rækkefølge.

---

**Dokument Version:** 1.0  
**Sidste Opdatering:** 19. November 2025  
**Næste Review:** Efter Phase 1 completion  
**Status:** Klar til implementation

