# API Endpoint Testing Script for GeorgiaTechLibrary.Marketplace
# Tests all endpoints through ApiGateway (localhost:5004)

$ErrorActionPreference = "Continue"
$results = @()

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [hashtable]$Headers = @{},
        [bool]$RequiresAuth = $false
    )
    
    Write-Host "`n=== Testing: $Name ===" -ForegroundColor Cyan
    Write-Host "$Method $Url" -ForegroundColor Gray
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
        }
        
        if ($Body) {
            $params['Body'] = ($Body | ConvertTo-Json -Depth 10)
            $params['ContentType'] = 'application/json'
        }
        
        $response = Invoke-WebRequest @params
        
        $result = @{
            Name = $Name
            Method = $Method
            Url = $Url
            Status = $response.StatusCode
            RequiresAuth = $RequiresAuth
            Success = $true
            Response = $response.Content
        }
        
        Write-Host "[OK] Status: $($response.StatusCode)" -ForegroundColor Green
        
        return $result
    }
    catch {
        $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "N/A" }
        
        $result = @{
            Name = $Name
            Method = $Method
            Url = $Url
            Status = $statusCode
            RequiresAuth = $RequiresAuth
            Success = $false
            Error = $_.Exception.Message
        }
        
        Write-Host "[FAIL] Error: $($_.Exception.Message)" -ForegroundColor Red
        
        return $result
    }
}

# Start testing
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "API ENDPOINT TESTING - GeorgiaTechLibrary.Marketplace" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

$baseUrl = "http://localhost:5004"

# ===== AUTHSERVICE ENDPOINTS =====
Write-Host "`n### AUTHSERVICE ENDPOINTS ###" -ForegroundColor Magenta

# Register
$registerBody = @{
    username = "testuser_$(Get-Random)"
    email = "test_$(Get-Random)@example.com"
    password = "Test123!"
    role = "Customer"
}
$result = Test-Endpoint -Name "Auth - Register" -Method "POST" -Url "$baseUrl/auth/register" -Body $registerBody
$results += $result

# Login and get token
$loginBody = @{
    email = $registerBody.email
    password = $registerBody.password
}
$result = Test-Endpoint -Name "Auth - Login" -Method "POST" -Url "$baseUrl/auth/login" -Body $loginBody
$results += $result

if ($result.Success) {
    $authData = $result.Response | ConvertFrom-Json
    $token = $authData.accessToken
    Write-Host "Token obtained: $($token.Substring(0,30))..." -ForegroundColor Green
    
    # Validate token
    $validateBody = @{ Token = $token }
    $result = Test-Endpoint -Name "Auth - Validate Token" -Method "POST" -Url "$baseUrl/auth/validate" -Body $validateBody
    $results += $result
    
    # Refresh token
    $refreshBody = @{ RefreshToken = $authData.refreshToken }
    $result = Test-Endpoint -Name "Auth - Refresh Token" -Method "POST" -Url "$baseUrl/auth/refresh" -Body $refreshBody
    $results += $result
}

# ===== BOOKSERVICE ENDPOINTS =====
Write-Host "`n### BOOKSERVICE ENDPOINTS ###" -ForegroundColor Magenta

# GET all books (public)
$result = Test-Endpoint -Name "Books - Get All (paginated)" -Method "GET" -Url "$baseUrl/books?pageSize=5"
$results += $result

# GET specific book (public)
$result = Test-Endpoint -Name "Books - Get by ISBN" -Method "GET" -Url "$baseUrl/books/0195153448"
$results += $result

# POST new book (requires auth)
if ($token) {
    $newBook = @{
        isbn = "9999999999999"
        bookTitle = "Test Book API"
        bookAuthor = "Test Author"
        yearOfPublication = 2024
        publisher = "Test Publisher"
        genre = "Fiction"
        language = "English"
        pageCount = 300
        description = "Test book for API testing"
        rating = 4.5
        availabilityStatus = "Available"
        edition = "1st"
        format = "Paperback"
    }
    $headers = @{ 'Authorization' = "Bearer $token" }
    $result = Test-Endpoint -Name "Books - Create New" -Method "POST" -Url "$baseUrl/books" -Body $newBook -Headers $headers -RequiresAuth $true
    $results += $result
    
    # PUT update book (requires auth)
    $updateBook = $newBook.Clone()
    $updateBook.bookTitle = "Updated Test Book"
    $result = Test-Endpoint -Name "Books - Update" -Method "PUT" -Url "$baseUrl/books/9999999999999" -Body $updateBook -Headers $headers -RequiresAuth $true
    $results += $result
    
    # DELETE book (requires auth)
    $result = Test-Endpoint -Name "Books - Delete" -Method "DELETE" -Url "$baseUrl/books/9999999999999" -Headers $headers -RequiresAuth $true
    $results += $result
}

# ===== SEARCHSERVICE ENDPOINTS =====
Write-Host "`n### SEARCHSERVICE ENDPOINTS ###" -ForegroundColor Magenta

# Search books
$result = Test-Endpoint -Name "Search - Query Books" -Method "GET" -Url "$baseUrl/search?query=classical"
$results += $result

# Get available books
$result = Test-Endpoint -Name "Search - Available Books" -Method "GET" -Url "$baseUrl/search/available?page=1&pageSize=10"
$results += $result

# Get featured books
$result = Test-Endpoint -Name "Search - Featured Books" -Method "GET" -Url "$baseUrl/search/featured"
$results += $result

# Get book by ISBN
$result = Test-Endpoint -Name "Search - Book by ISBN" -Method "GET" -Url "$baseUrl/search/by-isbn/0195153448"
$results += $result

# Get sellers for book
$result = Test-Endpoint -Name "Search - Book Sellers" -Method "GET" -Url "$baseUrl/search/sellers/0195153448"
$results += $result

