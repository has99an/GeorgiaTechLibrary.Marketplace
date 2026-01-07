# Script to seed BookService with 100+ test books for load testing
# BookService runs on port 5000

$baseUrl = "http://localhost:5000/api/books"
$booksCreated = 0
$booksFailed = 0

# Search terms that should appear in books for load testing
$searchTerms = @(
    "harry potter",
    "computer science",
    "mathematics",
    "physics",
    "engineering",
    "programming",
    "algorithms",
    "data structures"
)

# Sample data for generating varied books
$titles = @(
    "Harry Potter and the Philosopher's Stone",
    "Harry Potter and the Chamber of Secrets",
    "Introduction to Computer Science",
    "Advanced Computer Science Principles",
    "Mathematics for Engineers",
    "Applied Mathematics",
    "Physics Fundamentals",
    "Quantum Physics Explained",
    "Engineering Design Principles",
    "Software Engineering Best Practices",
    "Programming in C#",
    "Python Programming Guide",
    "Data Structures and Algorithms",
    "Algorithm Design Manual",
    "Database Systems",
    "Web Development Fundamentals",
    "Machine Learning Basics",
    "Artificial Intelligence",
    "Network Security",
    "Cloud Computing"
)

$authors = @(
    "J.K. Rowling",
    "John Smith",
    "Jane Doe",
    "Robert Johnson",
    "Emily Williams",
    "Michael Brown",
    "Sarah Davis",
    "David Miller",
    "Lisa Wilson",
    "James Moore"
)

$publishers = @(
    "Tech Books Publishing",
    "Academic Press",
    "Science Publishers",
    "Engineering Books Co",
    "Programming Press"
)

$genres = @(
    "Fiction",
    "Science Fiction",
    "Computer Science",
    "Mathematics",
    "Physics",
    "Engineering",
    "Programming",
    "Non-Fiction"
)

function Generate-ISBN {
    $isbn = "978" + (Get-Random -Minimum 1000000000 -Maximum 9999999999)
    return $isbn.ToString()
}

function Create-Book {
    param(
        [int]$Index
    )
    
    $isbn = Generate-ISBN
    $titleIndex = $Index % $titles.Count
    $authorIndex = $Index % $authors.Count
    $publisherIndex = $Index % $publishers.Count
    $genreIndex = $Index % $genres.Count
    
    # Ensure some books match search terms
    $title = $titles[$titleIndex]
    if ($Index -lt $searchTerms.Count) {
        $title = $titles[$titleIndex] + " - " + $searchTerms[$Index]
    }
    
    $book = @{
        isbn = $isbn
        bookTitle = $title
        bookAuthor = $authors[$authorIndex]
        yearOfPublication = (Get-Random -Minimum 2010 -Maximum 2025)
        publisher = $publishers[$publisherIndex]
        imageUrlS = "http://example.com/small_$Index.jpg"
        imageUrlM = "http://example.com/medium_$Index.jpg"
        imageUrlL = "http://example.com/large_$Index.jpg"
        genre = $genres[$genreIndex]
        language = "English"
        pageCount = (Get-Random -Minimum 200 -Maximum 800)
        description = "This is a test book description for $title. A comprehensive guide covering important topics."
        rating = [math]::Round((Get-Random -Minimum 3.0 -Maximum 5.0), 1)
        availabilityStatus = "Available"
        edition = "First Edition"
        format = if ((Get-Random) -gt 0.5) { "Paperback" } else { "Hardcover" }
    }
    
    return $book
}

Write-Host "Starting to seed BookService with test books..."
Write-Host "Target: 100+ books"
Write-Host ""

$batchSize = 10
$totalBooks = 100

for ($i = 0; $i -lt $totalBooks; $i++) {
    $book = Create-Book -Index $i
    
    try {
        $jsonBody = $book | ConvertTo-Json
        $response = Invoke-RestMethod -Uri $baseUrl -Method Post -Body $jsonBody -ContentType "application/json" -ErrorAction Stop
        
        $booksCreated++
        if ($booksCreated % 10 -eq 0) {
            Write-Host "Created $booksCreated books..." -ForegroundColor Green
        }
        
        # Small delay to avoid overwhelming the service
        Start-Sleep -Milliseconds 50
    }
    catch {
        $booksFailed++
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 409) {
            # Conflict - book already exists, try with new ISBN
            Write-Host "ISBN conflict for $($book.isbn), retrying..." -ForegroundColor Yellow
            $i-- # Retry this index
        }
        else {
            Write-Host "Failed to create book $($book.isbn): $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    # Batch delay for event processing
    if (($i + 1) % $batchSize -eq 0) {
        Write-Host "Batch completed, waiting for event processing..." -ForegroundColor Cyan
        Start-Sleep -Seconds 2
    }
}

Write-Host ""
Write-Host "Seeding completed!" -ForegroundColor Green
Write-Host "Books created: $booksCreated" -ForegroundColor Green
Write-Host "Books failed: $booksFailed" -ForegroundColor $(if ($booksFailed -gt 0) { "Yellow" } else { "Green" })
Write-Host ""
Write-Host "Waiting 10 seconds for events to be processed by SearchService..."
Start-Sleep -Seconds 10
Write-Host "Done!"

