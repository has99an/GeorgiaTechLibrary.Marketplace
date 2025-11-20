# Georgia Tech Library Marketplace - Complete API Endpoints Documentation

**Base URL (API Gateway):** `http://localhost:5004`

**Version:** 2.0

**Last Updated:** 2025-11-19

---

## Table of Contents

1. [Overview](#overview)
2. [Authentication & Security](#authentication--security)
3. [Rate Limiting](#rate-limiting)
4. [Authentication Endpoints](#authentication-endpoints)
5. [User Management Endpoints](#user-management-endpoints)
6. [Search & Discovery Endpoints](#search--discovery-endpoints)
7. [Book Management Endpoints](#book-management-endpoints)
8. [Warehouse Management Endpoints](#warehouse-management-endpoints)
9. [Shopping Cart Endpoints](#shopping-cart-endpoints)
10. [Order Management Endpoints](#order-management-endpoints)
11. [Notification Endpoints](#notification-endpoints)
12. [Error Responses](#error-responses)

---

## Overview

This document provides comprehensive documentation for all API endpoints in the Georgia Tech Library Marketplace system. All requests go through the API Gateway at `http://localhost:5004`.

### Service Architecture

The system consists of 7 microservices:
- **AuthService**: User authentication and JWT token management
- **UserService**: User profile and account management
- **SearchService**: Book search and discovery
- **BookService**: Book catalog management
- **WarehouseService**: Inventory and stock management
- **OrderService**: Order processing and shopping cart
- **NotificationService**: User notifications and preferences

---

## Authentication & Security

### Authentication Flow

1. **Register** or **Login** to receive JWT tokens
2. Include the access token in the `Authorization` header: `Bearer <token>`
3. Access token expires in 3600 seconds (1 hour)
4. Use refresh token to obtain new access token when expired

### Public Endpoints (No Authentication Required)

- All `/auth/*` endpoints
- GET requests to `/books/*`
- All GET requests to `/search/*`
- `/health` endpoint
- `/swagger` documentation

### Protected Endpoints (JWT Required)

- All other endpoints require a valid JWT token in the Authorization header
- POST, PUT, DELETE requests to `/books/*`
- All `/warehouse/*` endpoints
- All `/users/*` endpoints (except public GET requests)
- All `/orders/*` endpoints
- All `/cart/*` endpoints
- All `/notifications/*` endpoints

### Authorization Header Format

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

---

## Rate Limiting

The API Gateway implements rate limiting to prevent abuse:

| Endpoint | Limit | Period |
|----------|-------|--------|
| `/auth/login` | 5 requests | 60 seconds |
| `/auth/register` | 3 requests | 3600 seconds (1 hour) |
| General endpoints | 100 requests | 60 seconds |

**Response when rate limit exceeded:**
- Status Code: `429 Too Many Requests`
- Header: `Retry-After: <seconds>`

---

## Authentication Endpoints

**Base Path:** `/auth`

### 1. Register User

**Endpoint:** `POST /auth/register`

**Authentication:** Not required

**Description:** Create a new user account and receive authentication tokens

**Request Body:**
```json
{
  "email": "john.doe@example.com",
  "password": "SecurePass123!"
}
```

**Validation Rules:**
- `email`: Required, valid email format, max 255 characters
- `password`: Required, 8-100 characters

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

**Status Codes:**
- `200 OK` - User registered successfully
- `400 Bad Request` - Invalid input data
- `409 Conflict` - User with email already exists
- `429 Too Many Requests` - Rate limit exceeded (3 per hour)

---

### 2. Login

**Endpoint:** `POST /auth/login`

**Authentication:** Not required

**Description:** Authenticate user and receive JWT tokens

**Request Body:**
```json
{
  "email": "john.doe@example.com",
  "password": "SecurePass123!"
}
```

**Validation Rules:**
- `email`: Required, valid email format
- `password`: Required

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

**Status Codes:**
- `200 OK` - Authentication successful
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Invalid credentials
- `429 Too Many Requests` - Rate limit exceeded (5 per minute)

---

### 3. Refresh Token

**Endpoint:** `POST /auth/refresh`

**Authentication:** Not required (uses refresh token)

**Description:** Get a new access token using a refresh token

**Request Body:**
```json
{
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Validation Rules:**
- `refreshToken`: Required

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

**Status Codes:**
- `200 OK` - Token refreshed successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Invalid or expired refresh token

---

### 4. Validate Token

**Endpoint:** `POST /auth/validate`

**Authentication:** Not required

**Description:** Validate a JWT token

**Request Body:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

**Validation Rules:**
- `token`: Required

**Response (200 OK):**
```json
{
  "valid": true
}
```

**Response (401 Unauthorized):**
```json
{
  "valid": false
}
```

**Status Codes:**
- `200 OK` - Token is valid
- `401 Unauthorized` - Token is invalid or expired

---

## User Management Endpoints

**Base Path:** `/users`

**Authentication:** Protected (JWT required for most endpoints)

### 1. Get All Users (Paginated)

**Endpoint:** `GET /users`

**Authentication:** Protected

**Description:** Retrieve all users with pagination

**Query Parameters:**
- `page` (optional): Page number, default: 1
- `pageSize` (optional): Items per page, default: 20

**Example Request:**
```
GET /users?page=1&pageSize=20
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "email": "john.doe@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "role": "Student",
      "createdDate": "2024-01-15T10:30:00Z",
      "isActive": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 150,
  "totalPages": 8
}
```

**Status Codes:**
- `200 OK` - Users retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 2. Get User by ID

**Endpoint:** `GET /users/{userId}`

**Authentication:** Protected

**Description:** Retrieve a specific user by their ID

**Path Parameters:**
- `userId`: User's unique identifier (GUID)

**Example Request:**
```
GET /users/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Response (200 OK):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "role": "Student",
  "phoneNumber": "+1234567890",
  "address": "123 Main St, Atlanta, GA",
  "createdDate": "2024-01-15T10:30:00Z",
  "lastModifiedDate": "2024-01-20T14:45:00Z",
  "isActive": true
}
```

**Status Codes:**
- `200 OK` - User retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - User not found

---

### 3. Get Current User

**Endpoint:** `GET /users/me`

**Authentication:** Protected

**Description:** Retrieve the currently authenticated user's profile

**Example Request:**
```
GET /users/me
Authorization: Bearer <token>
```

**Response (200 OK):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "role": "Student",
  "phoneNumber": "+1234567890",
  "address": "123 Main St, Atlanta, GA",
  "createdDate": "2024-01-15T10:30:00Z",
  "isActive": true
}
```

**Status Codes:**
- `200 OK` - User retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 4. Search Users

**Endpoint:** `GET /users/search`

**Authentication:** Protected

**Description:** Search users by various criteria

**Query Parameters:**
- `email` (optional): Filter by email
- `firstName` (optional): Filter by first name
- `lastName` (optional): Filter by last name
- `role` (optional): Filter by role (Student, Seller, Admin)
- `page` (optional): Page number, default: 1
- `pageSize` (optional): Items per page, default: 20

**Example Request:**
```
GET /users/search?role=Student&page=1&pageSize=20
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "email": "john.doe@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "role": "Student"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 45,
  "totalPages": 3
}
```

**Status Codes:**
- `200 OK` - Search completed successfully
- `400 Bad Request` - Invalid search parameters
- `401 Unauthorized` - Missing or invalid JWT token

---

### 5. Get Users by Role

**Endpoint:** `GET /users/role/{role}`

**Authentication:** Protected

**Description:** Retrieve all users with a specific role

**Path Parameters:**
- `role`: User role (Student, Seller, Admin)

**Example Request:**
```
GET /users/role/Student
```

**Response (200 OK):**
```json
[
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "role": "Student"
  }
]
```

**Status Codes:**
- `200 OK` - Users retrieved successfully
- `400 Bad Request` - Invalid role
- `401 Unauthorized` - Missing or invalid JWT token

---

### 6. Create User

**Endpoint:** `POST /users`

**Authentication:** Protected

**Description:** Create a new user account

**Request Body:**
```json
{
  "email": "jane.smith@example.com",
  "firstName": "Jane",
  "lastName": "Smith",
  "role": "Student",
  "phoneNumber": "+1234567890",
  "address": "456 Oak Ave, Atlanta, GA"
}
```

**Validation Rules:**
- `email`: Required, valid email format, max 255 characters
- `firstName`: Required, max 100 characters
- `lastName`: Required, max 100 characters
- `role`: Required, must be "Student", "Seller", or "Admin"
- `phoneNumber`: Optional, max 20 characters
- `address`: Optional, max 500 characters

**Response (201 Created):**
```json
{
  "userId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "jane.smith@example.com",
  "firstName": "Jane",
  "lastName": "Smith",
  "role": "Student",
  "phoneNumber": "+1234567890",
  "address": "456 Oak Ave, Atlanta, GA",
  "createdDate": "2024-01-20T10:30:00Z",
  "isActive": true
}
```

**Status Codes:**
- `201 Created` - User created successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token
- `409 Conflict` - User with email already exists

---

### 7. Update User

**Endpoint:** `PUT /users/{userId}`

**Authentication:** Protected

**Description:** Update an existing user's information

**Path Parameters:**
- `userId`: User's unique identifier (GUID)

**Request Body:**
```json
{
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "address": "789 Pine St, Atlanta, GA"
}
```

**Validation Rules:**
- `firstName`: Optional, max 100 characters
- `lastName`: Optional, max 100 characters
- `phoneNumber`: Optional, max 20 characters
- `address`: Optional, max 500 characters

**Response (200 OK):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "role": "Student",
  "phoneNumber": "+1234567890",
  "address": "789 Pine St, Atlanta, GA",
  "lastModifiedDate": "2024-01-21T15:30:00Z",
  "isActive": true
}
```

**Status Codes:**
- `200 OK` - User updated successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - User not found
- `409 Conflict` - Email already in use

---

### 8. Delete User

**Endpoint:** `DELETE /users/{userId}`

**Authentication:** Protected

**Description:** Soft delete a user (marks as inactive)

**Path Parameters:**
- `userId`: User's unique identifier (GUID)

**Example Request:**
```
DELETE /users/3fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - User deleted successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - User not found

---

### 9. Change User Role

**Endpoint:** `PUT /users/{userId}/role`

**Authentication:** Protected (Admin only)

**Description:** Change a user's role

**Path Parameters:**
- `userId`: User's unique identifier (GUID)

**Request Body:**
```json
{
  "role": "Seller"
}
```

**Validation Rules:**
- `role`: Required, must be "Student", "Seller", or "Admin"

**Response (200 OK):**
```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "john.doe@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "role": "Seller",
  "lastModifiedDate": "2024-01-21T16:00:00Z"
}
```

**Status Codes:**
- `200 OK` - Role changed successfully
- `400 Bad Request` - Invalid role
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - User not found

---

### 10. Export User Data (GDPR)

**Endpoint:** `GET /users/{userId}/export`

**Authentication:** Protected

**Description:** Export all user data for GDPR compliance

**Path Parameters:**
- `userId`: User's unique identifier (GUID)

**Example Request:**
```
GET /users/3fa85f64-5717-4562-b3fc-2c963f66afa6/export
```

**Response (200 OK):**
```json
{
  "user": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "john.doe@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "role": "Student",
    "createdDate": "2024-01-15T10:30:00Z"
  },
  "orders": [],
  "notifications": [],
  "exportDate": "2024-01-21T17:00:00Z"
}
```

**Status Codes:**
- `200 OK` - Data exported successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - User not found

---

### 11. Anonymize User (GDPR)

**Endpoint:** `POST /users/{userId}/anonymize`

**Authentication:** Protected

**Description:** Anonymize user data for GDPR right to be forgotten

**Path Parameters:**
- `userId`: User's unique identifier (GUID)

**Example Request:**
```
POST /users/3fa85f64-5717-4562-b3fc-2c963f66afa6/anonymize
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - User anonymized successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - User not found

---

### 12. Get Role Statistics

**Endpoint:** `GET /users/statistics/roles`

**Authentication:** Protected

**Description:** Get count of users by role

**Example Request:**
```
GET /users/statistics/roles
```

**Response (200 OK):**
```json
{
  "Student": 150,
  "Seller": 45,
  "Admin": 5
}
```

**Status Codes:**
- `200 OK` - Statistics retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

## Search & Discovery Endpoints

**Base Path:** `/search`

**Authentication:** Public (no JWT required for GET requests)

### 1. Search Books

**Endpoint:** `GET /search`

**Authentication:** Not required

**Description:** Search for books by title, author, or ISBN with pagination and sorting

**Query Parameters:**
- `query` (required): Search term
- `page` (optional): Page number, default: 1
- `pageSize` (optional): Items per page (1-100), default: 20
- `sortBy` (optional): Sort field (relevance, title, price, rating), default: relevance

**Example Request:**
```
GET /search?query=python&page=1&pageSize=20&sortBy=relevance
```

**Response (200 OK):**
```json
{
  "books": [
    {
      "isbn": "9780134685991",
      "title": "Effective Python",
      "author": "Brett Slatkin",
      "publisher": "Addison-Wesley",
      "yearOfPublication": 2019,
      "genre": "Programming",
      "language": "English",
      "pageCount": 352,
      "rating": 4.5,
      "imageUrlS": "http://example.com/small.jpg",
      "imageUrlM": "http://example.com/medium.jpg",
      "imageUrlL": "http://example.com/large.jpg",
      "lowestPrice": 29.99,
      "availableCount": 15
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 45,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false,
  "searchTerm": "python",
  "executionTimeMs": 45
}
```

**Status Codes:**
- `200 OK` - Search completed successfully
- `400 Bad Request` - Missing or invalid query parameter

---

### 2. Get Available Books

**Endpoint:** `GET /search/available`

**Authentication:** Not required

**Description:** Get all available books with pagination and sorting

**Query Parameters:**
- `page` (optional): Page number, default: 1
- `pageSize` (optional): Items per page, default: 20
- `sortBy` (optional): Sort field (price, title, rating)
- `sortOrder` (optional): Sort order (asc, desc), default: asc

**Example Request:**
```
GET /search/available?page=1&pageSize=20&sortBy=price&sortOrder=asc
```

**Response (200 OK):**
```json
{
  "books": [
    {
      "isbn": "9780134685991",
      "title": "Effective Python",
      "author": "Brett Slatkin",
      "lowestPrice": 29.99,
      "availableCount": 15
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 500,
  "totalPages": 25,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

**Status Codes:**
- `200 OK` - Books retrieved successfully

---

### 3. Get Featured Books

**Endpoint:** `GET /search/featured`

**Authentication:** Not required

**Description:** Get featured/recommended books

**Query Parameters:**
- `count` (optional): Number of books to return, default: 10

**Example Request:**
```
GET /search/featured?count=10
```

**Response (200 OK):**
```json
{
  "books": [
    {
      "isbn": "9780134685991",
      "title": "Effective Python",
      "author": "Brett Slatkin",
      "rating": 4.5,
      "lowestPrice": 29.99
    }
  ],
  "count": 10
}
```

**Status Codes:**
- `200 OK` - Featured books retrieved successfully

---

### 4. Get Book Sellers

**Endpoint:** `GET /search/sellers/{isbn}`

**Authentication:** Not required

**Description:** Get all sellers offering a specific book with prices and stock

**Path Parameters:**
- `isbn`: Book's ISBN

**Example Request:**
```
GET /search/sellers/9780134685991
```

**Response (200 OK):**
```json
{
  "isbn": "9780134685991",
  "sellers": [
    {
      "sellerId": "seller123",
      "sellerName": "Campus Bookstore",
      "price": 29.99,
      "quantity": 15,
      "condition": "New",
      "location": "Building A"
    },
    {
      "sellerId": "seller456",
      "sellerName": "Student Books",
      "price": 24.99,
      "quantity": 5,
      "condition": "Used",
      "location": "Building B"
    }
  ],
  "totalSellers": 2
}
```

**Status Codes:**
- `200 OK` - Sellers retrieved successfully
- `404 Not Found` - Book not found

---

### 5. Search Health Check

**Endpoint:** `GET /search/health`

**Authentication:** Not required

**Description:** Health check for search service

**Example Request:**
```
GET /search/health
```

**Response (200 OK):**
```json
{
  "status": "healthy",
  "timestamp": "2024-01-21T18:00:00Z",
  "service": "SearchService"
}
```

**Status Codes:**
- `200 OK` - Service is healthy

---

### 6. Get Search Statistics

**Endpoint:** `GET /search/stats`

**Authentication:** Not required

**Description:** Get search service statistics

**Example Request:**
```
GET /search/stats
```

**Response (200 OK):**
```json
{
  "totalBooks": 50000,
  "totalSearches": 125000,
  "averageSearchTimeMs": 45,
  "cacheHitRate": 0.85,
  "lastIndexUpdate": "2024-01-21T12:00:00Z"
}
```

**Status Codes:**
- `200 OK` - Statistics retrieved successfully

---

### 7. Get Autocomplete Suggestions

**Endpoint:** `GET /search/autocomplete`

**Authentication:** Not required

**Description:** Get autocomplete suggestions for search (ultra-fast typeahead)

**Query Parameters:**
- `prefix` (required): Search prefix (minimum 2 characters)
- `maxResults` (optional): Maximum suggestions (1-50), default: 10

**Example Request:**
```
GET /search/autocomplete?prefix=pyth&maxResults=10
```

**Response (200 OK):**
```json
{
  "suggestions": [
    {
      "text": "Python Programming",
      "type": "Title",
      "score": 0.95
    },
    {
      "text": "Python for Data Science",
      "type": "Title",
      "score": 0.89
    },
    {
      "text": "Mark Lutz",
      "type": "Author",
      "score": 0.75
    }
  ],
  "prefix": "pyth",
  "count": 3,
  "executionTimeMs": 5
}
```

**Status Codes:**
- `200 OK` - Suggestions retrieved successfully
- `400 Bad Request` - Invalid prefix (too short)

---

### 8. Get Search Facets

**Endpoint:** `GET /search/facets`

**Authentication:** Not required

**Description:** Get available facets for filtering search results

**Query Parameters:**
- `searchTerm` (optional): Get facets for specific search results

**Example Request:**
```
GET /search/facets?searchTerm=python
```

**Response (200 OK):**
```json
{
  "facets": {
    "genres": [
      { "value": "Programming", "count": 150 },
      { "value": "Data Science", "count": 75 }
    ],
    "languages": [
      { "value": "English", "count": 200 },
      { "value": "Spanish", "count": 25 }
    ],
    "priceRanges": [
      { "range": "0-20", "count": 50 },
      { "range": "20-40", "count": 100 },
      { "range": "40+", "count": 75 }
    ],
    "publishers": [
      { "value": "O'Reilly", "count": 45 },
      { "value": "Addison-Wesley", "count": 30 }
    ]
  }
}
```

**Status Codes:**
- `200 OK` - Facets retrieved successfully

---

### 9. Advanced Search with Filters

**Endpoint:** `POST /search/advanced`

**Authentication:** Not required

**Description:** Advanced search with multiple filters (faceted search)

**Request Body:**
```json
{
  "searchTerm": "python",
  "genres": ["Programming", "Data Science"],
  "languages": ["English"],
  "minPrice": 0,
  "maxPrice": 50,
  "minRating": 4.0,
  "publishers": ["O'Reilly"],
  "yearFrom": 2018,
  "yearTo": 2024,
  "page": 1,
  "pageSize": 20,
  "sortBy": "relevance"
}
```

**Response (200 OK):**
```json
{
  "books": [
    {
      "isbn": "9780134685991",
      "title": "Effective Python",
      "author": "Brett Slatkin",
      "genre": "Programming",
      "language": "English",
      "rating": 4.5,
      "lowestPrice": 29.99
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 35,
  "totalPages": 2,
  "appliedFilters": {
    "genres": ["Programming", "Data Science"],
    "priceRange": "0-50",
    "minRating": 4.0
  }
}
```

**Status Codes:**
- `200 OK` - Search completed successfully
- `400 Bad Request` - Invalid filter parameters

---

### 10. Get Popular Searches

**Endpoint:** `GET /search/popular`

**Authentication:** Not required

**Description:** Get popular/trending searches

**Query Parameters:**
- `topN` (optional): Number of top searches, default: 10
- `timeWindow` (optional): Time window (24h, all), default: 24h

**Example Request:**
```
GET /search/popular?topN=10&timeWindow=24h
```

**Response (200 OK):**
```json
{
  "searches": [
    {
      "searchTerm": "python programming",
      "count": 450,
      "rank": 1
    },
    {
      "searchTerm": "data structures",
      "count": 320,
      "rank": 2
    }
  ],
  "timeWindow": "24h",
  "generatedAt": "2024-01-21T18:00:00Z"
}
```

**Status Codes:**
- `200 OK` - Popular searches retrieved successfully

---

## Book Management Endpoints

**Base Path:** `/books`

**Authentication:** Public for GET, Protected for POST/PUT/DELETE

### 1. Get All Books

**Endpoint:** `GET /books`

**Authentication:** Not required

**Description:** Retrieve all books in the catalog

**Example Request:**
```
GET /books
```

**Response (200 OK):**
```json
[
  {
    "isbn": "9780134685991",
    "bookTitle": "Effective Python",
    "bookAuthor": "Brett Slatkin",
    "yearOfPublication": 2019,
    "publisher": "Addison-Wesley",
    "imageUrlS": "http://example.com/small.jpg",
    "imageUrlM": "http://example.com/medium.jpg",
    "imageUrlL": "http://example.com/large.jpg",
    "genre": "Programming",
    "language": "English",
    "pageCount": 352,
    "description": "Best practices for Python development",
    "rating": 4.5,
    "availabilityStatus": "Available",
    "edition": "2nd",
    "format": "Paperback"
  }
]
```

**Status Codes:**
- `200 OK` - Books retrieved successfully

---

### 2. Get Book by ISBN

**Endpoint:** `GET /books/{isbn}`

**Authentication:** Not required

**Description:** Retrieve a specific book by ISBN

**Path Parameters:**
- `isbn`: Book's ISBN (10 or 13 digits)

**Example Request:**
```
GET /books/9780134685991
```

**Response (200 OK):**
```json
{
  "isbn": "9780134685991",
  "bookTitle": "Effective Python",
  "bookAuthor": "Brett Slatkin",
  "yearOfPublication": 2019,
  "publisher": "Addison-Wesley",
  "imageUrlS": "http://example.com/small.jpg",
  "imageUrlM": "http://example.com/medium.jpg",
  "imageUrlL": "http://example.com/large.jpg",
  "genre": "Programming",
  "language": "English",
  "pageCount": 352,
  "description": "Best practices for Python development",
  "rating": 4.5,
  "availabilityStatus": "Available",
  "edition": "2nd",
  "format": "Paperback"
}
```

**Status Codes:**
- `200 OK` - Book retrieved successfully
- `404 Not Found` - Book not found

---

### 3. Create Book

**Endpoint:** `POST /books`

**Authentication:** Protected

**Description:** Add a new book to the catalog

**Request Body:**
```json
{
  "isbn": "9780134685991",
  "bookTitle": "Effective Python",
  "bookAuthor": "Brett Slatkin",
  "yearOfPublication": 2019,
  "publisher": "Addison-Wesley",
  "imageUrlS": "http://example.com/small.jpg",
  "imageUrlM": "http://example.com/medium.jpg",
  "imageUrlL": "http://example.com/large.jpg",
  "genre": "Programming",
  "language": "English",
  "pageCount": 352,
  "description": "Best practices for Python development",
  "rating": 4.5,
  "availabilityStatus": "Available",
  "edition": "2nd",
  "format": "Paperback"
}
```

**Validation Rules:**
- `isbn`: Required, 10 or 13 digits
- `bookTitle`: Required, max 500 characters
- `bookAuthor`: Required, max 255 characters
- `yearOfPublication`: Required, valid year
- `publisher`: Required, max 255 characters

**Response (201 Created):**
```json
{
  "isbn": "9780134685991",
  "bookTitle": "Effective Python",
  "bookAuthor": "Brett Slatkin",
  "yearOfPublication": 2019,
  "publisher": "Addison-Wesley"
}
```

**Status Codes:**
- `201 Created` - Book created successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token
- `409 Conflict` - Book with ISBN already exists

---

### 4. Update Book

**Endpoint:** `PUT /books/{isbn}`

**Authentication:** Protected

**Description:** Update an existing book's information

**Path Parameters:**
- `isbn`: Book's ISBN

**Request Body:**
```json
{
  "bookTitle": "Effective Python (Updated)",
  "bookAuthor": "Brett Slatkin",
  "yearOfPublication": 2019,
  "publisher": "Addison-Wesley",
  "genre": "Programming",
  "language": "English",
  "pageCount": 352,
  "description": "Updated description",
  "rating": 4.6,
  "edition": "2nd",
  "format": "Paperback"
}
```

**Response (200 OK):**
```json
{
  "isbn": "9780134685991",
  "bookTitle": "Effective Python (Updated)",
  "bookAuthor": "Brett Slatkin",
  "yearOfPublication": 2019,
  "publisher": "Addison-Wesley",
  "rating": 4.6
}
```

**Status Codes:**
- `200 OK` - Book updated successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Book not found

---

### 5. Delete Book

**Endpoint:** `DELETE /books/{isbn}`

**Authentication:** Protected

**Description:** Remove a book from the catalog

**Path Parameters:**
- `isbn`: Book's ISBN

**Example Request:**
```
DELETE /books/9780134685991
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - Book deleted successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Book not found

---

### 6. Sync Book Events

**Endpoint:** `POST /books/sync-events`

**Authentication:** Protected

**Description:** Synchronize book data with other services via RabbitMQ events

**Query Parameters:**
- `skip` (optional): Number of books to skip, default: 0

**Example Request:**
```
POST /books/sync-events?skip=0
```

**Response (200 OK):**
```json
1500
```

**Status Codes:**
- `200 OK` - Sync completed, returns count of synced books
- `401 Unauthorized` - Missing or invalid JWT token

---

## Warehouse Management Endpoints

**Base Path:** `/warehouse`

**Authentication:** Protected (all endpoints require JWT)

### 1. Get All Warehouse Items

**Endpoint:** `GET /warehouse/items`

**Authentication:** Protected

**Description:** Retrieve all warehouse inventory items

**Example Request:**
```
GET /warehouse/items
```

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "bookISBN": "9780134685991",
    "sellerId": "seller123",
    "quantity": 15,
    "price": 29.99,
    "isNew": true,
    "location": "Building A, Shelf 12",
    "createdDate": "2024-01-15T10:00:00Z",
    "lastModifiedDate": "2024-01-20T14:30:00Z"
  }
]
```

**Status Codes:**
- `200 OK` - Items retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 2. Get Warehouse Items by Book ISBN

**Endpoint:** `GET /warehouse/items/id/{bookIsbn}`

**Authentication:** Protected

**Description:** Get all warehouse items for a specific book

**Path Parameters:**
- `bookIsbn`: Book's ISBN

**Example Request:**
```
GET /warehouse/items/id/9780134685991
```

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "bookISBN": "9780134685991",
    "sellerId": "seller123",
    "quantity": 15,
    "price": 29.99,
    "isNew": true,
    "location": "Building A, Shelf 12"
  },
  {
    "id": 2,
    "bookISBN": "9780134685991",
    "sellerId": "seller456",
    "quantity": 5,
    "price": 24.99,
    "isNew": false,
    "location": "Building B, Shelf 5"
  }
]
```

**Status Codes:**
- `200 OK` - Items retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 3. Get Warehouse Items by Seller

**Endpoint:** `GET /warehouse/sellers/{sellerId}/items`

**Authentication:** Protected

**Description:** Get all inventory items for a specific seller

**Path Parameters:**
- `sellerId`: Seller's unique identifier

**Example Request:**
```
GET /warehouse/sellers/seller123/items
```

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "bookISBN": "9780134685991",
    "sellerId": "seller123",
    "quantity": 15,
    "price": 29.99,
    "isNew": true,
    "location": "Building A, Shelf 12"
  }
]
```

**Status Codes:**
- `200 OK` - Items retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 4. Get New Books

**Endpoint:** `GET /warehouse/items/new`

**Authentication:** Protected

**Description:** Get all new (not used) books in warehouse

**Example Request:**
```
GET /warehouse/items/new
```

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "bookISBN": "9780134685991",
    "sellerId": "seller123",
    "quantity": 15,
    "price": 29.99,
    "isNew": true,
    "location": "Building A, Shelf 12"
  }
]
```

**Status Codes:**
- `200 OK` - Items retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 5. Get Used Books

**Endpoint:** `GET /warehouse/items/used`

**Authentication:** Protected

**Description:** Get all used books in warehouse

**Example Request:**
```
GET /warehouse/items/used
```

**Response (200 OK):**
```json
[
  {
    "id": 2,
    "bookISBN": "9780134685991",
    "sellerId": "seller456",
    "quantity": 5,
    "price": 24.99,
    "isNew": false,
    "location": "Building B, Shelf 5"
  }
]
```

**Status Codes:**
- `200 OK` - Items retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 6. Create Warehouse Item

**Endpoint:** `POST /warehouse/items`

**Authentication:** Protected

**Description:** Add a new item to warehouse inventory

**Request Body:**
```json
{
  "bookISBN": "9780134685991",
  "sellerId": "seller123",
  "quantity": 15,
  "price": 29.99,
  "isNew": true,
  "location": "Building A, Shelf 12"
}
```

**Validation Rules:**
- `bookISBN`: Required, valid ISBN
- `sellerId`: Required, max 100 characters
- `quantity`: Required, minimum 0
- `price`: Required, minimum 0.01
- `isNew`: Required, boolean
- `location`: Optional, max 200 characters

**Response (201 Created):**
```json
{
  "id": 1,
  "bookISBN": "9780134685991",
  "sellerId": "seller123",
  "quantity": 15,
  "price": 29.99,
  "isNew": true,
  "location": "Building A, Shelf 12",
  "createdDate": "2024-01-21T10:00:00Z"
}
```

**Status Codes:**
- `201 Created` - Item created successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token
- `409 Conflict` - Item already exists for this book and seller

---

### 7. Update Warehouse Item

**Endpoint:** `PUT /warehouse/items/{id}`

**Authentication:** Protected

**Description:** Update an existing warehouse item

**Path Parameters:**
- `id`: Warehouse item ID

**Request Body:**
```json
{
  "quantity": 20,
  "price": 27.99,
  "isNew": true,
  "location": "Building A, Shelf 15"
}
```

**Response (200 OK):**
```json
{
  "id": 1,
  "bookISBN": "9780134685991",
  "sellerId": "seller123",
  "quantity": 20,
  "price": 27.99,
  "isNew": true,
  "location": "Building A, Shelf 15",
  "lastModifiedDate": "2024-01-21T11:00:00Z"
}
```

**Status Codes:**
- `200 OK` - Item updated successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Item not found

---

### 8. Adjust Stock

**Endpoint:** `POST /warehouse/adjust-stock`

**Authentication:** Protected

**Description:** Adjust stock quantity for a warehouse item

**Request Body:**
```json
{
  "bookISBN": "9780134685991",
  "sellerId": "seller123",
  "quantityChange": -5
}
```

**Validation Rules:**
- `bookISBN`: Required
- `sellerId`: Required
- `quantityChange`: Required, can be positive or negative

**Response (200 OK):**
```json
{
  "message": "Stock adjusted successfully",
  "newQuantity": 10
}
```

**Status Codes:**
- `200 OK` - Stock adjusted successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Item not found

---

### 9. Get Warehouse Item by ID

**Endpoint:** `GET /warehouse/items/{id}`

**Authentication:** Protected

**Description:** Retrieve a specific warehouse item by ID

**Path Parameters:**
- `id`: Warehouse item ID

**Example Request:**
```
GET /warehouse/items/1
```

**Response (200 OK):**
```json
{
  "id": 1,
  "bookISBN": "9780134685991",
  "sellerId": "seller123",
  "quantity": 15,
  "price": 29.99,
  "isNew": true,
  "location": "Building A, Shelf 12"
}
```

**Status Codes:**
- `200 OK` - Item retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Item not found

---

### 10. Delete Warehouse Item

**Endpoint:** `DELETE /warehouse/items/{id}`

**Authentication:** Protected

**Description:** Remove an item from warehouse inventory

**Path Parameters:**
- `id`: Warehouse item ID

**Example Request:**
```
DELETE /warehouse/items/1
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - Item deleted successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Item not found

---

### 11. Sync Warehouse Events

**Endpoint:** `POST /warehouse/sync-events`

**Authentication:** Protected

**Description:** Synchronize warehouse data with other services via RabbitMQ events

**Example Request:**
```
POST /warehouse/sync-events
```

**Response (200 OK):**
```json
2500
```

**Status Codes:**
- `200 OK` - Sync completed, returns count of synced items
- `401 Unauthorized` - Missing or invalid JWT token

---

## Shopping Cart Endpoints

**Base Path:** `/cart`

**Authentication:** Protected (all endpoints require JWT)

### 1. Get Shopping Cart

**Endpoint:** `GET /cart/{customerId}`

**Authentication:** Protected

**Description:** Retrieve the shopping cart for a customer

**Path Parameters:**
- `customerId`: Customer's unique identifier

**Example Request:**
```
GET /cart/customer123
```

**Response (200 OK):**
```json
{
  "shoppingCartId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "createdDate": "2024-01-20T10:00:00Z",
  "updatedDate": "2024-01-21T14:30:00Z",
  "totalAmount": 79.97,
  "itemCount": 3,
  "items": [
    {
      "cartItemId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
      "bookISBN": "9780134685991",
      "sellerId": "seller123",
      "quantity": 2,
      "unitPrice": 29.99,
      "totalPrice": 59.98,
      "addedDate": "2024-01-20T10:00:00Z",
      "updatedDate": "2024-01-21T14:30:00Z"
    },
    {
      "cartItemId": "2fa85f64-5717-4562-b3fc-2c963f66afa6",
      "bookISBN": "9781491950357",
      "sellerId": "seller456",
      "quantity": 1,
      "unitPrice": 19.99,
      "totalPrice": 19.99,
      "addedDate": "2024-01-21T12:00:00Z"
    }
  ]
}
```

**Status Codes:**
- `200 OK` - Cart retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 2. Add Item to Cart

**Endpoint:** `POST /cart/{customerId}/items`

**Authentication:** Protected

**Description:** Add a book to the shopping cart

**Path Parameters:**
- `customerId`: Customer's unique identifier

**Request Body:**
```json
{
  "bookISBN": "9780134685991",
  "sellerId": "seller123",
  "quantity": 2,
  "unitPrice": 29.99
}
```

**Validation Rules:**
- `bookISBN`: Required, 10-13 characters
- `sellerId`: Required, max 100 characters
- `quantity`: Required, 1-1000
- `unitPrice`: Required, 0.01-10000

**Response (200 OK):**
```json
{
  "shoppingCartId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "totalAmount": 59.98,
  "itemCount": 1,
  "items": [
    {
      "cartItemId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
      "bookISBN": "9780134685991",
      "sellerId": "seller123",
      "quantity": 2,
      "unitPrice": 29.99,
      "totalPrice": 59.98,
      "addedDate": "2024-01-21T15:00:00Z"
    }
  ]
}
```

**Status Codes:**
- `200 OK` - Item added successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token

---

### 3. Update Cart Item Quantity

**Endpoint:** `PUT /cart/{customerId}/items/{cartItemId}`

**Authentication:** Protected

**Description:** Update the quantity of an item in the cart

**Path Parameters:**
- `customerId`: Customer's unique identifier
- `cartItemId`: Cart item's unique identifier (GUID)

**Request Body:**
```json
{
  "quantity": 3
}
```

**Validation Rules:**
- `quantity`: Required, 1-1000

**Response (200 OK):**
```json
{
  "shoppingCartId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "totalAmount": 89.97,
  "itemCount": 1,
  "items": [
    {
      "cartItemId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
      "bookISBN": "9780134685991",
      "sellerId": "seller123",
      "quantity": 3,
      "unitPrice": 29.99,
      "totalPrice": 89.97,
      "updatedDate": "2024-01-21T15:30:00Z"
    }
  ]
}
```

**Status Codes:**
- `200 OK` - Quantity updated successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Cart item not found

---

### 4. Remove Item from Cart

**Endpoint:** `DELETE /cart/{customerId}/items/{cartItemId}`

**Authentication:** Protected

**Description:** Remove an item from the shopping cart

**Path Parameters:**
- `customerId`: Customer's unique identifier
- `cartItemId`: Cart item's unique identifier (GUID)

**Example Request:**
```
DELETE /cart/customer123/items/1fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Response (200 OK):**
```json
{
  "shoppingCartId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "totalAmount": 0.00,
  "itemCount": 0,
  "items": []
}
```

**Status Codes:**
- `200 OK` - Item removed successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Cart item not found

---

### 5. Clear Cart

**Endpoint:** `DELETE /cart/{customerId}`

**Authentication:** Protected

**Description:** Remove all items from the shopping cart

**Path Parameters:**
- `customerId`: Customer's unique identifier

**Example Request:**
```
DELETE /cart/customer123
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - Cart cleared successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 6. Checkout (Convert Cart to Order)

**Endpoint:** `POST /cart/{customerId}/checkout`

**Authentication:** Protected

**Description:** Convert the shopping cart to an order

**Path Parameters:**
- `customerId`: Customer's unique identifier

**Example Request:**
```
POST /cart/customer123/checkout
```

**Response (201 Created):**
```json
{
  "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "orderDate": "2024-01-21T16:00:00Z",
  "totalAmount": 89.97,
  "status": "Pending",
  "orderItems": [
    {
      "orderItemId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
      "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
      "bookISBN": "9780134685991",
      "sellerId": "seller123",
      "quantity": 3,
      "unitPrice": 29.99,
      "status": "Pending"
    }
  ]
}
```

**Status Codes:**
- `201 Created` - Order created successfully
- `400 Bad Request` - Cart is empty or invalid
- `401 Unauthorized` - Missing or invalid JWT token

---

## Order Management Endpoints

**Base Path:** `/orders`

**Authentication:** Protected (all endpoints require JWT)

### 1. Create Order

**Endpoint:** `POST /orders`

**Authentication:** Protected

**Description:** Create a new order directly (without using cart)

**Request Body:**
```json
{
  "customerId": "customer123",
  "orderItems": [
    {
      "bookISBN": "9780134685991",
      "sellerId": "seller123",
      "quantity": 2,
      "unitPrice": 29.99
    }
  ]
}
```

**Validation Rules:**
- `customerId`: Required, max 100 characters
- `orderItems`: Required, minimum 1 item
- Each item requires: `bookISBN`, `sellerId`, `quantity` (1-1000), `unitPrice` (0.01-10000)

**Response (201 Created):**
```json
{
  "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "orderDate": "2024-01-21T16:00:00Z",
  "totalAmount": 59.98,
  "status": "Pending",
  "orderItems": [
    {
      "orderItemId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
      "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
      "bookISBN": "9780134685991",
      "sellerId": "seller123",
      "quantity": 2,
      "unitPrice": 29.99,
      "status": "Pending"
    }
  ]
}
```

**Status Codes:**
- `201 Created` - Order created successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token

---

### 2. Get Order by ID

**Endpoint:** `GET /orders/{orderId}`

**Authentication:** Protected

**Description:** Retrieve a specific order by ID

**Path Parameters:**
- `orderId`: Order's unique identifier (GUID)

**Example Request:**
```
GET /orders/9fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Response (200 OK):**
```json
{
  "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "orderDate": "2024-01-21T16:00:00Z",
  "totalAmount": 59.98,
  "status": "Paid",
  "paidDate": "2024-01-21T16:15:00Z",
  "shippedDate": null,
  "deliveredDate": null,
  "cancelledDate": null,
  "refundedDate": null,
  "cancellationReason": null,
  "refundReason": null,
  "orderItems": [
    {
      "orderItemId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
      "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
      "bookISBN": "9780134685991",
      "sellerId": "seller123",
      "quantity": 2,
      "unitPrice": 29.99,
      "status": "Paid"
    }
  ]
}
```

**Status Codes:**
- `200 OK` - Order retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Order not found

---

### 3. Get Customer Orders

**Endpoint:** `GET /orders/customer/{customerId}`

**Authentication:** Protected

**Description:** Get all orders for a specific customer (paginated)

**Path Parameters:**
- `customerId`: Customer's unique identifier

**Query Parameters:**
- `page` (optional): Page number, default: 1
- `pageSize` (optional): Items per page, default: 10

**Example Request:**
```
GET /orders/customer/customer123?page=1&pageSize=10
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
      "customerId": "customer123",
      "orderDate": "2024-01-21T16:00:00Z",
      "totalAmount": 59.98,
      "status": "Delivered"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 25,
  "totalPages": 3
}
```

**Status Codes:**
- `200 OK` - Orders retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 4. Get All Orders

**Endpoint:** `GET /orders`

**Authentication:** Protected (Admin only)

**Description:** Get all orders in the system (paginated)

**Query Parameters:**
- `page` (optional): Page number, default: 1
- `pageSize` (optional): Items per page, default: 10

**Example Request:**
```
GET /orders?page=1&pageSize=10
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
      "customerId": "customer123",
      "orderDate": "2024-01-21T16:00:00Z",
      "totalAmount": 59.98,
      "status": "Delivered"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1500,
  "totalPages": 150
}
```

**Status Codes:**
- `200 OK` - Orders retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 5. Get Orders by Status

**Endpoint:** `GET /orders/status/{status}`

**Authentication:** Protected

**Description:** Get orders filtered by status (paginated)

**Path Parameters:**
- `status`: Order status (Pending, Paid, Shipped, Delivered, Cancelled, Refunded)

**Query Parameters:**
- `page` (optional): Page number, default: 1
- `pageSize` (optional): Items per page, default: 10

**Example Request:**
```
GET /orders/status/Pending?page=1&pageSize=10
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
      "customerId": "customer123",
      "orderDate": "2024-01-21T16:00:00Z",
      "totalAmount": 59.98,
      "status": "Pending"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 45,
  "totalPages": 5
}
```

**Status Codes:**
- `200 OK` - Orders retrieved successfully
- `400 Bad Request` - Invalid status
- `401 Unauthorized` - Missing or invalid JWT token

---

### 6. Pay Order

**Endpoint:** `POST /orders/{orderId}/pay`

**Authentication:** Protected

**Description:** Process payment for an order

**Path Parameters:**
- `orderId`: Order's unique identifier (GUID)

**Request Body:**
```json
{
  "paymentMethod": "CreditCard",
  "paymentReference": "PAY-123456789"
}
```

**Response (200 OK):**
```json
{
  "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "orderDate": "2024-01-21T16:00:00Z",
  "totalAmount": 59.98,
  "status": "Paid",
  "paidDate": "2024-01-21T16:30:00Z"
}
```

**Status Codes:**
- `200 OK` - Payment processed successfully
- `400 Bad Request` - Invalid payment data or order cannot be paid
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Order not found

---

### 7. Ship Order

**Endpoint:** `POST /orders/{orderId}/ship`

**Authentication:** Protected

**Description:** Mark an order as shipped

**Path Parameters:**
- `orderId`: Order's unique identifier (GUID)

**Example Request:**
```
POST /orders/9fa85f64-5717-4562-b3fc-2c963f66afa6/ship
```

**Response (200 OK):**
```json
{
  "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "status": "Shipped",
  "shippedDate": "2024-01-22T10:00:00Z"
}
```

**Status Codes:**
- `200 OK` - Order marked as shipped
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Order not found

---

### 8. Deliver Order

**Endpoint:** `POST /orders/{orderId}/deliver`

**Authentication:** Protected

**Description:** Mark an order as delivered

**Path Parameters:**
- `orderId`: Order's unique identifier (GUID)

**Example Request:**
```
POST /orders/9fa85f64-5717-4562-b3fc-2c963f66afa6/deliver
```

**Response (200 OK):**
```json
{
  "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "status": "Delivered",
  "deliveredDate": "2024-01-25T14:30:00Z"
}
```

**Status Codes:**
- `200 OK` - Order marked as delivered
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Order not found

---

### 9. Cancel Order

**Endpoint:** `POST /orders/{orderId}/cancel`

**Authentication:** Protected

**Description:** Cancel an order

**Path Parameters:**
- `orderId`: Order's unique identifier (GUID)

**Request Body:**
```json
{
  "reason": "Customer requested cancellation"
}
```

**Response (200 OK):**
```json
{
  "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "status": "Cancelled",
  "cancelledDate": "2024-01-21T17:00:00Z",
  "cancellationReason": "Customer requested cancellation"
}
```

**Status Codes:**
- `200 OK` - Order cancelled successfully
- `400 Bad Request` - Order cannot be cancelled (already shipped/delivered)
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Order not found

---

### 10. Refund Order

**Endpoint:** `POST /orders/{orderId}/refund`

**Authentication:** Protected

**Description:** Process a refund for an order

**Path Parameters:**
- `orderId`: Order's unique identifier (GUID)

**Request Body:**
```json
{
  "reason": "Defective product"
}
```

**Response (200 OK):**
```json
{
  "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
  "customerId": "customer123",
  "status": "Refunded",
  "refundedDate": "2024-01-26T10:00:00Z",
  "refundReason": "Defective product"
}
```

**Status Codes:**
- `200 OK` - Refund processed successfully
- `400 Bad Request` - Order cannot be refunded
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Order not found

---

## Notification Endpoints

**Base Path:** `/notifications`

**Authentication:** Protected (all endpoints require JWT)

### 1. Get Notification by ID

**Endpoint:** `GET /notifications/{notificationId}`

**Authentication:** Protected

**Description:** Retrieve a specific notification by ID

**Path Parameters:**
- `notificationId`: Notification's unique identifier (GUID)

**Example Request:**
```
GET /notifications/5fa85f64-5717-4562-b3fc-2c963f66afa6
```

**Response (200 OK):**
```json
{
  "notificationId": "5fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "user123",
  "type": "OrderShipped",
  "title": "Your order has been shipped",
  "message": "Your order #9fa85f64 has been shipped and is on its way.",
  "isRead": false,
  "createdDate": "2024-01-22T10:00:00Z",
  "readDate": null,
  "status": "Sent",
  "metadata": {
    "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
}
```

**Status Codes:**
- `200 OK` - Notification retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Notification not found

---

### 2. Get User Notifications

**Endpoint:** `GET /notifications/user/{userId}`

**Authentication:** Protected

**Description:** Get all notifications for a user (paginated)

**Path Parameters:**
- `userId`: User's unique identifier

**Query Parameters:**
- `page` (optional): Page number, default: 1
- `pageSize` (optional): Items per page, default: 10

**Example Request:**
```
GET /notifications/user/user123?page=1&pageSize=10
```

**Response (200 OK):**
```json
{
  "items": [
    {
      "notificationId": "5fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userId": "user123",
      "type": "OrderShipped",
      "title": "Your order has been shipped",
      "message": "Your order #9fa85f64 has been shipped.",
      "isRead": false,
      "createdDate": "2024-01-22T10:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 25,
  "totalPages": 3,
  "unreadCount": 5
}
```

**Status Codes:**
- `200 OK` - Notifications retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 3. Get Unread Count

**Endpoint:** `GET /notifications/user/{userId}/unread-count`

**Authentication:** Protected

**Description:** Get the count of unread notifications for a user

**Path Parameters:**
- `userId`: User's unique identifier

**Example Request:**
```
GET /notifications/user/user123/unread-count
```

**Response (200 OK):**
```json
5
```

**Status Codes:**
- `200 OK` - Count retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 4. Create Notification

**Endpoint:** `POST /notifications`

**Authentication:** Protected

**Description:** Create a new notification

**Request Body:**
```json
{
  "userId": "user123",
  "type": "OrderShipped",
  "title": "Your order has been shipped",
  "message": "Your order #9fa85f64 has been shipped and is on its way.",
  "metadata": {
    "orderId": "9fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
}
```

**Validation Rules:**
- `userId`: Required, max 100 characters
- `type`: Required, valid notification type
- `title`: Required, max 200 characters
- `message`: Required, max 1000 characters
- `metadata`: Optional, JSON object

**Response (201 Created):**
```json
{
  "notificationId": "5fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "user123",
  "type": "OrderShipped",
  "title": "Your order has been shipped",
  "message": "Your order #9fa85f64 has been shipped and is on its way.",
  "isRead": false,
  "createdDate": "2024-01-22T10:00:00Z",
  "status": "Pending"
}
```

**Status Codes:**
- `201 Created` - Notification created successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token

---

### 5. Mark Notification as Read

**Endpoint:** `POST /notifications/{notificationId}/mark-read`

**Authentication:** Protected

**Description:** Mark a notification as read

**Path Parameters:**
- `notificationId`: Notification's unique identifier (GUID)

**Example Request:**
```
POST /notifications/5fa85f64-5717-4562-b3fc-2c963f66afa6/mark-read
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - Notification marked as read
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Notification not found

---

### 6. Send Notification

**Endpoint:** `POST /notifications/{notificationId}/send`

**Authentication:** Protected

**Description:** Send a notification to the user

**Path Parameters:**
- `notificationId`: Notification's unique identifier (GUID)

**Example Request:**
```
POST /notifications/5fa85f64-5717-4562-b3fc-2c963f66afa6/send
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - Notification sent successfully
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Notification not found

---

### 7. Retry Failed Notification

**Endpoint:** `POST /notifications/{notificationId}/retry`

**Authentication:** Protected

**Description:** Retry sending a failed notification

**Path Parameters:**
- `notificationId`: Notification's unique identifier (GUID)

**Example Request:**
```
POST /notifications/5fa85f64-5717-4562-b3fc-2c963f66afa6/retry
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - Retry initiated successfully
- `400 Bad Request` - Notification is not in failed state
- `401 Unauthorized` - Missing or invalid JWT token
- `404 Not Found` - Notification not found

---

### 8. Get User Notification Preferences

**Endpoint:** `GET /notifications/preferences/{userId}`

**Authentication:** Protected

**Description:** Get notification preferences for a user

**Path Parameters:**
- `userId`: User's unique identifier

**Example Request:**
```
GET /notifications/preferences/user123
```

**Response (200 OK):**
```json
{
  "userId": "user123",
  "emailEnabled": true,
  "smsEnabled": false,
  "pushEnabled": true,
  "notificationTypes": {
    "OrderShipped": true,
    "OrderDelivered": true,
    "OrderCancelled": true,
    "PriceAlert": false,
    "NewArrival": true
  },
  "quietHoursEnabled": true,
  "quietHoursStart": "22:00",
  "quietHoursEnd": "08:00"
}
```

**Status Codes:**
- `200 OK` - Preferences retrieved successfully
- `401 Unauthorized` - Missing or invalid JWT token

---

### 9. Update User Notification Preferences

**Endpoint:** `PUT /notifications/preferences/{userId}`

**Authentication:** Protected

**Description:** Update notification preferences for a user

**Path Parameters:**
- `userId`: User's unique identifier

**Request Body:**
```json
{
  "emailEnabled": true,
  "smsEnabled": false,
  "pushEnabled": true,
  "notificationTypes": {
    "OrderShipped": true,
    "OrderDelivered": true,
    "OrderCancelled": true,
    "PriceAlert": false,
    "NewArrival": true
  },
  "quietHoursEnabled": true,
  "quietHoursStart": "22:00",
  "quietHoursEnd": "08:00"
}
```

**Response (200 OK):**
```json
{
  "userId": "user123",
  "emailEnabled": true,
  "smsEnabled": false,
  "pushEnabled": true,
  "notificationTypes": {
    "OrderShipped": true,
    "OrderDelivered": true,
    "OrderCancelled": true,
    "PriceAlert": false,
    "NewArrival": true
  },
  "quietHoursEnabled": true,
  "quietHoursStart": "22:00",
  "quietHoursEnd": "08:00",
  "updatedDate": "2024-01-22T11:00:00Z"
}
```

**Status Codes:**
- `200 OK` - Preferences updated successfully
- `400 Bad Request` - Invalid input data
- `401 Unauthorized` - Missing or invalid JWT token

---

### 10. Disable All Notifications

**Endpoint:** `POST /notifications/preferences/{userId}/disable-all`

**Authentication:** Protected

**Description:** Disable all notifications for a user

**Path Parameters:**
- `userId`: User's unique identifier

**Example Request:**
```
POST /notifications/preferences/user123/disable-all
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - All notifications disabled
- `401 Unauthorized` - Missing or invalid JWT token

---

### 11. Enable All Notifications

**Endpoint:** `POST /notifications/preferences/{userId}/enable-all`

**Authentication:** Protected

**Description:** Enable all notifications for a user

**Path Parameters:**
- `userId`: User's unique identifier

**Example Request:**
```
POST /notifications/preferences/user123/enable-all
```

**Response (204 No Content):**
No response body

**Status Codes:**
- `204 No Content` - All notifications enabled
- `401 Unauthorized` - Missing or invalid JWT token

---

## Error Responses

All endpoints follow a consistent error response format:

### Standard Error Response

```json
{
  "statusCode": 400,
  "message": "Validation failed",
  "errors": {
    "email": ["Email is required", "Invalid email format"],
    "password": ["Password must be at least 8 characters"]
  },
  "timestamp": "2024-01-21T18:00:00Z",
  "path": "/auth/register"
}
```

### Common HTTP Status Codes

| Code | Description | When Used |
|------|-------------|-----------|
| 200 | OK | Successful GET, PUT, POST (non-creation) |
| 201 | Created | Successful POST (resource creation) |
| 204 | No Content | Successful DELETE or action with no response body |
| 400 | Bad Request | Invalid input data, validation errors |
| 401 | Unauthorized | Missing, invalid, or expired JWT token |
| 403 | Forbidden | Valid token but insufficient permissions |
| 404 | Not Found | Resource not found |
| 409 | Conflict | Resource already exists (duplicate) |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Unexpected server error |

### Authentication Errors

**Missing Authorization Header:**
```json
{
  "statusCode": 401,
  "message": "Authorization header missing",
  "timestamp": "2024-01-21T18:00:00Z"
}
```

**Invalid Token:**
```json
{
  "statusCode": 401,
  "message": "Invalid token",
  "timestamp": "2024-01-21T18:00:00Z"
}
```

**Expired Token:**
```json
{
  "statusCode": 401,
  "message": "Token has expired",
  "timestamp": "2024-01-21T18:00:00Z"
}
```

### Validation Errors

```json
{
  "statusCode": 400,
  "message": "Validation failed",
  "errors": {
    "email": ["Email is required"],
    "password": ["Password must be between 8 and 100 characters"]
  },
  "timestamp": "2024-01-21T18:00:00Z",
  "path": "/auth/register"
}
```

### Rate Limit Error

```json
{
  "statusCode": 429,
  "message": "Rate limit exceeded. Please try again later.",
  "retryAfter": 45,
  "timestamp": "2024-01-21T18:00:00Z"
}
```

---

## Summary

### Total Endpoints: 81+

- **Authentication**: 4 endpoints
- **User Management**: 12 endpoints
- **Search & Discovery**: 10 endpoints
- **Book Management**: 6 endpoints
- **Warehouse Management**: 11 endpoints
- **Shopping Cart**: 6 endpoints
- **Order Management**: 10 endpoints
- **Notifications**: 11 endpoints
- **Notification Preferences**: 4 endpoints

### Service Routes via API Gateway

| Service | Route Prefix | Destination |
|---------|-------------|-------------|
| AuthService | `/auth/*` | http://authservice:8080 |
| BookService | `/books/*` | http://bookservice:8080/api/books |
| WarehouseService | `/warehouse/*` | http://warehouseservice:8080/api/warehouse |
| SearchService | `/search/*` | http://searchservice:8080/api/search |
| OrderService | `/orders/*` | http://orderservice:8080/api/orders |
| ShoppingCartService | `/cart/*` | http://orderservice:8080/api/shoppingcart |
| UserService | `/users/*` | http://userservice:8080/api/users |
| NotificationService | `/notifications/*` | http://notificationservice:8080/api/notifications |

### CORS Configuration

The API Gateway allows requests from:
- `http://localhost:3000`
- `http://localhost:3001`

Allowed methods: GET, POST, PUT, DELETE, PATCH

### Health Check

**Endpoint:** `GET /health`

Check the health status of the API Gateway and all downstream services.

---

**Document Version:** 2.0  
**Last Updated:** 2025-11-19  
**Maintained By:** Georgia Tech Library Marketplace Team

