# K6 Search Load Test

Denne mappe indeholder en komplet K6 load test til at validere søgeendepunktets performance for Georgia Tech Library Marketplace.

## Performance Krav

- **1500+ requests/minut** (25 requests/sekund)
- **Response time <200ms** (krav for både P95 og P99 percentiler)
- **Fejlrate <1%**
- **Traffic mix**: 70% korte søgninger, 30% komplekse søgninger

## Prerequisites

K6 skal installeres separat. K6 er ikke en npm-pakke, men et standalone værktøj.

### Installation

#### Windows (ved hjælp af Chocolatey)
```bash
choco install k6
```

#### Windows (ved hjælp af winget)
```bash
winget install k6
```

#### Windows (Manuel download)
1. Download K6 fra [https://k6.io/docs/getting-started/installation/](https://k6.io/docs/getting-started/installation/)
2. Ekstraher og tilføj til PATH

#### macOS
```bash
brew install k6
```

#### Linux (Debian/Ubuntu)
```bash
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6
```

#### Linux (RedHat/CentOS)
```bash
sudo dnf install https://dl.k6.io/rpm/repo.rpm
sudo dnf install k6
```

### Verificer Installation

Efter installation, verificer at K6 er installeret korrekt:
```bash
k6 version
```

**Vigtigt for Windows:** Hvis K6 ikke er genkendt efter installation, skal du enten:
1. **Genstarte PowerShell** (anbefalet), eller
2. **Opdatere PATH i den aktuelle session:**
   ```powershell
   $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
   ```
3. **Eller kør helper scriptet:**
   ```powershell
   .\load-test\refresh-path.ps1
   ```

## Kørsel af Testen

### Test Strategi

Der er to testfiler tilgængelige:

1. **`search-load-test.js`** - Tester gennem API Gateway (port 5004)
   - Simulerer realistisk trafik gennem gateway
   - Tester hele routing-stakken
   - Anbefalet til end-to-end testing

2. **`search-load-test-direct.js`** - Tester direkte mod SearchService (port 5002)
   - Bypasser API Gateway
   - Isolerer SearchService performance
   - Anbefalet til debugging og isoleret performance testing

### Basic Test (Standard Base URL - API Gateway)

Kør testen med standard base URL (`http://localhost:5004`):

```bash
cd load-test
k6 run search-load-test.js
```

### Test Direkte Mod SearchService

For at teste direkte mod SearchService (bypasser API Gateway):

```bash
# Windows PowerShell
cd load-test
k6 run search-load-test-direct.js

# Eller med custom URL
$env:BASE_URL="http://localhost:5002"; k6 run search-load-test-direct.js
```

### Custom Base URL

Kør testen mod en anden base URL ved hjælp af environment variable:

```bash
# Windows PowerShell
$env:BASE_URL="http://localhost:5004"; k6 run search-load-test.js

# Windows CMD
set BASE_URL=http://localhost:5004 && k6 run search-load-test.js

# Linux/macOS
BASE_URL=http://localhost:5004 k6 run search-load-test.js
```

### Brug af npm Scripts

Hvis du har Node.js installeret, kan du bruge npm scripts fra `package.json`:

```bash
# Standard test (localhost:5004)
npm run test

# Eksplicit localhost
npm run test:local

# Production (skift URL først)
npm run test:production

# Custom URL (sæt BASE_URL environment variable først)
npm run test:custom
```

### Production Test

For at teste mod production miljø:

```bash
# Windows PowerShell
$env:BASE_URL="https://api.production.com"; k6 run search-load-test.js

# Linux/macOS
BASE_URL=https://api.production.com k6 run search-load-test.js
```

## Test Scenarier

### Load Pattern

Testen bruger følgende load pattern:

1. **Ramp-up**: 0 → 25 req/sec over 2 minutter
2. **Sustain**: 25 req/sec (1500 req/min) i 5 minutter
3. **Ramp-down**: 25 → 0 req/sec over 1 minut

**Total test varighed**: ~8 minutter

### Traffic Mix

Testen simulerer realistisk trafik med følgende fordeling:

#### 70% Korte Søgninger
- **Format**: `GET /search?query={single-word}`
- **Eksempler**: 
  - `GET /search?query=python`
  - `GET /search?query=java`
  - `GET /search?query=fiction`
- **Karakteristik**: Enkelt ord queries, hurtige og simple søgninger

#### 30% Komplekse Søgninger
- **Format**: `GET /search?query={multi-word}&page={1-5}&pageSize=20&sortBy={relevance|price|title}`
- **Eksempler**:
  - `GET /search?query=python programming&page=1&pageSize=20&sortBy=price`
  - `GET /search?query=computer science&page=2&pageSize=20&sortBy=relevance`
  - `GET /search?query=machine learning&page=3&pageSize=20&sortBy=title`
- **Karakteristik**: Flere ord + query parametre (pagination, sorting)

### Test Data

Testen bruger arrays med realistiske søgeord baseret på bogkatalog:

**Korte søgninger** (20 forskellige):
- python, java, fiction, science, history, math, physics, engineering, programming, database, algorithm, design, network, security, web, mobile, cloud, data, machine, artificial

**Komplekse søgninger** (15 forskellige):
- python programming, computer science, data structures, machine learning, web development, software engineering, database systems, network security, cloud computing, artificial intelligence, object oriented, system design, algorithm analysis, distributed systems, cyber security

## Metrics og Thresholds

### Tracked Metrics

Testen tracker følgende metrics:

- **`search_response_time`**: Response time for alle søgninger (korte + komplekse)
- **`short_search_response_time`**: Response time specifikt for korte søgninger
- **`complex_search_response_time`**: Response time specifikt for komplekse søgninger
- **`error_rate`**: Fejlrate for alle requests
- **`http_req_failed`**: HTTP request failure rate
- **`http_reqs`**: Total antal requests per sekund

### Performance Thresholds

Testen validerer følgende thresholds:

- ✅ **Response time P95 < 200ms**: 95% af requests skal være < 200ms
- ✅ **Response time P99 < 200ms**: 99% af requests skal være < 200ms
- ✅ **Error rate < 1%**: Fejlrate skal være under 1%
- ✅ **HTTP failure rate < 1%**: HTTP fejl skal være under 1%
- ✅ **Throughput >= 25 req/sec**: Systemet skal håndtere mindst 25 requests per sekund

Hvis nogen af disse thresholds ikke opfyldes, vil testen fejle.

## Fortolkning af Resultater

### Eksempel Output

```
     ✓ short search status is 200
     ✓ short search response time < 200ms
     ✓ short search has valid JSON
     ✓ short search has results
     ✓ complex search status is 200
     ✓ complex search response time < 200ms
     ✓ complex search has valid JSON
     ✓ complex search has pagination data

     checks.........................: 100.00% ✓ 15000    ✗ 0
     data_received..................: 45 MB   9.4 MB/s
     data_sent......................: 2.1 MB  438 kB/s
     http_req_duration..............: avg=125ms   min=45ms    med=110ms    max=350ms   p(95)=180ms   p(99)=195ms
     http_req_failed................: 0.00%   ✓ 0        ✗ 0
     http_reqs......................: 12000   25.0/s
     iteration_duration.............: avg=225ms   min=145ms    med=210ms    max=450ms   p(95)=280ms   p(99)=295ms
     iterations.....................: 12000   25.0/s
     vus............................: 1       min=1      max=25
     vus_max........................: 25       min=25      max=25

     search_response_time...........: avg=125ms   min=45ms    med=110ms    max=350ms   p(95)=180ms   p(99)=195ms
     short_search_response_time.....: avg=110ms   min=40ms    med=100ms    max=300ms   p(95)=170ms   p(99)=185ms
     complex_search_response_time....: avg=150ms   min=50ms    med=140ms    max=350ms   p(95)=190ms   p(99)=200ms
     error_rate.....................: 0.00%   ✓ 0        ✗ 0
```

### Vigtige Metrics at Overvåge

1. **`http_req_duration`**:
   - **p(95)**: 95% af requests er under denne værdi (skal være < 200ms)
   - **p(99)**: 99% af requests er under denne værdi (skal være < 200ms)
   - **avg**: Gennemsnitlig response time

2. **`http_reqs`**: 
   - Antal requests per sekund (skal være >= 25 req/sec for at opfylde 1500 req/min kravet)

3. **`http_req_failed`**:
   - Fejlrate (skal være < 1%)

4. **`error_rate`**:
   - Custom error rate (skal være < 1%)

5. **`search_response_time`** (custom metrics):
   - Specifik response time for søgninger
   - Sammenlign `short_search_response_time` vs `complex_search_response_time` for at identificere performance forskelle

### Success Kriterier

Testen anses for succesfuld hvis:

- ✅ Alle thresholds er opfyldt (ingen røde ✗ i output)
- ✅ P95 response time < 200ms
- ✅ P99 response time < 200ms
- ✅ Error rate < 1%
- ✅ Throughput >= 25 req/sec (1500+ req/min)

### Fejl Analyse

Hvis testen fejler, tjek følgende:

1. **Response time for høj**:
   - Tjek om SearchService kører optimalt
   - Verificer Redis cache er aktiv og fungerer
   - Tjek database performance
   - Overvej at skalere SearchService

2. **Error rate for høj**:
   - Tjek API Gateway logs
   - Tjek SearchService logs
   - Verificer at alle services er oppe og kører
   - Tjek for rate limiting issues

3. **Throughput for lav**:
   - Tjek om systemet kan håndtere load
   - Verificer at ingen bottlenecks eksisterer
   - Overvej at øge antal instances

## Troubleshooting

### Problem: "API Gateway health check returned status XXX"

**Løsning**: 
- Verificer at API Gateway kører på port 5004
- Tjek at alle services er startet
- Kør: `docker-compose ps` for at se service status

### Problem: "Connection refused" eller timeout errors

**Løsning**:
- Verificer at API Gateway er tilgængelig: `curl http://localhost:5004/health`
- Tjek firewall indstillinger
- Verificer at BASE_URL er korrekt sat

### Problem: Response times er for høje (>200ms)

**Løsning**:
- Tjek SearchService performance
- Verificer Redis cache hit rate
- Tjek database query performance
- Overvej at optimere søgeindeks

### Problem: Testen når ikke target throughput (25 req/sec)

**Løsning**:
- Tjek om systemet kan håndtere load
- Verificer at ingen rate limiting blokerer requests
- Tjek system ressourcer (CPU, memory, network)
- Overvej at justere test konfiguration (stages)

## Continuous Integration

Testen kan integreres i CI/CD pipelines:

```yaml
# GitHub Actions eksempel
- name: Run K6 Load Test
  run: |
    k6 run --out json=load-test-results.json load-test/search-load-test.js
  
- name: Upload Results
  uses: actions/upload-artifact@v2
  with:
    name: load-test-results
    path: load-test-results.json
```

## Yderligere Ressourcer

- [K6 Dokumentation](https://k6.io/docs/)
- [K6 Best Practices](https://k6.io/docs/using-k6/best-practices/)
- [K6 Thresholds Guide](https://k6.io/docs/using-k6/thresholds/)
- [K6 Metrics Reference](https://k6.io/docs/using-k6/metrics/)

## Noter

- Testen er designet til at køre mod API Gateway (`http://localhost:5004`) for realistisk test
- Alle søgninger er public endpoints (ingen authentication nødvendig)
- Testen inkluderer automatisk health check i setup phase
- Testen bruger minimal think time (0.1-0.15s) for at opnå target throughput

## Support

Ved spørgsmål eller problemer, kontakt teamet eller se projekt dokumentation.

