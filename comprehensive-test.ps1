# Comprehensive test script
$ErrorActionPreference = "Continue"
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "COMPREHENSIVE USER REGISTRATION TEST" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Check services status
Write-Host "1. Checking services status..." -ForegroundColor Yellow
$services = @("sqlserver", "rabbitmq", "authservice", "userservice")
$allRunning = $true
foreach ($service in $services) {
    $container = docker ps --filter "name=$service" --format "{{.Names}}" 2>&1
    if ($container -match $service) {
        Write-Host "   ✓ $service is running" -ForegroundColor Green
    } else {
        Write-Host "   ✗ $service is NOT running" -ForegroundColor Red
        $allRunning = $false
    }
}

if (-not $allRunning) {
    Write-Host "`n⚠ Some services are not running. Starting them..." -ForegroundColor Yellow
    docker-compose up -d 2>&1 | Out-Null
    Start-Sleep -Seconds 20
}

# 2. Test health endpoints
Write-Host "`n2. Testing health endpoints..." -ForegroundColor Yellow
try {
    $authHealth = Invoke-WebRequest -Uri "http://localhost:5006/health" -UseBasicParsing -TimeoutSec 5
    Write-Host "   ✓ AuthService health: $($authHealth.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ AuthService health check failed" -ForegroundColor Red
}

try {
    $userHealth = Invoke-WebRequest -Uri "http://localhost:5005/health" -UseBasicParsing -TimeoutSec 5
    Write-Host "   ✓ UserService health: $($userHealth.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "   ✗ UserService health check failed" -ForegroundColor Red
}

# 3. Check RabbitMQ connection
Write-Host "`n3. Checking RabbitMQ connections..." -ForegroundColor Yellow
$authLogs = docker-compose logs authservice --tail 50 2>&1 | Select-String -Pattern "Successfully connected to RabbitMQ|RabbitMQ not connected"
if ($authLogs -match "Successfully connected") {
    Write-Host "   ✓ AuthService connected to RabbitMQ" -ForegroundColor Green
} else {
    Write-Host "   ⚠ AuthService RabbitMQ connection status unknown (will connect on first use)" -ForegroundColor Yellow
}

$userLogs = docker-compose logs userservice --tail 50 2>&1 | Select-String -Pattern "RabbitMQ consumer started|Listening for user events"
if ($userLogs -match "RabbitMQ consumer started|Listening") {
    Write-Host "   ✓ UserService RabbitMQ consumer is running" -ForegroundColor Green
} else {
    Write-Host "   ⚠ UserService RabbitMQ consumer status unknown" -ForegroundColor Yellow
}

# 4. Test user registration
Write-Host "`n4. Testing user registration..." -ForegroundColor Yellow
$testEmail = "test$(Get-Random)@example.com"
$testPassword = "TestPassword123!"
$registerBody = @{
    email = $testEmail
    password = $testPassword
} | ConvertTo-Json

$registrationSuccess = $false
$userId = $null
$accessToken = $null

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5006/register" -Method POST -Body $registerBody -ContentType "application/json" -TimeoutSec 10
    $registrationSuccess = $true
    $userId = $response.userId
    $accessToken = $response.accessToken
    Write-Host "   ✓ Registration successful" -ForegroundColor Green
    Write-Host "     UserId: $userId" -ForegroundColor Cyan
    Write-Host "     Email: $testEmail" -ForegroundColor Cyan
} catch {
    Write-Host "   ✗ Registration failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host "     Error: $errorBody" -ForegroundColor Red
    }
}

# 5. Check if event was published
Write-Host "`n5. Checking if UserCreated event was published..." -ForegroundColor Yellow
Start-Sleep -Seconds 2
$eventLogs = docker-compose logs authservice --tail 30 2>&1 | Select-String -Pattern "UserCreated event published|Message published to RabbitMQ.*UserCreated|Attempting to connect"
if ($eventLogs -match "UserCreated event published|Message published.*UserCreated") {
    Write-Host "   ✓ UserCreated event was published" -ForegroundColor Green
} elseif ($eventLogs -match "Attempting to connect") {
    Write-Host "   ⚠ RabbitMQ connection attempt detected (lazy initialization)" -ForegroundColor Yellow
} else {
    Write-Host "   ✗ UserCreated event was NOT published" -ForegroundColor Red
    Write-Host "     Checking connection status..." -ForegroundColor Yellow
    $connLogs = docker-compose logs authservice --tail 20 2>&1 | Select-String -Pattern "RabbitMQ not connected|Successfully connected"
    Write-Host "     $connLogs" -ForegroundColor Yellow
}

# 6. Wait for event processing
Write-Host "`n6. Waiting for event processing..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# 7. Check if user exists in UserService
Write-Host "`n7. Checking if user exists in UserService..." -ForegroundColor Yellow
if ($userId -and $accessToken) {
    try {
        $headers = @{
            "Authorization" = "Bearer $accessToken"
        }
        $userResponse = Invoke-RestMethod -Uri "http://localhost:5005/api/users/$userId" -Method GET -Headers $headers -TimeoutSec 5
        Write-Host "   ✓ User found in UserService" -ForegroundColor Green
        Write-Host "     Email: $($userResponse.email)" -ForegroundColor Cyan
        Write-Host "     Name: $($userResponse.name)" -ForegroundColor Cyan
        Write-Host "     Role: $($userResponse.role)" -ForegroundColor Cyan
    } catch {
        Write-Host "   ✗ User NOT found in UserService" -ForegroundColor Red
        Write-Host "     Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response.StatusCode -eq 401) {
            Write-Host "     Status: 401 Unauthorized - User not found" -ForegroundColor Red
        }
        
        # Check event processing logs
        Write-Host "`n     Checking event processing logs..." -ForegroundColor Yellow
        $processLogs = docker-compose logs userservice --tail 30 2>&1 | Select-String -Pattern "Processing UserCreated|User profile created|Received message|Error processing"
        if ($processLogs) {
            Write-Host "     Event logs:" -ForegroundColor Yellow
            $processLogs | ForEach-Object { Write-Host "       $_" -ForegroundColor Yellow }
        } else {
            Write-Host "     No event processing logs found" -ForegroundColor Red
        }
    }
} else {
    Write-Host "   ⚠ Skipping - no user ID available" -ForegroundColor Yellow
}

# 8. Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST SUMMARY" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

if ($registrationSuccess -and $userId) {
    Write-Host "✓ User registration: SUCCESS" -ForegroundColor Green
} else {
    Write-Host "✗ User registration: FAILED" -ForegroundColor Red
}

if ($userId -and $accessToken) {
    try {
        $headers = @{ "Authorization" = "Bearer $accessToken" }
        $userResponse = Invoke-RestMethod -Uri "http://localhost:5005/api/users/$userId" -Method GET -Headers $headers -TimeoutSec 5 -ErrorAction Stop
        Write-Host "✓ User in UserService: SUCCESS" -ForegroundColor Green
        Write-Host "`n🎉 ALL TESTS PASSED!" -ForegroundColor Green
    } catch {
        Write-Host "✗ User in UserService: FAILED" -ForegroundColor Red
        Write-Host "`n⚠ SOME TESTS FAILED - Check logs above" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠ Cannot verify UserService - registration failed" -ForegroundColor Yellow
}

Write-Host ""





