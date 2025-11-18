# GeorgiaTechLibrary.Marketplace - Complete API Documentation

**Base URL (ApiGateway):** `http://localhost:5004`

**Test Results:** 19/22 endpoints tested successfully ✓

---

## Table of Contents
1. [Authentication & Authorization](#authentication--authorization)
2. [AuthService Endpoints](#authservice-endpoints)
3. [BookService Endpoints](#bookservice-endpoints)
4. [SearchService Endpoints](#searchservice-endpoints)
5. [WarehouseService Endpoints](#warehouseservice-endpoints)
6. [UserService Endpoints](#userservice-endpoints)
7. [OrderService Endpoints](#orderservice-endpoints)
8. [Error Codes](#error-codes)
9. [Known Issues](#known-issues)

---

## Authentication & Authorization

### Authentication Flow
1. Register a new user or login with existing credentials
2. Receive JWT access token and refresh token
3. Include token in `Authorization` header for protected endpoints: `Bearer <token>`
4. Token expires in 3600 seconds (1 hour)
5. Use refresh token to get new access token

### Public Endpoints (No Authentication Required)
- All AuthService endpoints (`/auth/*`)
- GET requests to BookService (`/books`, `/books/{isbn}`)
- All SearchService endpoints (`/search/*`)
- Health check endpoints (`/health`)
- Swagger documentation (`/swagger/*`)

### Protected Endpoints (Authentication Required)
- POST, PUT, DELETE requests to BookService
- All WarehouseService endpoints
- All UserService endpoints
- All OrderService endpoints

---

## AuthService Endpoints

**Route Prefix:** `/auth`

### 1. Register User
**Endpoint:** `POST /auth/register`  
**Authentication:** Not required  
**Description:** Create a new user account and receive authentication tokens

**Request Body:**
```json
{
  "username": "johndoe",
  "email": "john@example.com",
  "password": "SecurePass123!",
  "role": "Customer"
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3599
}
```

**Status Codes:**
- `200 OK` - User registered successfully
- `400 Bad Request` - Invalid input data
- `409 Conflict` - User already exists
- `500 Internal Server Error` - Server error

---

### 2. Login
**Endpoint:** `POST /auth/login`  
**Authentication:** Not required  
**Description:** Authenticate user and receive tokens

**Request Body:**
```json
{
  "email": "john@example.com",
  "password": "SecurePass123!"
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3599
}
```

**Status Codes:**
- `200 OK` - Login successful
- `400 Bad Request` - Invalid input
- `401 Unauthorized` - Invalid credentials
- `500 Internal Server Error` - Server error

---

### 3. Validate Token
**Endpoint:** `POST /auth/validate`  
**Authentication:** Not required  
**Description:** Validate JWT token (used internally by ApiGateway)

**Request Body:**
```json
{
  "Token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Response (200 OK):**
```json
{
  "Valid": true
}
```

**Status Codes:**
- `200 OK` - Token is valid
- `401 Unauthorized` - Token is invalid or expired

---

### 4. Refresh Token
**Endpoint:** `POST /auth/refresh`  
**Authentication:** Not required  
**Description:** Get new access token using refresh token

**Request Body:**
```json
{
  "RefreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3599
}
```

**Status Codes:**
- `200 OK` - Token refreshed successfully
- `401 Unauthorized` - Invalid refresh token

---

## BookService Endpoints

**Route Prefix:** `/books`  
**Database:** 266,415 books seeded

### 1. Get All Books
**Endpoint:** `GET /books`  
**Authentication:** Not required  
**Description:** Retrieve all books with optional pagination

**Query Parameters:**
- `pageSize` (optional): Number of books per page (default: all)
- `page` (optional): Page number

**Example Request:**
```
GET http://localhost:5004/books?pageSize=20&page=1
```

**Response (200 OK):**
```json
[
  {
    "isbn": "0195153448",
    "bookTitle": "Classical Mythology",
    "bookAuthor": "Mark P. O. Morford",
    "yearOfPublication": 2002,
    "publisher": "Oxford University Press",
    "imageUrlS": "http://images.amazon.com/images/P/0195153448.01.THUMBZZZ.jpg",
    "imageUrlM": "http://images.amazon.com/images/P/0195153448.01.MZZZZZZZ.jpg",
    "imageUrlL": "http://images.amazon.com/images/P/0195153448.01.LZZZZZZZ.jpg",
    "genre": "Mythology",
    "language": "English",
    "pageCount": 736,
    "description": "A comprehensive introduction to classical mythology",
    "rating": 4.5,
    "availabilityStatus": "Available",
    "edition": "6th",
    "format": "Hardcover"
  }
]
```

**Status Codes:**
- `200 OK` - Books retrieved successfully
- `500 Internal Server Error` - Server error

---

### 2. Get Book by ISBN
**Endpoint:** `GET /books/{isbn}`  
**Authentication:** Not required  
**Description:** Retrieve specific book details

**Example Request:**
```
GET http://localhost:5004/books/0195153448
```

**Response (200 OK):**
```json
{
  "isbn": "0195153448",
  "bookTitle": "Classical Mythology",
  "bookAuthor": "Mark P. O. Morford",
  "yearOfPublication": 2002,
  "publisher": "Oxford University Press",
  "genre": "Mythology",
  "language": "English",
  "pageCount": 736,
  "rating": 4.5,
  "availabilityStatus": "Available"
}
```

**Status Codes:**
- `200 OK` - Book found
- `404 Not Found` - Book not found
- `500 Internal Server Error` - Server error

---

### 3. Create Book
**Endpoint:** `POST /books`  
**Authentication:** Required  
**Description:** Add a new book to the catalog

**Request Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "isbn": "9781234567890",
  "bookTitle": "New Book Title",
  "bookAuthor": "Author Name",
  "yearOfPublication": 2024,
  "publisher": "Publisher Name",
  "genre": "Fiction",
  "language": "English",
  "pageCount": 350,
  "description": "Book description",
  "rating": 4.0,
  "availabilityStatus": "Available",
  "edition": "1st",
  "format": "Paperback"
}
```

**Response (201 Created):**
```json
{
  "isbn": "9781234567890",
  "bookTitle": "New Book Title",
  "bookAuthor": "Author Name",
  ...
}
```

**Status Codes:**
- `201 Created` - Book created successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid token
- `409 Conflict` - Book with ISBN already exists
- `500 Internal Server Error` - Server error

---

### 4. Update Book
**Endpoint:** `PUT /books/{isbn}`  
**Authentication:** Required  
**Description:** Update existing book details

**Request Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:** (all fields required)
```json
{
  "isbn": "9781234567890",
  "bookTitle": "Updated Book Title",
  "bookAuthor": "Author Name",
  "yearOfPublication": 2024,
  "publisher": "Publisher Name",
  "genre": "Fiction",
  "language": "English",
  "pageCount": 350,
  "description": "Updated description",
  "rating": 4.5,
  "availabilityStatus": "Available",
  "edition": "2nd",
  "format": "Paperback"
}
```

**Response (200 OK):**
```json
{
  "isbn": "9781234567890",
  "bookTitle": "Updated Book Title",
  ...
}
```

**Status Codes:**
- `200 OK` - Book updated successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Book not found
- `500 Internal Server Error` - Server error

---

### 5. Delete Book
**Endpoint:** `DELETE /books/{isbn}`  
**Authentication:** Required  
**Description:** Remove a book from the catalog

**Request Headers:**
```
Authorization: Bearer <token>
```

**Example Request:**
```
DELETE http://localhost:5004/books/9781234567890
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - Book deleted successfully
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Book not found
- `500 Internal Server Error` - Server error

---

### 6. Sync Events
**Endpoint:** `POST /books/sync-events`  
**Authentication:** Required  
**Description:** Trigger synchronization of book data to SearchService

**Status Codes:**
- `200 OK` - Sync initiated
- `401 Unauthorized` - Missing or invalid token
- `500 Internal Server Error` - Server error

---

## SearchService Endpoints

**Route Prefix:** `/search`  
**Description:** Fast search functionality using Redis cache

### 1. Search Books
**Endpoint:** `GET /search`  
**Authentication:** Not required  
**Description:** Search books by title, author, or ISBN

**Query Parameters:**
- `query` (required): Search term

**Example Request:**
```
GET http://localhost:5004/search?query=classical
```

**Response (200 OK):**
```json
[
  {
    "isbn": "0195153448",
    "title": "Classical Mythology",
    "author": "Mark P. O. Morford",
    "totalStock": 15,
    "availableSellers": 3,
    "minPrice": 19.99,
    "maxPrice": 29.99,
    "averagePrice": 24.50
  }
]
```

**Status Codes:**
- `200 OK` - Search completed
- `400 Bad Request` - Missing query parameter
- `500 Internal Server Error` - Server error

---

### 2. Get Available Books
**Endpoint:** `GET /search/available`  
**Authentication:** Not required  
**Description:** Get books that are currently in stock with pagination and sorting

**Query Parameters:**
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 20)
- `sortBy` (optional): Sort field (e.g., "price", "title")
- `sortOrder` (optional): "asc" or "desc" (default: "asc")

**Example Request:**
```
GET http://localhost:5004/search/available?page=1&pageSize=10&sortBy=price&sortOrder=asc
```

**Response (200 OK):**
```json
{
  "page": 1,
  "pageSize": 10,
  "totalCount": 150
}
```

**Status Codes:**
- `200 OK` - Books retrieved
- `500 Internal Server Error` - Server error

---

### 3. Get Featured Books
**Endpoint:** `GET /search/featured`  
**Authentication:** Not required  
**Description:** Get featured/recommended books

**Example Request:**
```
GET http://localhost:5004/search/featured
```

**Response (200 OK):**
```json
[
  {
    "isbn": "0195153448",
    "title": "Classical Mythology",
    "author": "Mark P. O. Morford",
    "rating": 4.5,
    "minPrice": 19.99
  }
]
```

**Status Codes:**
- `200 OK` - Featured books retrieved
- `500 Internal Server Error` - Server error

---

### 4. Get Book by ISBN
**Endpoint:** `GET /search/by-isbn/{isbn}`  
**Authentication:** Not required  
**Description:** Get book details with stock and pricing information

**Example Request:**
```
GET http://localhost:5004/search/by-isbn/0195153448
```

**Response (200 OK):**
```json
{
  "isbn": "0195153448",
  "title": "Classical Mythology",
  "author": "Mark P. O. Morford",
  "totalStock": 15,
  "availableSellers": 3,
  "minPrice": 19.99,
  "maxPrice": 29.99
}
```

**Status Codes:**
- `200 OK` - Book found
- `404 Not Found` - Book not found
- `500 Internal Server Error` - Server error

---

### 5. Get Book Sellers
**Endpoint:** `GET /search/sellers/{isbn}`  
**Authentication:** Not required  
**Description:** Get all sellers offering a specific book

**Example Request:**
```
GET http://localhost:5004/search/sellers/0195153448
```

**Response (200 OK):**
```json
[
  {
    "sellerId": "GT-Library",
    "price": 29.99,
    "condition": "New",
    "stockQuantity": 10,
    "location": "Main Warehouse"
  },
  {
    "sellerId": "student-c1234567",
    "price": 19.99,
    "condition": "Used",
    "stockQuantity": 2,
    "location": "Campus-2"
  }
]
```

**Status Codes:**
- `200 OK` - Sellers retrieved
- `500 Internal Server Error` - Server error

---

### 6. Get Search Statistics
**Endpoint:** `GET /search/stats`  
**Authentication:** Not required  
**Description:** Get search service statistics

**Example Request:**
```
GET http://localhost:5004/search/stats
```

**Response (200 OK):**
```json
{
  "totalBooks": 266415,
  "totalSearches": 1523,
  "averageResponseTime": 12.5,
  "cacheHitRate": 0.95
}
```

**Status Codes:**
- `200 OK` - Statistics retrieved
- `500 Internal Server Error` - Server error

---

## WarehouseService Endpoints

**Route Prefix:** `/warehouse`  
**Database:** 106,000+ inventory items

### 1. Get All Warehouse Items
**Endpoint:** `GET /warehouse/items`  
**Authentication:** Required  
**Description:** Retrieve all inventory items

**Query Parameters:**
- `pageSize` (optional): Number of items per page
- `page` (optional): Page number

**Request Headers:**
```
Authorization: Bearer <token>
```

**Example Request:**
```
GET http://localhost:5004/warehouse/items?pageSize=20
```

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "isbn": "0195153448",
    "sellerId": "GT-Library",
    "stockQuantity": 15,
    "price": 29.99,
    "condition": "New",
    "location": "Main Warehouse"
  }
]
```

**Status Codes:**
- `200 OK` - Items retrieved
- `401 Unauthorized` - Missing or invalid token
- `500 Internal Server Error` - Server error

---

### 2. Get Items by ISBN (⚠️ Known Issue)
**Endpoint:** `GET /warehouse/items/id/{isbn}`  
**Authentication:** Required  
**Description:** Get all inventory items for a specific book

**Note:** This endpoint has a routing issue. The route parameter is `{id}` but expects `bookIsbn` query parameter.

**Status:** ⚠️ Returns 400 Bad Request

---

### 3. Get Items by Seller
**Endpoint:** `GET /warehouse/sellers/{sellerId}/items`  
**Authentication:** Required  
**Description:** Get all inventory items for a specific seller

**Request Headers:**
```
Authorization: Bearer <token>
```

**Example Request:**
```
GET http://localhost:5004/warehouse/sellers/GT-Library/items
```

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "isbn": "0195153448",
    "sellerId": "GT-Library",
    "stockQuantity": 15,
    "price": 29.99,
    "condition": "New"
  }
]
```

**Status Codes:**
- `200 OK` - Items retrieved
- `401 Unauthorized` - Missing or invalid token
- `500 Internal Server Error` - Server error

---

### 4. Get New Items
**Endpoint:** `GET /warehouse/items/new`  
**Authentication:** Required  
**Description:** Get only new books (from GT-Library)

**Request Headers:**
```
Authorization: Bearer <token>
```

**Example Request:**
```
GET http://localhost:5004/warehouse/items/new
```

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "isbn": "0195153448",
    "sellerId": "GT-Library",
    "stockQuantity": 15,
    "price": 29.99,
    "condition": "New",
    "location": "Main Warehouse"
  }
]
```

**Status Codes:**
- `200 OK` - Items retrieved
- `401 Unauthorized` - Missing or invalid token
- `500 Internal Server Error` - Server error

---

### 5. Get Used Items
**Endpoint:** `GET /warehouse/items/used`  
**Authentication:** Required  
**Description:** Get only used books (from student sellers)

**Request Headers:**
```
Authorization: Bearer <token>
```

**Example Request:**
```
GET http://localhost:5004/warehouse/items/used
```

**Response (200 OK):**
```json
[
  {
    "id": 2,
    "isbn": "0195153448",
    "sellerId": "student-c1234567",
    "stockQuantity": 3,
    "price": 19.99,
    "condition": "Used",
    "location": "Campus-2"
  }
]
```

**Status Codes:**
- `200 OK` - Items retrieved
- `401 Unauthorized` - Missing or invalid token
- `500 Internal Server Error` - Server error

---

### 6. Get Item by ID
**Endpoint:** `GET /warehouse/items/{id}`  
**Authentication:** Required  
**Description:** Get specific warehouse item by ID

**Request Headers:**
```
Authorization: Bearer <token>
```

**Example Request:**
```
GET http://localhost:5004/warehouse/items/1
```

**Response (200 OK):**
```json
{
  "id": 1,
  "isbn": "0195153448",
  "sellerId": "GT-Library",
  "stockQuantity": 15,
  "price": 29.99,
  "condition": "New",
  "location": "Main Warehouse"
}
```

**Status Codes:**
- `200 OK` - Item found
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Item not found
- `500 Internal Server Error` - Server error

---

### 7. Create Warehouse Item
**Endpoint:** `POST /warehouse/items`  
**Authentication:** Required  
**Description:** Add new inventory item

**Request Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "isbn": "0195153448",
  "sellerId": "GT-Library",
  "stockQuantity": 20,
  "price": 29.99,
  "condition": "New",
  "location": "Main Warehouse"
}
```

**Response (201 Created):**
```json
{
  "id": 123,
  "isbn": "0195153448",
  "sellerId": "GT-Library",
  "stockQuantity": 20,
  "price": 29.99,
  "condition": "New",
  "location": "Main Warehouse"
}
```

**Status Codes:**
- `201 Created` - Item created
- `400 Bad Request` - Invalid input
- `401 Unauthorized` - Missing or invalid token
- `500 Internal Server Error` - Server error

---

### 8. Update Warehouse Item
**Endpoint:** `PUT /warehouse/items/{id}`  
**Authentication:** Required  
**Description:** Update existing inventory item

**Request Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "stockQuantity": 25,
  "price": 27.99,
  "condition": "New",
  "location": "Main Warehouse"
}
```

**Response (200 OK):**
```json
{
  "id": 123,
  "isbn": "0195153448",
  "sellerId": "GT-Library",
  "stockQuantity": 25,
  "price": 27.99,
  "condition": "New",
  "location": "Main Warehouse"
}
```

**Status Codes:**
- `200 OK` - Item updated
- `400 Bad Request` - Invalid input
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Item not found
- `500 Internal Server Error` - Server error

---

### 9. Delete Warehouse Item
**Endpoint:** `DELETE /warehouse/items/{id}`  
**Authentication:** Required  
**Description:** Remove inventory item

**Request Headers:**
```
Authorization: Bearer <token>
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - Item deleted
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Item not found
- `500 Internal Server Error` - Server error

---

### 10. Adjust Stock
**Endpoint:** `POST /warehouse/adjust-stock`  
**Authentication:** Required  
**Description:** Adjust stock quantity (used by OrderService)

**Request Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "isbn": "0195153448",
  "sellerId": "GT-Library",
  "quantityChange": -2
}
```

**Response (200 OK):**
```json
{
  "message": "Stock adjusted successfully",
  "newQuantity": 13
}
```

**Status Codes:**
- `200 OK` - Stock adjusted
- `400 Bad Request` - Invalid input or insufficient stock
- `401 Unauthorized` - Missing or invalid token
- `500 Internal Server Error` - Server error

---

### 11. Sync Events
**Endpoint:** `POST /warehouse/sync-events`  
**Authentication:** Required  
**Description:** Trigger synchronization to SearchService

**Status Codes:**
- `200 OK` - Sync initiated
- `401 Unauthorized` - Missing or invalid token
- `500 Internal Server Error` - Server error

---

## UserService Endpoints

**Route Prefix:** `/users`

### 1. Get All Users
**Endpoint:** `GET /users`  
**Authentication:** Required  
**Description:** Retrieve all users

**Request Headers:**
```
Authorization: Bearer <token>
```

**Example Request:**
```
GET http://localhost:5004/users
```

**Response (200 OK):**
```json
[
  {
    "userId": "123e4567-e89b-12d3-a456-426614174000",
    "username": "johndoe",
    "email": "john@example.com",
    "role": "Customer",
    "createdAt": "2024-01-15T10:30:00Z"
  }
]
```

**Status Codes:**
- `200 OK` - Users retrieved
- `401 Unauthorized` - Missing or invalid token
- `500 Internal Server Error` - Server error

---

### 2. Get User by ID
**Endpoint:** `GET /users/{userId}`  
**Authentication:** Required  
**Description:** Get specific user details

**Request Headers:**
```
Authorization: Bearer <token>
```

**Example Request:**
```
GET http://localhost:5004/users/123e4567-e89b-12d3-a456-426614174000
```

**Response (200 OK):**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "username": "johndoe",
  "email": "john@example.com",
  "role": "Customer",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

**Status Codes:**
- `200 OK` - User found
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - User not found
- `500 Internal Server Error` - Server error

---

### 3. Create User (⚠️ Known Issue)
**Endpoint:** `POST /users`  
**Authentication:** Required  
**Description:** Create a new user

**Note:** This endpoint may have validation issues. Use AuthService `/auth/register` instead for user registration.

**Status:** ⚠️ Returns 400 Bad Request in testing

---

### 4. Update User
**Endpoint:** `PUT /users/{userId}`  
**Authentication:** Required  
**Description:** Update user details

**Request Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "username": "johndoe_updated",
  "email": "john@example.com",
  "role": "Customer"
}
```

**Response (200 OK):**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "username": "johndoe_updated",
  "email": "john@example.com",
  "role": "Customer"
}
```

**Status Codes:**
- `200 OK` - User updated
- `400 Bad Request` - Invalid input
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - User not found
- `500 Internal Server Error` - Server error

---

### 5. Delete User
**Endpoint:** `DELETE /users/{userId}`  
**Authentication:** Required  
**Description:** Delete a user

**Request Headers:**
```
Authorization: Bearer <token>
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - User deleted
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - User not found
- `500 Internal Server Error` - Server error

---

## OrderService Endpoints

**Route Prefix:** `/orders`

### 1. Create Order (⚠️ Known Issue)
**Endpoint:** `POST /orders`  
**Authentication:** Required  
**Description:** Create a new order

**Request Headers:**
```
Authorization: Bearer <token>
Content-Type: application/json
```

**Request Body:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "items": [
    {
      "warehouseItemId": "456e7890-e89b-12d3-a456-426614174001",
      "quantity": 1,
      "price": 29.99
    }
  ]
}
```

**Status:** ⚠️ Returns 400 Bad Request in testing (likely validation or foreign key issue)

**Expected Response (201 Created):**
```json
{
  "orderId": "789e0123-e89b-12d3-a456-426614174002",
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "orderDate": "2024-01-15T10:30:00Z",
  "status": "Pending",
  "totalAmount": 29.99,
  "items": [...]
}
```

---

### 2. Get Order by ID
**Endpoint:** `GET /orders/{orderId}`  
**Authentication:** Required  
**Description:** Get order details

**Request Headers:**
```
Authorization: Bearer <token>
```

**Example Request:**
```
GET http://localhost:5004/orders/789e0123-e89b-12d3-a456-426614174002
```

**Response (200 OK):**
```json
{
  "orderId": "789e0123-e89b-12d3-a456-426614174002",
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "orderDate": "2024-01-15T10:30:00Z",
  "status": "Pending",
  "totalAmount": 29.99,
  "items": [
    {
      "orderItemId": "abc-123",
      "warehouseItemId": "456e7890-e89b-12d3-a456-426614174001",
      "quantity": 1,
      "price": 29.99
    }
  ]
}
```

**Status Codes:**
- `200 OK` - Order found
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Order not found
- `500 Internal Server Error` - Server error

---

### 3. Pay for Order
**Endpoint:** `POST /orders/{orderId}/pay`  
**Authentication:** Required  
**Description:** Process payment for an order

**Request Headers:**
```
Authorization: Bearer <token>
```

**Example Request:**
```
POST http://localhost:5004/orders/789e0123-e89b-12d3-a456-426614174002/pay
```

**Response (200 OK):**
```json
{
  "orderId": "789e0123-e89b-12d3-a456-426614174002",
  "status": "Paid",
  "paidAt": "2024-01-15T10:35:00Z"
}
```

**Status Codes:**
- `200 OK` - Payment processed
- `400 Bad Request` - Order already paid or invalid state
- `401 Unauthorized` - Missing or invalid token
- `404 Not Found` - Order not found
- `500 Internal Server Error` - Server error

---

## Error Codes

### Standard HTTP Status Codes

| Code | Meaning | Description |
|------|---------|-------------|
| 200 | OK | Request succeeded |
| 201 | Created | Resource created successfully |
| 204 | No Content | Request succeeded, no response body |
| 400 | Bad Request | Invalid input data or validation error |
| 401 | Unauthorized | Missing or invalid authentication token |
| 403 | Forbidden | Authenticated but not authorized for this resource |
| 404 | Not Found | Resource not found |
| 409 | Conflict | Resource already exists |
| 500 | Internal Server Error | Server error occurred |

### Error Response Format

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "fieldName": ["Error message"]
  },
  "traceId": "00-trace-id-here-00"
}
```

---

## Known Issues

### 1. WarehouseService - Get Items by ISBN
**Endpoint:** `GET /warehouse/items/id/{isbn}`  
**Issue:** Route parameter mismatch - route uses `{id}` but controller expects `bookIsbn` parameter  
**Status:** Returns 400 Bad Request  
**Workaround:** Use `/warehouse/items` and filter client-side, or use SearchService `/search/sellers/{isbn}`

### 2. UserService - Create User
**Endpoint:** `POST /users`  
**Issue:** Validation errors during testing  
**Status:** Returns 400 Bad Request  
**Workaround:** Use AuthService `/auth/register` for user registration instead

### 3. OrderService - Create Order
**Endpoint:** `POST /orders`  
**Issue:** Validation or foreign key constraint issues  
**Status:** Returns 400 Bad Request  
**Recommendation:** Verify that `userId` and `warehouseItemId` exist before creating order

---

## Testing Script

A comprehensive PowerShell testing script is available: `test-api-endpoints.ps1`

**Usage:**
```powershell
cd "C:\Softwareudvikling\Semester Projekt\GeorgiaTechLibrary.Marketplace"
.\test-api-endpoints.ps1
```

**Output:**
- Console output with test results
- JSON file: `api-test-results.json` with detailed results

---

## Architecture Notes

### Service Communication
- **Synchronous:** HTTP/REST through ApiGateway
- **Asynchronous:** RabbitMQ for event-driven updates
- **Caching:** Redis for SearchService

### Event Flow
1. BookService creates/updates book → Publishes event to RabbitMQ
2. SearchService consumes event → Updates Redis cache
3. WarehouseService updates stock → Publishes event
4. SearchService updates availability data

### Authentication Flow
1. User authenticates via AuthService
2. Receives JWT token
3. ApiGateway validates token on each request
4. Forwards request to appropriate service with `X-User-Id` header

---

## Database Statistics

- **BookService:** 266,415 books
- **WarehouseService:** 106,000+ inventory items
- **SearchService:** In-memory Redis cache
- **UserService:** User accounts
- **OrderService:** Order history
- **AuthService:** Authentication credentials

---

## Next Steps for UI Development

### Recommended Implementation Order

1. **Authentication Flow**
   - Implement login/register forms
   - Store JWT token in localStorage/sessionStorage
   - Add token to all authenticated requests
   - Implement token refresh logic

2. **Book Browsing**
   - Use `/search/available` for main catalog
   - Use `/search/featured` for homepage
   - Use `/search?query=` for search functionality
   - Use `/books/{isbn}` for book details

3. **Inventory Display**
   - Use `/search/sellers/{isbn}` to show available sellers
   - Display price comparison
   - Show new vs used options

4. **Order Flow**
   - Fix OrderService create endpoint issues first
   - Implement shopping cart (client-side)
   - Create order with `/orders`
   - Process payment with `/orders/{id}/pay`

5. **Admin Features**
   - Book management (create/update/delete)
   - Inventory management
   - User management

---

**Document Version:** 1.0  
**Last Updated:** 2024-11-18  
**Test Coverage:** 19/22 endpoints (86%)  
**Generated by:** Automated API testing script

