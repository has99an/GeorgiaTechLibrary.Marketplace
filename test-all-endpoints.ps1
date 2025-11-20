# Comprehensive API Endpoint Testing Script
# Tests all endpoints through API Gateway (localhost:5004)

$ErrorActionPreference = "Continue"
$baseUrl = "http://localhost:5004"
$results = @()

function Test-Endpoint {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [hashtable]$Headers = @{}
    )
    
    $url = "$baseUrl$Path"
    $result = @{
        Method = $Method
        Path = $Path
        Status = "Failed"
        StatusCode = 0
        Response = $null
        Error = $null
    }
    
    try {
        $params = @{
            Uri = $url
            Method = $Method
            Headers = $Headers
            ContentType = "application/json"
            ErrorAction = "Stop"
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        
        $response = Invoke-RestMethod @params
        $result.Status = "Success"
        $result.StatusCode = 200
        $result.Response = $response
    }
    catch {
        $result.Error = $_.Exception.Message
        if ($_.Exception.Response) {
            $result.StatusCode = [int]$_.Exception.Response.StatusCode
        }
    }
    
    $script:results += $result
    Write-Host "$Method $Path - $($result.Status) ($($result.StatusCode))" -ForegroundColor $(if ($result.Status -eq "Success") { "Green" } else { "Red" })
    return $result
}

Write-Host "`n=== PHASE 3: END-TO-END API TESTING ===" -ForegroundColor Cyan
Write-Host "Testing all endpoints through API Gateway at $baseUrl`n" -ForegroundColor Yellow

# AUTHENTICATION FLOW
Write-Host "`n--- AUTHENTICATION FLOW ---" -ForegroundColor Magenta

# Register a new user
$registerBody = @{
    email = "testuser@gatech.edu"
    password = "TestPassword123!"
    firstName = "Test"
    lastName = "User"
}
$registerResult = Test-Endpoint -Method "POST" -Path "/auth/register" -Body $registerBody

# Login
$loginBody = @{
    email = "testuser@gatech.edu"
    password = "TestPassword123!"
}
$loginResult = Test-Endpoint -Method "POST" -Path "/auth/login" -Body $loginBody
$token = $null
if ($loginResult.Status -eq "Success" -and $loginResult.Response.token) {
    $token = $loginResult.Response.token
    Write-Host "✓ Login successful, token obtained" -ForegroundColor Green
}

# Validate token
if ($token) {
    $validateHeaders = @{ "Authorization" = "Bearer $token" }
    Test-Endpoint -Method "POST" -Path "/auth/validate" -Headers $validateHeaders
}

# Refresh token
if ($token -and $loginResult.Response.refreshToken) {
    $refreshBody = @{
        refreshToken = $loginResult.Response.refreshToken
    }
    Test-Endpoint -Method "POST" -Path "/auth/refresh" -Body $refreshBody
}

# PUBLIC SEARCH & BROWSING
Write-Host "`n--- PUBLIC SEARCH AND BROWSING ---" -ForegroundColor Magenta

Test-Endpoint -Method "GET" -Path "/search?query=test"
Test-Endpoint -Method "GET" -Path "/search/available"
Test-Endpoint -Method "GET" -Path "/search/autocomplete?prefix=te"
Test-Endpoint -Method "GET" -Path "/search/facets"
Test-Endpoint -Method "GET" -Path "/search/stats"
Test-Endpoint -Method "GET" -Path "/search/popular"
Test-Endpoint -Method "GET" -Path "/books"
Test-Endpoint -Method "GET" -Path "/warehouse/items"

# Get a book ISBN for testing
$booksResult = Test-Endpoint -Method "GET" -Path "/books"
if ($booksResult.Status -eq "Success" -and $booksResult.Response -and $booksResult.Response.Count -gt 0) {
    $testIsbn = $booksResult.Response[0].isbn
    Test-Endpoint -Method "GET" -Path "/books/$testIsbn"
}

# Advanced search
$advancedSearchBody = @{
    query = "test"
    filters = @{
        minPrice = 0
        maxPrice = 100
    }
    page = 1
    pageSize = 10
}
Test-Endpoint -Method "POST" -Path "/search/advanced" -Body $advancedSearchBody

# PROTECTED USER OPERATIONS (after login)
Write-Host "`n--- PROTECTED USER OPERATIONS ---" -ForegroundColor Magenta

if ($token) {
    $authHeaders = @{ "Authorization" = "Bearer $token" }
    
    # Get user profile (need userId from login response)
    if ($loginResult.Response.userId) {
        $userId = $loginResult.Response.userId
        Test-Endpoint -Method "GET" -Path "/users/$userId" -Headers $authHeaders
        Test-Endpoint -Method "GET" -Path "/users/$userId/preferences" -Headers $authHeaders
        
        # Update profile
        $updateBody = @{
            firstName = "Updated"
            lastName = "Name"
        }
        Test-Endpoint -Method "PUT" -Path "/users/$userId" -Body $updateBody -Headers $authHeaders
        
        # Cart operations
        if ($testIsbn) {
            $cartItemBody = @{
                isbn = $testIsbn
                quantity = 1
            }
            Test-Endpoint -Method "POST" -Path "/cart/$userId/items" -Body $cartItemBody -Headers $authHeaders
            Test-Endpoint -Method "GET" -Path "/cart/$userId" -Headers $authHeaders
        }
    }
}

# Summary
Write-Host "`n=== TEST SUMMARY ===" -ForegroundColor Cyan
$successCount = ($results | Where-Object { $_.Status -eq "Success" }).Count
$totalCount = $results.Count
$failureCount = $totalCount - $successCount

Write-Host "Total Tests: $totalCount" -ForegroundColor White
Write-Host "Successful: $successCount" -ForegroundColor Green
Write-Host "Failed: $failureCount" -ForegroundColor $(if ($failureCount -eq 0) { "Green" } else { "Red" })

if ($failureCount -gt 0) {
    Write-Host "`nFailed Tests:" -ForegroundColor Red
    $results | Where-Object { $_.Status -eq "Failed" } | ForEach-Object {
        Write-Host "  $($_.Method) $($_.Path) - $($_.Error)" -ForegroundColor Red
    }
}

# Export results
$results | ConvertTo-Json -Depth 10 | Out-File "test-results.json"
Write-Host "`nResults exported to test-results.json" -ForegroundColor Yellow

