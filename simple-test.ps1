Write-Host "=== SIMPLE TEST ===" -ForegroundColor Cyan

# Test 1: Register user
Write-Host "`n1. Registering user..." -ForegroundColor Yellow
$email = "test$(Get-Random)@test.com"
$body = @{email=$email;password="Test123!"} | ConvertTo-Json
try {
    $reg = Invoke-RestMethod -Uri "http://localhost:5006/register" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 10
    Write-Host "   SUCCESS: UserId=$($reg.userId)" -ForegroundColor Green
    $script:userId = $reg.userId
    $script:token = $reg.accessToken
} catch {
    Write-Host "   FAILED: $_" -ForegroundColor Red
    exit
}

# Test 2: Wait and check UserService
Write-Host "`n2. Waiting 5 seconds for event processing..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

Write-Host "`n3. Checking UserService..." -ForegroundColor Yellow
try {
    $headers = @{Authorization="Bearer $($script:token)"}
    $user = Invoke-RestMethod -Uri "http://localhost:5005/api/users/$($script:userId)" -Method GET -Headers $headers -TimeoutSec 5
    Write-Host "   SUCCESS: User found - Email=$($user.email)" -ForegroundColor Green
    Write-Host "`n=== ALL TESTS PASSED ===" -ForegroundColor Green
} catch {
    Write-Host "   FAILED: User not found - $_" -ForegroundColor Red
    Write-Host "`n=== TEST FAILED ===" -ForegroundColor Red
    
    # Check logs
    Write-Host "`nChecking AuthService logs..." -ForegroundColor Yellow
    docker logs georgiatechlibrarymarketplace-authservice-1 --tail 30 2>&1 | Select-String -Pattern "UserCreated|RabbitMQ|Message published" | Select-Object -Last 5
    
    Write-Host "`nChecking UserService logs..." -ForegroundColor Yellow
    docker logs georgiatechlibrarymarketplace-userservice-1 --tail 30 2>&1 | Select-String -Pattern "UserCreated|Processing|Received" | Select-Object -Last 5
}







