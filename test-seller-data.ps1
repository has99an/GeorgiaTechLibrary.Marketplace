$ErrorActionPreference = "Stop"

Write-Host "=== Testing Seller Data Implementation ===" -ForegroundColor Cyan
Write-Host ""

# Wait for services to be ready
Write-Host "Waiting for services to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Test 1: Trigger sync events
Write-Host "`nTest 1: Triggering WarehouseService sync-events..." -ForegroundColor Yellow
try {
    $syncResponse = Invoke-WebRequest -Uri "http://localhost:5004/warehouse/api/warehouse/sync-events" -Method POST -UseBasicParsing
    Write-Host "Sync events triggered successfully. Synced items: $($syncResponse.Content)" -ForegroundColor Green
}
catch {
    Write-Host "Failed to trigger sync events: $($_.Exception.Message)" -ForegroundColor Red
}

# Wait for events to be processed
Write-Host "`nWaiting for events to be processed..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# Test 2: Get available books
Write-Host "`nTest 2: Getting available books from SearchService..." -ForegroundColor Yellow
try {
    $booksResponse = Invoke-WebRequest -Uri "http://localhost:5004/search/api/search/available?page=1&pageSize=5" -UseBasicParsing
    $booksData = $booksResponse.Content | ConvertFrom-Json
    
    Write-Host "API Response received successfully!" -ForegroundColor Green
    Write-Host "Total books returned: $($booksData.books.Count)" -ForegroundColor Cyan
    Write-Host "Total count: $($booksData.totalCount)" -ForegroundColor Cyan
    Write-Host "Page: $($booksData.page)" -ForegroundColor Cyan
    
    if ($booksData.books.Count -gt 0) {
        Write-Host "`nFirst book entry:" -ForegroundColor Yellow
        $firstBook = $booksData.books[0]
        Write-Host "  ISBN: $($firstBook.isbn)" -ForegroundColor White
        Write-Host "  Title: $($firstBook.title)" -ForegroundColor White
        Write-Host "  SellerId: $($firstBook.sellerId)" -ForegroundColor White
        Write-Host "  Price: $($firstBook.price)" -ForegroundColor White
        Write-Host "  Quantity: $($firstBook.quantity)" -ForegroundColor White
        Write-Host "  Condition: $($firstBook.condition)" -ForegroundColor White
        if ($firstBook.location) {
            Write-Host "  Location: $($firstBook.location)" -ForegroundColor White
        }
        
        # Check if sellerId exists
        if ([string]::IsNullOrWhiteSpace($firstBook.sellerId)) {
            Write-Host "`nWARNING: sellerId is missing or empty!" -ForegroundColor Red
        } else {
            Write-Host "`nSUCCESS: sellerId is present: $($firstBook.sellerId)" -ForegroundColor Green
        }
        
        # Check for multiple sellers with same ISBN
        $sameIsbn = $booksData.books | Where-Object { $_.isbn -eq $firstBook.isbn }
        if ($sameIsbn.Count -gt 1) {
            Write-Host "`nSUCCESS: Found $($sameIsbn.Count) entries for ISBN $($firstBook.isbn) (multiple sellers)" -ForegroundColor Green
            foreach ($entry in $sameIsbn) {
                Write-Host "  - SellerId: $($entry.sellerId), Price: $($entry.price), Quantity: $($entry.quantity)" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "`nWARNING: No books returned from API!" -ForegroundColor Red
        Write-Host "This may indicate:" -ForegroundColor Yellow
        Write-Host "  1. No sellers data in Redis" -ForegroundColor Yellow
        Write-Host "  2. Events not processed yet" -ForegroundColor Yellow
        Write-Host "  3. No warehouse items with quantity > 0" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "Failed to get available books: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host "Error response: $errorBody" -ForegroundColor Red
    }
}

# Test 3: Check specific book with sellers
Write-Host "`nTest 3: Checking sellers endpoint for a specific book..." -ForegroundColor Yellow
try {
    # First get a book ISBN
    $booksResponse = Invoke-WebRequest -Uri "http://localhost:5004/search/api/search/available?page=1&pageSize=1" -UseBasicParsing
    $booksData = $booksResponse.Content | ConvertFrom-Json
    
    if ($booksData.books.Count -gt 0) {
        $testIsbn = $booksData.books[0].isbn
        Write-Host "Testing with ISBN: $testIsbn" -ForegroundColor Cyan
        
        $sellersResponse = Invoke-WebRequest -Uri "http://localhost:5004/search/api/search/sellers/$testIsbn" -UseBasicParsing
        $sellersData = $sellersResponse.Content | ConvertFrom-Json
        
        Write-Host "Sellers endpoint returned $($sellersData.sellers.Count) sellers" -ForegroundColor Green
        foreach ($seller in $sellersData.sellers) {
            Write-Host "  - SellerId: $($seller.sellerId), Price: $($seller.price), Quantity: $($seller.quantity), Condition: $($seller.condition)" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host "Failed to get sellers: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan






