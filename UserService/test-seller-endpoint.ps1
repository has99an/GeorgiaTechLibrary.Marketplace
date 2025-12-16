# Test script for Seller Profile endpoint
# This script tests the GET /api/sellers/{sellerId}/profile endpoint

$baseUrl = "http://localhost:5005"
$testSellerId = "ef7e4404-b79a-4914-ae09-560977e5f525" # First seller from CSV

Write-Host "Testing Seller Profile Endpoint..." -ForegroundColor Cyan
Write-Host "Base URL: $baseUrl" -ForegroundColor Gray
Write-Host "Seller ID: $testSellerId" -ForegroundColor Gray
Write-Host ""

# Test 1: Get seller profile
Write-Host "Test 1: GET /api/sellers/$testSellerId/profile" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/sellers/$testSellerId/profile" -Method Get -ContentType "application/json"
    Write-Host "✓ Success!" -ForegroundColor Green
    Write-Host "Response:" -ForegroundColor Gray
    $response | ConvertTo-Json -Depth 10
} catch {
    Write-Host "✗ Failed!" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Test completed." -ForegroundColor Cyan