# Get search stats
$result = Test-Endpoint -Name "Search - Statistics" -Method "GET" -Url "$baseUrl/search/stats"
$results += $result

# ===== WAREHOUSESERVICE ENDPOINTS =====
Write-Host "`n### WAREHOUSESERVICE ENDPOINTS ###" -ForegroundColor Magenta

if ($token) {
    $headers = @{ 'Authorization' = "Bearer $token" }
    
    # Get all items
    $result = Test-Endpoint -Name "Warehouse - Get All Items" -Method "GET" -Url "$baseUrl/warehouse/items?pageSize=10" -Headers $headers -RequiresAuth $true
    $results += $result
    
    # Get items by ISBN
    $result = Test-Endpoint -Name "Warehouse - Items by ISBN" -Method "GET" -Url "$baseUrl/warehouse/items/id/0195153448" -Headers $headers -RequiresAuth $true
    $results += $result
    
    # Get new items
    $result = Test-Endpoint -Name "Warehouse - New Items" -Method "GET" -Url "$baseUrl/warehouse/items/new" -Headers $headers -RequiresAuth $true
    $results += $result
    
    # Get used items
    $result = Test-Endpoint -Name "Warehouse - Used Items" -Method "GET" -Url "$baseUrl/warehouse/items/used" -Headers $headers -RequiresAuth $true
    $results += $result
}

# ===== USERSERVICE ENDPOINTS =====
Write-Host "`n### USERSERVICE ENDPOINTS ###" -ForegroundColor Magenta

if ($token) {
    $headers = @{ 'Authorization' = "Bearer $token" }
    
    # Get all users
    $result = Test-Endpoint -Name "Users - Get All" -Method "GET" -Url "$baseUrl/users" -Headers $headers -RequiresAuth $true
    $results += $result
    
    # Create user
    $newUser = @{
        username = "apiuser_$(Get-Random)"
        email = "apiuser_$(Get-Random)@example.com"
        role = "Customer"
    }
    $result = Test-Endpoint -Name "Users - Create" -Method "POST" -Url "$baseUrl/users" -Body $newUser -Headers $headers -RequiresAuth $true
    $results += $result
    
    if ($result.Success) {
        $createdUser = $result.Response | ConvertFrom-Json
        $userId = $createdUser.userId
        
        # Get specific user
        $result = Test-Endpoint -Name "Users - Get by ID" -Method "GET" -Url "$baseUrl/users/$userId" -Headers $headers -RequiresAuth $true
        $results += $result
        
        # Update user
        $updateUser = @{
            userId = $userId
            username = "updated_user"
            email = $newUser.email
            role = "Customer"
        }
        $result = Test-Endpoint -Name "Users - Update" -Method "PUT" -Url "$baseUrl/users/$userId" -Body $updateUser -Headers $headers -RequiresAuth $true
        $results += $result
        
        # Delete user
        $result = Test-Endpoint -Name "Users - Delete" -Method "DELETE" -Url "$baseUrl/users/$userId" -Headers $headers -RequiresAuth $true
        $results += $result
    }
}

# ===== ORDERSERVICE ENDPOINTS =====
Write-Host "`n### ORDERSERVICE ENDPOINTS ###" -ForegroundColor Magenta

if ($token) {
    $headers = @{ 'Authorization' = "Bearer $token" }
    
    # Create order
    $newOrder = @{
        userId = "00000000-0000-0000-0000-000000000001"
        items = @(
            @{
                warehouseItemId = "00000000-0000-0000-0000-000000000001"
                quantity = 1
                price = 29.99
            }
        )
    }
    $result = Test-Endpoint -Name "Orders - Create" -Method "POST" -Url "$baseUrl/orders" -Body $newOrder -Headers $headers -RequiresAuth $true
    $results += $result
    
    if ($result.Success) {
        $createdOrder = $result.Response | ConvertFrom-Json
        $orderId = $createdOrder.orderId
        
        # Get order by ID
        $result = Test-Endpoint -Name "Orders - Get by ID" -Method "GET" -Url "$baseUrl/orders/$orderId" -Headers $headers -RequiresAuth $true
        $results += $result
        
        # Pay for order
        $result = Test-Endpoint -Name "Orders - Pay" -Method "POST" -Url "$baseUrl/orders/$orderId/pay" -Headers $headers -RequiresAuth $true
        $results += $result
    }
}

# ===== GENERATE SUMMARY =====
Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "TEST SUMMARY" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

$totalTests = $results.Count
$successfulTests = ($results | Where-Object { $_.Success }).Count
$failedTests = $totalTests - $successfulTests

Write-Host "`nTotal Tests: $totalTests" -ForegroundColor White
Write-Host "Successful: $successfulTests" -ForegroundColor Green
Write-Host "Failed: $failedTests" -ForegroundColor Red

# Group by service
Write-Host "`n### Results by Service ###" -ForegroundColor Cyan
$results | Group-Object { $_.Name.Split(' - ')[0] } | ForEach-Object {
    Write-Host "`n$($_.Name):" -ForegroundColor Magenta
    $_.Group | ForEach-Object {
        $status = if ($_.Success) { "[OK]" } else { "[FAIL]" }
        $color = if ($_.Success) { "Green" } else { "Red" }
        $authLabel = if ($_.RequiresAuth) { "[AUTH]" } else { "[PUBLIC]" }
        Write-Host "  $status $($_.Name) - $($_.Method) - Status: $($_.Status) $authLabel" -ForegroundColor $color
    }
}

# Save results to JSON
$results | ConvertTo-Json -Depth 10 | Out-File "api-test-results.json"
Write-Host "`n[OK] Results saved to api-test-results.json" -ForegroundColor Green

