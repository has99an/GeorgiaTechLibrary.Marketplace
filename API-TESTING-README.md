# API Testing & Documentation Files

This directory contains comprehensive API testing and documentation for the GeorgiaTechLibrary.Marketplace microservices system.

---

## 📄 Files Overview

### 1. API-DOCUMENTATION.md
**Complete API Reference Guide**

A comprehensive 50+ page documentation covering all API endpoints across all microservices.

**Contents:**
- Authentication & Authorization guide
- All 22+ endpoints documented with:
  - HTTP method and URL
  - Authentication requirements
  - Request parameters and body examples
  - Response examples with status codes
  - Error handling
- Known issues and workarounds
- Architecture notes
- Database statistics
- UI development recommendations

**Use this for:** Complete API integration reference

---

### 2. API-TESTING-SUMMARY.md
**Quick Reference & Test Results**

A concise summary of API testing results and key findings.

**Contents:**
- Test results: 19/22 endpoints working (86%)
- Working endpoints by service
- Known issues with fixes
- Authentication summary
- Recommendations for UI development
- Action items for backend/frontend teams

**Use this for:** Quick overview and status check

---

### 3. test-api-endpoints.ps1
**Automated Testing Script**

PowerShell script that systematically tests all API endpoints through the ApiGateway.

**Features:**
- Tests all 22+ endpoints
- Handles authentication automatically
- Creates test users and data
- Cleans up test data
- Generates detailed results
- Color-coded console output

**Usage:**
```powershell
cd "C:\Softwareudvikling\Semester Projekt\GeorgiaTechLibrary.Marketplace"
.\test-api-endpoints.ps1
```

**Output:**
- Console: Real-time test results with colors
- File: `api-test-results.json` (detailed results)

**Use this for:** Automated testing after deployments

---

## 🎯 Quick Start Guide

### For Frontend Developers

1. **Read First:** `API-TESTING-SUMMARY.md` (5 min read)
   - Get overview of what works
   - Understand authentication flow
   - See known issues

2. **Reference:** `API-DOCUMENTATION.md`
   - Look up specific endpoints
   - Copy request/response examples
   - Check authentication requirements

3. **Test:** Run `test-api-endpoints.ps1`
   - Verify services are running
   - Test your changes don't break existing endpoints

### For Backend Developers

1. **Review:** `API-TESTING-SUMMARY.md`
   - See which endpoints have issues
   - Check action items

2. **Fix Issues:**
   - WarehouseService routing bug
   - OrderService validation
   - UserService create endpoint

3. **Test:** Run `test-api-endpoints.ps1`
   - Verify fixes work
   - Ensure no regressions

---

## 📊 Test Results Summary

**Last Run:** 2024-11-18

| Service | Endpoints Tested | Working | Issues |
|---------|-----------------|---------|--------|
| AuthService | 4 | ✅ 4 | - |
| BookService | 5 | ✅ 5 | - |
| SearchService | 6 | ✅ 6 | - |
| WarehouseService | 4 | ⚠️ 3 | 1 routing bug |
| UserService | 2 | ⚠️ 1 | 1 validation issue |
| OrderService | 1 | ❌ 0 | 1 validation issue |
| **Total** | **22** | **19** | **3** |

**Success Rate:** 86% (19/22)

---

## 🔧 Known Issues

### 1. WarehouseService - Get Items by ISBN
- **Endpoint:** `GET /warehouse/items/id/{isbn}`
- **Status:** ❌ 400 Bad Request
- **Fix:** Change route parameter from `{id}` to `{bookIsbn}`

### 2. UserService - Create User
- **Endpoint:** `POST /users`
- **Status:** ❌ 400 Bad Request
- **Workaround:** Use `POST /auth/register` instead

### 3. OrderService - Create Order
- **Endpoint:** `POST /orders`
- **Status:** ❌ 400 Bad Request
- **Needs:** Investigation of validation requirements

---

## 🚀 API Gateway Routes

All requests go through: `http://localhost:5004`

| Service | Route Prefix | Example |
|---------|-------------|---------|
| AuthService | `/auth` | `/auth/login` |
| BookService | `/books` | `/books/0195153448` |
| SearchService | `/search` | `/search?query=classical` |
| WarehouseService | `/warehouse` | `/warehouse/items` |
| UserService | `/users` | `/users/{id}` |
| OrderService | `/orders` | `/orders/{id}` |

---

## 🔐 Authentication

### Getting a Token

```powershell
# Register
$body = @{
    username = "myuser"
    email = "user@example.com"
    password = "Pass123!"
    role = "Customer"
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri 'http://localhost:5004/auth/register' `
    -Method POST -Body $body -ContentType 'application/json'

$token = ($response.Content | ConvertFrom-Json).accessToken
```

### Using the Token

```powershell
$headers = @{
    'Authorization' = "Bearer $token"
}

$response = Invoke-WebRequest -Uri 'http://localhost:5004/warehouse/items' `
    -Method GET -Headers $headers
```

---

## 📈 Database Statistics

- **Books:** 266,415 books
- **Warehouse Items:** 106,000+ items
- **Services:** 6 microservices
- **Infrastructure:** SQL Server, Redis, RabbitMQ

---

## 🎓 Architecture Overview

```
Client/UI
    ↓
ApiGateway (localhost:5004)
    ↓
├── AuthService (localhost:5006)
├── BookService (localhost:5000)
├── SearchService (localhost:5002)
├── WarehouseService (localhost:5001)
├── UserService (localhost:5005)
└── OrderService (localhost:3000)
    ↓
├── SQL Server (localhost:1433)
├── Redis (localhost:6379)
└── RabbitMQ (localhost:5672)
```

---

## 📝 Recommendations

### Start with These Working Endpoints

1. **Authentication**
   - `POST /auth/register`
   - `POST /auth/login`

2. **Browse Books**
   - `GET /search/available?page=1&pageSize=20`
   - `GET /search/featured`

3. **Search Books**
   - `GET /search?query=harry+potter`

4. **Book Details**
   - `GET /books/{isbn}`
   - `GET /search/by-isbn/{isbn}`

5. **View Sellers**
   - `GET /search/sellers/{isbn}`

### Wait for Fixes

- Order creation (needs backend fix)
- Direct warehouse item lookup by ISBN (needs backend fix)

---

## 🔄 Continuous Testing

Run the test script after:
- Deploying new code
- Making API changes
- Before releases
- When debugging issues

```powershell
.\test-api-endpoints.ps1
```

---

## 📞 Support

For issues or questions:
1. Check `API-DOCUMENTATION.md` for endpoint details
2. Review `API-TESTING-SUMMARY.md` for known issues
3. Run `test-api-endpoints.ps1` to verify service status
4. Check service logs: `docker-compose logs <service-name>`

---

**Last Updated:** 2024-11-18  
**Documentation Version:** 1.0  
**Test Coverage:** 86% (19/22 endpoints)

