# Test checkout endpoint
$ErrorActionPreference = "Continue"
Write-Host "=== TESTING CHECKOUT ENDPOINT ===" -ForegroundColor Cyan
Write-Host ""

# Get a test user ID (you'll need to replace this with an actual user ID)
$customerId = "3cc9d04d-4b97-4ffb-b222-a4ceba2d243a"

# First, get the cart to see what's in it
Write-Host "1. Getting cart for customer: $customerId" -ForegroundColor Yellow
try {
    $cartResponse = Invoke-RestMethod -Uri "http://localhost:5004/cart/$customerId" -Method GET -Headers @{
        "Authorization" = "Bearer YOUR_TOKEN_HERE"
    } -ErrorAction Stop
    Write-Host "   Cart retrieved successfully" -ForegroundColor Green
    Write-Host "   Total: $($cartResponse.totalAmount)" -ForegroundColor Cyan
    Write-Host "   Items: $($cartResponse.itemCount)" -ForegroundColor Cyan
    
    $cartTotal = $cartResponse.totalAmount
} catch {
    Write-Host "   Failed to get cart: $($_.Exception.Message)" -ForegroundColor Red
    $cartTotal = 100.00  # Default for testing
}

# Create checkout request
Write-Host "`n2. Creating checkout request..." -ForegroundColor Yellow
$checkoutBody = @{
    deliveryAddress = @{
        street = "Test Street 123"
        city = "Copenhagen"
        postalCode = "2100"
        state = "Capital Region"
        country = "Denmark"
    }
    amount = $cartTotal
    paymentMethod = "card"
} | ConvertTo-Json -Depth 10

Write-Host "   Request body:" -ForegroundColor Cyan
Write-Host $checkoutBody -ForegroundColor Gray

# Send checkout request
Write-Host "`n3. Sending checkout request..." -ForegroundColor Yellow
try {
    $checkoutResponse = Invoke-RestMethod -Uri "http://localhost:5004/cart/$customerId/checkout" `
        -Method POST `
        -Body $checkoutBody `
        -ContentType "application/json" `
        -Headers @{
            "Authorization" = "Bearer YOUR_TOKEN_HERE"
        } `
        -ErrorAction Stop
    
    Write-Host "   SUCCESS: Checkout completed!" -ForegroundColor Green
    Write-Host "   OrderId: $($checkoutResponse.orderId)" -ForegroundColor Cyan
} catch {
    Write-Host "   FAILED: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host "   Error details:" -ForegroundColor Red
        Write-Host $errorBody -ForegroundColor Yellow
        
        # Try to parse as JSON
        try {
            $errorJson = $errorBody | ConvertFrom-Json
            if ($errorJson.errors) {
                Write-Host "`n   Validation errors:" -ForegroundColor Red
                $errorJson.errors.PSObject.Properties | ForEach-Object {
                    Write-Host "     $($_.Name): $($_.Value -join ', ')" -ForegroundColor Yellow
                }
            }
        } catch {
            # Not JSON, just show raw
        }
    }
}

Write-Host "`n=== TEST COMPLETED ===" -ForegroundColor Cyan







