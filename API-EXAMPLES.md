# API Usage Examples

Code examples for integrating with GeorgiaTechLibrary.Marketplace API

**Base URL:** `http://localhost:5004`

---

## Table of Contents
1. [JavaScript/TypeScript (React/Vue/Angular)](#javascripttypescript)
2. [C# (.NET)](#c-net)
3. [Python](#python)
4. [PowerShell](#powershell)
5. [cURL](#curl)

---

## JavaScript/TypeScript

### Setup API Client

```typescript
// api.ts
const API_BASE_URL = 'http://localhost:5004';

class ApiClient {
  private token: string | null = null;

  setToken(token: string) {
    this.token = token;
    localStorage.setItem('authToken', token);
  }

  getToken(): string | null {
    if (!this.token) {
      this.token = localStorage.getItem('authToken');
    }
    return this.token;
  }

  async request(endpoint: string, options: RequestInit = {}) {
    const url = `${API_BASE_URL}${endpoint}`;
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
      ...options.headers,
    };

    const token = this.getToken();
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(url, {
      ...options,
      headers,
    });

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    if (response.status === 204) {
      return null;
    }

    return response.json();
  }

  // Auth methods
  async register(username: string, email: string, password: string, role: string = 'Customer') {
    const data = await this.request('/auth/register', {
      method: 'POST',
      body: JSON.stringify({ username, email, password, role }),
    });
    this.setToken(data.accessToken);
    return data;
  }

  async login(email: string, password: string) {
    const data = await this.request('/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
    this.setToken(data.accessToken);
    return data;
  }

  // Book methods
  async getBooks(pageSize: number = 20, page: number = 1) {
    return this.request(`/books?pageSize=${pageSize}&page=${page}`);
  }

  async getBook(isbn: string) {
    return this.request(`/books/${isbn}`);
  }

  async createBook(book: any) {
    return this.request('/books', {
      method: 'POST',
      body: JSON.stringify(book),
    });
  }

  async updateBook(isbn: string, book: any) {
    return this.request(`/books/${isbn}`, {
      method: 'PUT',
      body: JSON.stringify(book),
    });
  }

  async deleteBook(isbn: string) {
    return this.request(`/books/${isbn}`, {
      method: 'DELETE',
    });
  }

  // Search methods
  async searchBooks(query: string) {
    return this.request(`/search?query=${encodeURIComponent(query)}`);
  }

  async getAvailableBooks(page: number = 1, pageSize: number = 20, sortBy?: string, sortOrder: string = 'asc') {
    let url = `/search/available?page=${page}&pageSize=${pageSize}&sortOrder=${sortOrder}`;
    if (sortBy) {
      url += `&sortBy=${sortBy}`;
    }
    return this.request(url);
  }

  async getFeaturedBooks() {
    return this.request('/search/featured');
  }

  async getBookSellers(isbn: string) {
    return this.request(`/search/sellers/${isbn}`);
  }

  // Warehouse methods
  async getWarehouseItems(pageSize: number = 20) {
    return this.request(`/warehouse/items?pageSize=${pageSize}`);
  }

  async getNewItems() {
    return this.request('/warehouse/items/new');
  }

  async getUsedItems() {
    return this.request('/warehouse/items/used');
  }

  // Order methods
  async createOrder(userId: string, items: any[]) {
    return this.request('/orders', {
      method: 'POST',
      body: JSON.stringify({ userId, items }),
    });
  }

  async getOrder(orderId: string) {
    return this.request(`/orders/${orderId}`);
  }

  async payOrder(orderId: string) {
    return this.request(`/orders/${orderId}/pay`, {
      method: 'POST',
    });
  }
}

export const api = new ApiClient();
```

### React Component Examples

```tsx
// LoginComponent.tsx
import React, { useState } from 'react';
import { api } from './api';

export const LoginComponent: React.FC = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const result = await api.login(email, password);
      console.log('Login successful:', result);
      // Redirect to dashboard
    } catch (err) {
      setError('Login failed. Please check your credentials.');
    }
  };

  return (
    <form onSubmit={handleLogin}>
      <input 
        type="email" 
        value={email} 
        onChange={(e) => setEmail(e.target.value)}
        placeholder="Email"
      />
      <input 
        type="password" 
        value={password} 
        onChange={(e) => setPassword(e.target.value)}
        placeholder="Password"
      />
      <button type="submit">Login</button>
      {error && <p className="error">{error}</p>}
    </form>
  );
};
```

```tsx
// BookListComponent.tsx
import React, { useEffect, useState } from 'react';
import { api } from './api';

interface Book {
  isbn: string;
  bookTitle: string;
  bookAuthor: string;
  price?: number;
}

export const BookListComponent: React.FC = () => {
  const [books, setBooks] = useState<Book[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    loadBooks();
  }, []);

  const loadBooks = async () => {
    try {
      const data = await api.getAvailableBooks(1, 20, 'price', 'asc');
      setBooks(data);
    } catch (err) {
      console.error('Failed to load books:', err);
    } finally {
      setLoading(false);
    }
  };

  if (loading) return <div>Loading...</div>;

  return (
    <div className="book-list">
      {books.map(book => (
        <div key={book.isbn} className="book-card">
          <h3>{book.bookTitle}</h3>
          <p>by {book.bookAuthor}</p>
          <p>ISBN: {book.isbn}</p>
        </div>
      ))}
    </div>
  );
};
```

```tsx
// SearchComponent.tsx
import React, { useState } from 'react';
import { api } from './api';

export const SearchComponent: React.FC = () => {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<any[]>([]);
  const [searching, setSearching] = useState(false);

  const handleSearch = async () => {
    if (!query.trim()) return;
    
    setSearching(true);
    try {
      const data = await api.searchBooks(query);
      setResults(data);
    } catch (err) {
      console.error('Search failed:', err);
    } finally {
      setSearching(false);
    }
  };

  return (
    <div className="search">
      <input 
        type="text"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        onKeyPress={(e) => e.key === 'Enter' && handleSearch()}
        placeholder="Search books..."
      />
      <button onClick={handleSearch} disabled={searching}>
        {searching ? 'Searching...' : 'Search'}
      </button>
      
      <div className="results">
        {results.map(book => (
          <div key={book.isbn}>
            <h4>{book.title}</h4>
            <p>{book.author}</p>
            <p>Available from {book.availableSellers} sellers</p>
            <p>Starting at ${book.minPrice}</p>
          </div>
        ))}
      </div>
    </div>
  );
};
```

---

## C# (.NET)

### API Client

```csharp
// ApiClient.cs
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private string? _token;

    public ApiClient(string baseUrl = "http://localhost:5004")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public void SetToken(string token)
    {
        _token = token;
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
    }

    // Auth methods
    public async Task<AuthResponse> RegisterAsync(string username, string email, 
        string password, string role = "Customer")
    {
        var request = new { username, email, password, role };
        var response = await PostAsync<AuthResponse>("/auth/register", request);
        SetToken(response.AccessToken);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var request = new { email, password };
        var response = await PostAsync<AuthResponse>("/auth/login", request);
        SetToken(response.AccessToken);
        return response;
    }

    // Book methods
    public async Task<List<Book>> GetBooksAsync(int pageSize = 20, int page = 1)
    {
        return await GetAsync<List<Book>>($"/books?pageSize={pageSize}&page={page}");
    }

    public async Task<Book> GetBookAsync(string isbn)
    {
        return await GetAsync<Book>($"/books/{isbn}");
    }

    public async Task<Book> CreateBookAsync(Book book)
    {
        return await PostAsync<Book>("/books", book);
    }

    public async Task<Book> UpdateBookAsync(string isbn, Book book)
    {
        return await PutAsync<Book>($"/books/{isbn}", book);
    }

    public async Task DeleteBookAsync(string isbn)
    {
        await DeleteAsync($"/books/{isbn}");
    }

    // Search methods
    public async Task<List<SearchResult>> SearchBooksAsync(string query)
    {
        return await GetAsync<List<SearchResult>>($"/search?query={Uri.EscapeDataString(query)}");
    }

    public async Task<List<SearchResult>> GetAvailableBooksAsync(
        int page = 1, int pageSize = 20, string? sortBy = null, string sortOrder = "asc")
    {
        var url = $"/search/available?page={page}&pageSize={pageSize}&sortOrder={sortOrder}";
        if (!string.IsNullOrEmpty(sortBy))
            url += $"&sortBy={sortBy}";
        return await GetAsync<List<SearchResult>>(url);
    }

    public async Task<List<SellerInfo>> GetBookSellersAsync(string isbn)
    {
        return await GetAsync<List<SellerInfo>>($"/search/sellers/{isbn}");
    }

    // Generic HTTP methods
    private async Task<T> GetAsync<T>(string endpoint)
    {
        var response = await _httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        })!;
    }

    private async Task<T> PostAsync<T>(string endpoint, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseJson, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        })!;
    }

    private async Task<T> PutAsync<T>(string endpoint, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _httpClient.PutAsync(endpoint, content);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseJson, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        })!;
    }

    private async Task DeleteAsync(string endpoint)
    {
        var response = await _httpClient.DeleteAsync(endpoint);
        response.EnsureSuccessStatusCode();
    }
}

// Models
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class Book
{
    public string ISBN { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public int YearOfPublication { get; set; }
    public string Publisher { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Rating { get; set; }
    public string AvailabilityStatus { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
}

public class SearchResult
{
    public string ISBN { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int TotalStock { get; set; }
    public int AvailableSellers { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
}

public class SellerInfo
{
    public string SellerId { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Condition { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public string Location { get; set; } = string.Empty;
}
```

### Usage Example

```csharp
// Program.cs
var api = new ApiClient();

// Login
var authResult = await api.LoginAsync("user@example.com", "Pass123!");
Console.WriteLine($"Logged in! Token expires in {authResult.ExpiresIn} seconds");

// Search books
var searchResults = await api.SearchBooksAsync("classical mythology");
foreach (var book in searchResults)
{
    Console.WriteLine($"{book.Title} by {book.Author}");
    Console.WriteLine($"  Available from {book.AvailableSellers} sellers");
    Console.WriteLine($"  Price range: ${book.MinPrice} - ${book.MaxPrice}");
}

// Get book details
var bookDetails = await api.GetBookAsync("0195153448");
Console.WriteLine($"Book: {bookDetails.BookTitle}");
Console.WriteLine($"Author: {bookDetails.BookAuthor}");
Console.WriteLine($"Rating: {bookDetails.Rating}/5");

// Get sellers
var sellers = await api.GetBookSellersAsync("0195153448");
foreach (var seller in sellers)
{
    Console.WriteLine($"{seller.SellerId}: ${seller.Price} ({seller.Condition})");
}
```

---

## Python

### API Client

```python
# api_client.py
import requests
from typing import Optional, Dict, List, Any

class ApiClient:
    def __init__(self, base_url: str = "http://localhost:5004"):
        self.base_url = base_url
        self.token: Optional[str] = None
        self.session = requests.Session()
    
    def set_token(self, token: str):
        self.token = token
        self.session.headers.update({
            'Authorization': f'Bearer {token}'
        })
    
    def _request(self, method: str, endpoint: str, **kwargs) -> Any:
        url = f"{self.base_url}{endpoint}"
        response = self.session.request(method, url, **kwargs)
        response.raise_for_status()
        
        if response.status_code == 204:
            return None
        
        return response.json()
    
    # Auth methods
    def register(self, username: str, email: str, password: str, role: str = "Customer") -> Dict:
        data = {
            "username": username,
            "email": email,
            "password": password,
            "role": role
        }
        result = self._request('POST', '/auth/register', json=data)
        self.set_token(result['accessToken'])
        return result
    
    def login(self, email: str, password: str) -> Dict:
        data = {"email": email, "password": password}
        result = self._request('POST', '/auth/login', json=data)
        self.set_token(result['accessToken'])
        return result
    
    # Book methods
    def get_books(self, page_size: int = 20, page: int = 1) -> List[Dict]:
        return self._request('GET', f'/books?pageSize={page_size}&page={page}')
    
    def get_book(self, isbn: str) -> Dict:
        return self._request('GET', f'/books/{isbn}')
    
    def create_book(self, book: Dict) -> Dict:
        return self._request('POST', '/books', json=book)
    
    def update_book(self, isbn: str, book: Dict) -> Dict:
        return self._request('PUT', f'/books/{isbn}', json=book)
    
    def delete_book(self, isbn: str) -> None:
        return self._request('DELETE', f'/books/{isbn}')
    
    # Search methods
    def search_books(self, query: str) -> List[Dict]:
        return self._request('GET', f'/search?query={query}')
    
    def get_available_books(self, page: int = 1, page_size: int = 20, 
                          sort_by: Optional[str] = None, sort_order: str = 'asc') -> List[Dict]:
        url = f'/search/available?page={page}&pageSize={page_size}&sortOrder={sort_order}'
        if sort_by:
            url += f'&sortBy={sort_by}'
        return self._request('GET', url)
    
    def get_featured_books(self) -> List[Dict]:
        return self._request('GET', '/search/featured')
    
    def get_book_sellers(self, isbn: str) -> List[Dict]:
        return self._request('GET', f'/search/sellers/{isbn}')
    
    # Warehouse methods
    def get_warehouse_items(self, page_size: int = 20) -> List[Dict]:
        return self._request('GET', f'/warehouse/items?pageSize={page_size}')
    
    def get_new_items(self) -> List[Dict]:
        return self._request('GET', '/warehouse/items/new')
    
    def get_used_items(self) -> List[Dict]:
        return self._request('GET', '/warehouse/items/used')
```

### Usage Example

```python
# main.py
from api_client import ApiClient

# Initialize client
api = ApiClient()

# Login
auth_result = api.login("user@example.com", "Pass123!")
print(f"Logged in! Token expires in {auth_result['expiresIn']} seconds")

# Search books
results = api.search_books("classical mythology")
for book in results:
    print(f"{book['title']} by {book['author']}")
    print(f"  Available from {book['availableSellers']} sellers")
    print(f"  Price: ${book['minPrice']} - ${book['maxPrice']}")

# Get book details
book = api.get_book("0195153448")
print(f"\nBook: {book['bookTitle']}")
print(f"Author: {book['bookAuthor']}")
print(f"Rating: {book['rating']}/5")

# Get sellers
sellers = api.get_book_sellers("0195153448")
for seller in sellers:
    print(f"{seller['sellerId']}: ${seller['price']} ({seller['condition']})")

# Get available books with pagination
books = api.get_available_books(page=1, page_size=10, sort_by='price', sort_order='asc')
print(f"\nFound {len(books)} available books")
```

---

## PowerShell

```powershell
# api-functions.ps1

# Set base URL
$script:BaseUrl = "http://localhost:5004"
$script:Token = $null

function Set-ApiToken {
    param([string]$Token)
    $script:Token = $Token
}

function Invoke-ApiRequest {
    param(
        [string]$Method,
        [string]$Endpoint,
        [object]$Body = $null
    )
    
    $uri = "$script:BaseUrl$Endpoint"
    $headers = @{
        'Content-Type' = 'application/json'
    }
    
    if ($script:Token) {
        $headers['Authorization'] = "Bearer $script:Token"
    }
    
    $params = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
    }
    
    if ($Body) {
        $params['Body'] = ($Body | ConvertTo-Json -Depth 10)
    }
    
    try {
        $response = Invoke-WebRequest @params
        if ($response.StatusCode -eq 204) {
            return $null
        }
        return $response.Content | ConvertFrom-Json
    }
    catch {
        Write-Error "API request failed: $_"
        throw
    }
}

# Auth functions
function Register-User {
    param(
        [string]$Username,
        [string]$Email,
        [string]$Password,
        [string]$Role = "Customer"
    )
    
    $body = @{
        username = $Username
        email = $Email
        password = $Password
        role = $Role
    }
    
    $result = Invoke-ApiRequest -Method POST -Endpoint "/auth/register" -Body $body
    Set-ApiToken -Token $result.accessToken
    return $result
}

function Login-User {
    param(
        [string]$Email,
        [string]$Password
    )
    
    $body = @{
        email = $Email
        password = $Password
    }
    
    $result = Invoke-ApiRequest -Method POST -Endpoint "/auth/login" -Body $body
    Set-ApiToken -Token $result.accessToken
    return $result
}

# Book functions
function Get-Books {
    param(
        [int]$PageSize = 20,
        [int]$Page = 1
    )
    
    return Invoke-ApiRequest -Method GET -Endpoint "/books?pageSize=$PageSize&page=$Page"
}

function Get-Book {
    param([string]$ISBN)
    
    return Invoke-ApiRequest -Method GET -Endpoint "/books/$ISBN"
}

function Search-Books {
    param([string]$Query)
    
    $encodedQuery = [System.Web.HttpUtility]::UrlEncode($Query)
    return Invoke-ApiRequest -Method GET -Endpoint "/search?query=$encodedQuery"
}

function Get-BookSellers {
    param([string]$ISBN)
    
    return Invoke-ApiRequest -Method GET -Endpoint "/search/sellers/$ISBN"
}

# Usage example
Login-User -Email "user@example.com" -Password "Pass123!"

$books = Search-Books -Query "classical"
$books | ForEach-Object {
    Write-Host "$($_.title) by $($_.author)"
    Write-Host "  Price: $$$($_.minPrice) - $$$($_.maxPrice)"
}

$sellers = Get-BookSellers -ISBN "0195153448"
$sellers | ForEach-Object {
    Write-Host "$($_.sellerId): $$$($_.price) ($($_.condition))"
}
```

---

## cURL

### Authentication

```bash
# Register
curl -X POST http://localhost:5004/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "Pass123!",
    "role": "Customer"
  }'

# Login
curl -X POST http://localhost:5004/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Pass123!"
  }'

# Save token
TOKEN="your_token_here"
```

### Books

```bash
# Get all books
curl http://localhost:5004/books?pageSize=20

# Get specific book
curl http://localhost:5004/books/0195153448

# Create book (requires auth)
curl -X POST http://localhost:5004/books \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "isbn": "9781234567890",
    "bookTitle": "Test Book",
    "bookAuthor": "Test Author",
    "yearOfPublication": 2024,
    "publisher": "Test Publisher",
    "genre": "Fiction",
    "language": "English",
    "pageCount": 300,
    "description": "Test description",
    "rating": 4.5,
    "availabilityStatus": "Available",
    "edition": "1st",
    "format": "Paperback"
  }'

# Update book
curl -X PUT http://localhost:5004/books/9781234567890 \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{...}'

# Delete book
curl -X DELETE http://localhost:5004/books/9781234567890 \
  -H "Authorization: Bearer $TOKEN"
```

### Search

```bash
# Search books
curl "http://localhost:5004/search?query=classical"

# Get available books
curl "http://localhost:5004/search/available?page=1&pageSize=20&sortBy=price&sortOrder=asc"

# Get featured books
curl http://localhost:5004/search/featured

# Get book sellers
curl http://localhost:5004/search/sellers/0195153448

# Get search stats
curl http://localhost:5004/search/stats
```

### Warehouse

```bash
# Get all items (requires auth)
curl http://localhost:5004/warehouse/items?pageSize=20 \
  -H "Authorization: Bearer $TOKEN"

# Get new items
curl http://localhost:5004/warehouse/items/new \
  -H "Authorization: Bearer $TOKEN"

# Get used items
curl http://localhost:5004/warehouse/items/used \
  -H "Authorization: Bearer $TOKEN"
```

---

## Error Handling Examples

### JavaScript

```typescript
try {
  const books = await api.searchBooks(query);
  setBooks(books);
} catch (error) {
  if (error.message.includes('401')) {
    // Token expired, redirect to login
    router.push('/login');
  } else if (error.message.includes('404')) {
    setError('No books found');
  } else {
    setError('An error occurred. Please try again.');
  }
}
```

### C#

```csharp
try
{
    var books = await api.SearchBooksAsync(query);
    return books;
}
catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
{
    // Token expired, redirect to login
    NavigateToLogin();
}
catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
{
    ShowMessage("No books found");
}
catch (Exception ex)
{
    ShowMessage("An error occurred. Please try again.");
    Logger.LogError(ex, "Search failed");
}
```

### Python

```python
try:
    books = api.search_books(query)
    return books
except requests.HTTPError as e:
    if e.response.status_code == 401:
        # Token expired, redirect to login
        redirect_to_login()
    elif e.response.status_code == 404:
        print("No books found")
    else:
        print(f"Error: {e}")
except Exception as e:
    print(f"An error occurred: {e}")
```

---

## Best Practices

1. **Store tokens securely** - Use localStorage/sessionStorage in browsers, secure storage in mobile apps
2. **Handle token expiration** - Implement automatic refresh or redirect to login
3. **Implement retry logic** - For network errors
4. **Use environment variables** - For API base URL
5. **Validate input** - Before sending to API
6. **Handle errors gracefully** - Show user-friendly messages
7. **Implement loading states** - For better UX
8. **Cache responses** - When appropriate (featured books, etc.)
9. **Use TypeScript/type hints** - For better type safety
10. **Log errors** - For debugging

---

**For more details, see:**
- API-DOCUMENTATION.md - Complete API reference
- API-QUICK-REFERENCE.md - Quick lookup guide
- API-TESTING-SUMMARY.md - Test results and known issues

