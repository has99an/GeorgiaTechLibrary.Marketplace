# API Quick Reference Guide

**Base URL:** `http://localhost:5004`

---

## 🔐 Authentication

```javascript
// Register
POST /auth/register
{
  "username": "user",
  "email": "user@example.com",
  "password": "Pass123!",
  "role": "Customer"
}
→ Returns: { accessToken, refreshToken, expiresIn }

// Login
POST /auth/login
{
  "email": "user@example.com",
  "password": "Pass123!"
}
→ Returns: { accessToken, refreshToken, expiresIn }

// Use token in headers
Authorization: Bearer <accessToken>
```

---

## 📚 Books

```javascript
// Browse all books
GET /books?pageSize=20&page=1
→ Returns: Array of books

// Get specific book
GET /books/{isbn}
→ Returns: Book object

// Create book [AUTH REQUIRED]
POST /books
{
  "isbn": "9781234567890",
  "bookTitle": "Title",
  "bookAuthor": "Author",
  "yearOfPublication": 2024,
  "publisher": "Publisher",
  "genre": "Fiction",
  "language": "English",
  "pageCount": 300,
  "description": "Description",
  "rating": 4.5,
  "availabilityStatus": "Available",
  "edition": "1st",
  "format": "Paperback"
}
→ Returns: Created book (201)

// Update book [AUTH REQUIRED]
PUT /books/{isbn}
→ Returns: Updated book (200)

// Delete book [AUTH REQUIRED]
DELETE /books/{isbn}
→ Returns: No content (204)
```

---

## 🔍 Search

```javascript
// Search books
GET /search?query=harry+potter
→ Returns: Array of search results with stock info

// Get available books (paginated)
GET /search/available?page=1&pageSize=20&sortBy=price&sortOrder=asc
→ Returns: { page, pageSize, totalCount }

// Get featured books
GET /search/featured
→ Returns: Array of featured books

// Get book by ISBN (with stock info)
GET /search/by-isbn/{isbn}
→ Returns: Book with stock and pricing

// Get sellers for a book
GET /search/sellers/{isbn}
→ Returns: Array of sellers with prices and stock

// Get search statistics
GET /search/stats
→ Returns: { totalBooks, totalSearches, averageResponseTime, cacheHitRate }
```

---

## 📦 Warehouse

**All endpoints require authentication**

```javascript
// Get all items
GET /warehouse/items?pageSize=20
→ Returns: Array of warehouse items

// Get items by seller
GET /warehouse/sellers/{sellerId}/items
→ Returns: Array of items for seller

// Get new items only
GET /warehouse/items/new
→ Returns: Array of new items

// Get used items only
GET /warehouse/items/used
→ Returns: Array of used items

// Get item by ID
GET /warehouse/items/{id}
→ Returns: Single warehouse item

// Create item
POST /warehouse/items
{
  "isbn": "0195153448",
  "sellerId": "GT-Library",
  "stockQuantity": 20,
  "price": 29.99,
  "condition": "New",
  "location": "Main Warehouse"
}
→ Returns: Created item (201)

// Update item
PUT /warehouse/items/{id}
{
  "stockQuantity": 25,
  "price": 27.99
}
→ Returns: Updated item (200)

// Delete item
DELETE /warehouse/items/{id}
→ Returns: No content (204)

// Adjust stock
POST /warehouse/adjust-stock
{
  "isbn": "0195153448",
  "sellerId": "GT-Library",
  "quantityChange": -2
}
→ Returns: { message, newQuantity }
```

---

## 👥 Users

**All endpoints require authentication**

```javascript
// Get all users
GET /users
→ Returns: Array of users

// Get user by ID
GET /users/{userId}
→ Returns: User object

// Update user
PUT /users/{userId}
{
  "userId": "guid",
  "username": "newname",
  "email": "email@example.com",
  "role": "Customer"
}
→ Returns: Updated user (200)

// Delete user
DELETE /users/{userId}
→ Returns: No content (204)
```

---

## 🛒 Orders

**All endpoints require authentication**

```javascript
// Create order (⚠️ Currently has issues)
POST /orders
{
  "userId": "guid",
  "items": [
    {
      "warehouseItemId": "guid",
      "quantity": 1,
      "price": 29.99
    }
  ]
}
→ Returns: Created order (201)

// Get order by ID
GET /orders/{orderId}
→ Returns: Order object with items

// Pay for order
POST /orders/{orderId}/pay
→ Returns: { orderId, status: "Paid", paidAt }
```

---

## 📊 Response Codes

| Code | Meaning |
|------|---------|
| 200 | OK - Request succeeded |
| 201 | Created - Resource created |
| 204 | No Content - Deleted successfully |
| 400 | Bad Request - Invalid input |
| 401 | Unauthorized - Missing/invalid token |
| 404 | Not Found - Resource not found |
| 409 | Conflict - Resource already exists |
| 500 | Internal Server Error |

---

## ⚠️ Known Issues

1. **WarehouseService** - `GET /warehouse/items/id/{isbn}` returns 400
   - **Workaround:** Use `/search/sellers/{isbn}` instead

2. **UserService** - `POST /users` returns 400
   - **Workaround:** Use `/auth/register` instead

3. **OrderService** - `POST /orders` returns 400
   - **Status:** Needs backend fix

---

## 💡 Common Patterns

### Fetch and Display Books
```javascript
// 1. Get featured books for homepage
GET /search/featured

// 2. Get available books for catalog
GET /search/available?page=1&pageSize=20

// 3. Search for specific books
GET /search?query=user_input

// 4. Get book details
GET /books/{isbn}

// 5. Get sellers and prices
GET /search/sellers/{isbn}
```

### User Authentication Flow
```javascript
// 1. Register new user
POST /auth/register → Get token

// 2. Or login existing user
POST /auth/login → Get token

// 3. Store token
localStorage.setItem('token', accessToken)

// 4. Use token in requests
headers: { 'Authorization': `Bearer ${token}` }

// 5. Refresh when expired
POST /auth/refresh
```

### Shopping Flow
```javascript
// 1. Browse books
GET /search/available

// 2. View book details
GET /books/{isbn}

// 3. Check sellers and prices
GET /search/sellers/{isbn}

// 4. Add to cart (client-side)

// 5. Create order (⚠️ needs fix)
POST /orders

// 6. Process payment
POST /orders/{orderId}/pay
```

---

## 🎯 Best Practices

1. **Always check authentication** before making protected requests
2. **Handle 401 errors** by redirecting to login
3. **Use SearchService** for browsing (faster with Redis cache)
4. **Use BookService** for detailed book data
5. **Implement pagination** for large result sets
6. **Cache tokens** but respect expiration
7. **Handle errors gracefully** with user-friendly messages

---

## 🔗 Related Files

- **API-DOCUMENTATION.md** - Complete API reference
- **API-TESTING-SUMMARY.md** - Test results and issues
- **test-api-endpoints.ps1** - Automated testing script

---

**Quick Tip:** Start with SearchService and BookService GET endpoints - they're all working and don't require authentication!

