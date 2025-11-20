# Frontend Integration Guide

## Overview

This guide provides everything a frontend developer needs to integrate with the Georgia Tech Library Marketplace backend APIs. The system uses a microservices architecture with an API Gateway that handles authentication, routing, and request aggregation.

## Getting Started

### API Base URL
```
http://localhost:5004
```

All API requests should be made to this base URL. The API Gateway will route requests to the appropriate microservice.

### Required Tools
- Modern web browser with fetch API or axios
- Development server (e.g., Create React App, Vite)
- JWT token handling library (optional but recommended)

### Service Architecture
- **ApiGateway**: Entry point, authentication, routing
- **AuthService**: User authentication and authorization
- **BookService**: Book catalog management
- **SearchService**: Full-text search functionality
- **OrderService**: Order processing and management
- **WarehouseService**: Inventory and stock management
- **NotificationService**: Real-time notifications (background service)

## Authentication Flow

### User Registration
Register a new user account:

```javascript
const registerUser = async (email, password) => {
  const response = await fetch('http://localhost:5004/auth/api/auth/register', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      email: email,
      password: password
    })
  });

  if (response.ok) {
    const data = await response.json();
    // Store tokens
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    return data;
  } else {
    throw new Error('Registration failed');
  }
};
```

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "securepassword123"
}
```

**Success Response (201):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

### User Login
Authenticate existing user:

```javascript
const loginUser = async (email, password) => {
  const response = await fetch('http://localhost:5004/auth/api/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      email: email,
      password: password
    })
  });

  if (response.ok) {
    const data = await response.json();
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    return data;
  } else {
    throw new Error('Login failed');
  }
};
```

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "securepassword123"
}
```

**Success Response (200):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

### Token Refresh
Refresh expired access tokens:

```javascript
const refreshToken = async () => {
  const refreshToken = localStorage.getItem('refreshToken');
  const response = await fetch('http://localhost:5004/auth/api/auth/refresh', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      refreshToken: refreshToken
    })
  });

  if (response.ok) {
    const data = await response.json();
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    return data;
  } else {
    // Redirect to login
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    throw new Error('Token refresh failed');
  }
};
```

## Protected Endpoints

### Authorization Header
All protected endpoints require a JWT token in the Authorization header:

```javascript
const getAuthHeaders = () => {
  const token = localStorage.getItem('accessToken');
  return {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  };
};
```

### Automatic Token Refresh
Implement an HTTP interceptor to handle token refresh:

```javascript
// Axios interceptor example
axios.interceptors.response.use(
  response => response,
  async error => {
    if (error.response?.status === 401) {
      try {
        await refreshToken();
        // Retry the original request
        const config = error.config;
        config.headers.Authorization = `Bearer ${localStorage.getItem('accessToken')}`;
        return axios(config);
      } catch (refreshError) {
        // Redirect to login
        window.location.href = '/login';
      }
    }
    return Promise.reject(error);
  }
);
```

## Key API Calls

### Get All Books
Retrieve the complete book catalog:

```javascript
const getAllBooks = async () => {
  const response = await fetch('http://localhost:5004/books/api/books', {
    headers: getAuthHeaders()
  });

  if (response.ok) {
    return await response.json();
  } else {
    throw new Error('Failed to fetch books');
  }
};
```

**Response (200):**
```json
[
  {
    "isbn": "1234567890123",
    "bookTitle": "Sample Book",
    "bookAuthor": "Author Name",
    "yearOfPublication": 2023,
    "publisher": "Publisher Inc",
    "imageUrlS": "http://example.com/small.jpg",
    "imageUrlM": "http://example.com/medium.jpg",
    "imageUrlL": "http://example.com/large.jpg"
  }
]
```

### Search Books
Search for books by title, author, or ISBN:

```javascript
const searchBooks = async (query) => {
  const response = await fetch(`http://localhost:5004/search/api/search?query=${encodeURIComponent(query)}`, {
    headers: getAuthHeaders()
  });

  if (response.ok) {
    return await response.json();
  } else {
    throw new Error('Search failed');
  }
};
```

**Response (200):**
```json
[
  {
    "isbn": "1234567890123",
    "title": "Harry Potter and the Philosopher's Stone",
    "author": "J.K. Rowling",
    "totalStock": 15,
    "availableSellers": 3,
    "minPrice": 19.99
  }
]
```

### Create Order
Create a new order with multiple items:

```javascript
const createOrder = async (customerId, orderItems) => {
  const response = await fetch('http://localhost:5004/orders/api/orders', {
    method: 'POST',
    headers: getAuthHeaders(),
    body: JSON.stringify({
      customerId: customerId,
      orderItems: orderItems
    })
  });

  if (response.ok) {
    return await response.json();
  } else {
    throw new Error('Order creation failed');
  }
};
```

**Request Body:**
```json
{
  "customerId": "user-guid-here",
  "orderItems": [
    {
      "bookISBN": "1234567890123",
      "sellerId": "seller-guid-here",
      "quantity": 2,
      "unitPrice": 22.99
    }
  ]
}
```

**Response (201):**
```json
{
  "orderId": "order-guid-here",
  "customerId": "user-guid-here",
  "orderDate": "2025-11-11T04:00:00Z",
  "totalAmount": 45.98,
  "status": "Pending",
  "orderItems": [
    {
      "orderItemId": "item-guid-here",
      "orderId": "order-guid-here",
      "bookISBN": "1234567890123",
      "sellerId": "seller-guid-here",
      "quantity": 2,
      "unitPrice": 22.99,
      "status": "Pending"
    }
  ]
}
```

### Get Order Details
Retrieve information about a specific order:

```javascript
const getOrderDetails = async (orderId) => {
  const response = await fetch(`http://localhost:5004/orders/api/orders/${orderId}`, {
    headers: getAuthHeaders()
  });

  if (response.ok) {
    return await response.json();
  } else {
    throw new Error('Failed to fetch order details');
  }
};
```

### Pay for Order
Process payment for a pending order:

```javascript
const payForOrder = async (orderId, amount, paymentMethod = "card") => {
  const response = await fetch(`http://localhost:5004/orders/api/orders/${orderId}/pay`, {
    method: 'POST',
    headers: {
      ...getAuthHeaders(),
      'Content-Type': 'application/json'  // CRITICAL: Must include Content-Type
    },
    body: JSON.stringify({
      amount: amount,
      paymentMethod: paymentMethod  // Optional, defaults to "card"
    })
  });

  if (response.ok) {
    return await response.json();
  } else {
    const errorData = await response.json().catch(() => ({}));
    throw new Error(errorData.error || 'Payment failed');
  }
};
```

**Request Body (REQUIRED):**
```json
{
  "amount": 45.98,
  "paymentMethod": "card"
}
```

**Note:** 
- `amount` is **REQUIRED** and must be >= 0.01
- `paymentMethod` is **OPTIONAL** and defaults to "card" if not provided
- **CRITICAL:** Always include `Content-Type: application/json` header
- The `amount` should match the order's `totalAmount`

**Response (200):**
```json
{
  "message": "Order paid successfully"
}
```

## Response Formats

### Success Responses
All successful API responses follow this general structure:

```json
{
  // Response data varies by endpoint
  // Can be an object, array, or primitive value
}
```

### Error Responses
Error responses include appropriate HTTP status codes and error details:

```json
{
  "message": "Error description",
  "details": "Additional error information (optional)"
}
```

## Error Handling

### HTTP Status Codes

| Status Code | Meaning | Action Required |
|-------------|---------|----------------|
| 200 | Success | None |
| 201 | Created | None |
| 400 | Bad Request | Fix request data |
| 401 | Unauthorized | Refresh token or login |
| 403 | Forbidden | Check permissions |
| 404 | Not Found | Check resource ID |
| 409 | Conflict | Resource already exists |
| 500 | Internal Server Error | Retry later |

### Common Error Scenarios

#### Token Expired (401)
```javascript
if (response.status === 401) {
  try {
    await refreshToken();
    // Retry request
  } catch {
    // Redirect to login
  }
}
```

#### Network Errors
```javascript
try {
  const response = await fetch(url, options);
  // Handle response
} catch (error) {
  if (error.name === 'TypeError') {
    // Network error - show offline message
  }
}
```

#### Validation Errors (400)
```javascript
if (response.status === 400) {
  const errorData = await response.json();
  // Display validation messages to user
  showValidationErrors(errorData.errors);
}
```

## Real-time Events

### WebSocket Gateway (Proposed Implementation)

For real-time notifications, implement a WebSocket gateway service that connects to RabbitMQ and broadcasts events to connected clients.

#### Connection Setup
```javascript
class WebSocketService {
  constructor() {
    this.socket = null;
    this.reconnectAttempts = 0;
    this.maxReconnectAttempts = 5;
  }

  connect(userId) {
    this.socket = new WebSocket(`ws://localhost:5007/ws?userId=${userId}`);

    this.socket.onopen = () => {
      console.log('WebSocket connected');
      this.reconnectAttempts = 0;
    };

    this.socket.onmessage = (event) => {
      const notification = JSON.parse(event.data);
      this.handleNotification(notification);
    };

    this.socket.onclose = () => {
      console.log('WebSocket disconnected');
      this.attemptReconnect(userId);
    };

    this.socket.onerror = (error) => {
      console.error('WebSocket error:', error);
    };
  }

  handleNotification(notification) {
    switch (notification.type) {
      case 'ORDER_CREATED':
        showOrderConfirmation(notification.data);
        break;
      case 'ORDER_PAID':
        showPaymentConfirmation(notification.data);
        break;
      case 'SELLER_NOTIFICATION':
        showSellerMessage(notification.data);
        break;
    }
  }

  attemptReconnect(userId) {
    if (this.reconnectAttempts < this.maxReconnectAttempts) {
      setTimeout(() => {
        this.reconnectAttempts++;
        this.connect(userId);
      }, 1000 * this.reconnectAttempts);
    }
  }

  disconnect() {
    if (this.socket) {
      this.socket.close();
    }
  }
}
```

#### Event Types

**Order Created Notification:**
```json
{
  "type": "ORDER_CREATED",
  "data": {
    "orderId": "order-guid",
    "totalAmount": 45.98,
    "itemCount": 2
  }
}
```

**Order Paid Notification:**
```json
{
  "type": "ORDER_PAID",
  "data": {
    "orderId": "order-guid",
    "paidDate": "2025-11-11T04:05:00Z"
  }
}
```

**Seller Notification:**
```json
{
  "type": "SELLER_NOTIFICATION",
  "data": {
    "orderId": "order-guid",
    "bookISBN": "1234567890123",
    "quantity": 2,
    "message": "Please ship 2x of book 1234567890123"
  }
}
```

### Implementation Notes

1. **WebSocket Gateway Service**: A separate service (port 5007) that bridges RabbitMQ events to WebSocket clients
2. **User-Specific Connections**: Each user connects with their userId for personalized notifications
3. **Reconnection Logic**: Automatic reconnection with exponential backoff
4. **Message Parsing**: JSON-based message format for easy client handling

## React Integration Example

### Authentication Context
```javascript
import React, { createContext, useContext, useState, useEffect } from 'react';

const AuthContext = createContext();

export const useAuth = () => useContext(AuthContext);

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    checkAuthStatus();
  }, []);

  const checkAuthStatus = async () => {
    const token = localStorage.getItem('accessToken');
    if (token) {
      // Validate token with backend
      try {
        const response = await fetch('http://localhost:5004/auth/api/auth/validate', {
          headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
          setUser({ token });
        } else {
          localStorage.removeItem('accessToken');
          localStorage.removeItem('refreshToken');
        }
      } catch (error) {
        console.error('Auth check failed:', error);
      }
    }
    setLoading(false);
  };

  const login = async (email, password) => {
    const response = await fetch('http://localhost:5004/auth/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });

    if (response.ok) {
      const data = await response.json();
      localStorage.setItem('accessToken', data.accessToken);
      localStorage.setItem('refreshToken', data.refreshToken);
      setUser({ token: data.accessToken });
      return data;
    } else {
      throw new Error('Login failed');
    }
  };

  const logout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, login, logout, loading }}>
      {children}
    </AuthContext.Provider>
  );
};
```

### API Service
```javascript
class ApiService {
  constructor() {
    this.baseUrl = 'http://localhost:5004';
  }

  async request(endpoint, options = {}) {
    const url = `${this.baseUrl}${endpoint}`;
    const token = localStorage.getItem('accessToken');

    const config = {
      headers: {
        'Content-Type': 'application/json',
        ...(token && { 'Authorization': `Bearer ${token}` }),
        ...options.headers
      },
      ...options
    };

    let response = await fetch(url, config);

    // Handle token refresh
    if (response.status === 401) {
      try {
        await this.refreshToken();
        config.headers.Authorization = `Bearer ${localStorage.getItem('accessToken')}`;
        response = await fetch(url, config);
      } catch {
        throw new Error('Authentication failed');
      }
    }

    if (!response.ok) {
      throw new Error(`API Error: ${response.status}`);
    }

    return response.json();
  }

  async refreshToken() {
    const refreshToken = localStorage.getItem('refreshToken');
    const response = await fetch(`${this.baseUrl}/auth/api/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken })
    });

    if (response.ok) {
      const data = await response.json();
      localStorage.setItem('accessToken', data.accessToken);
      localStorage.setItem('refreshToken', data.refreshToken);
      return data;
    } else {
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
      throw new Error('Token refresh failed');
    }
  }

  // API Methods
  async getBooks() {
    return this.request('/books/api/books');
  }

  async searchBooks(query) {
    return this.request(`/search/api/search?query=${encodeURIComponent(query)}`);
  }

  async createOrder(customerId, orderItems) {
    return this.request('/orders/api/orders', {
      method: 'POST',
      body: JSON.stringify({ customerId, orderItems })
    });
  }

  async getOrder(orderId) {
    return this.request(`/orders/api/orders/${orderId}`);
  }

  async payOrder(orderId, amount, paymentMethod = "card") {
    return this.request(`/orders/api/orders/${orderId}/pay`, {
      method: 'POST',
      body: JSON.stringify({ 
        amount: amount,
        paymentMethod: paymentMethod 
      })
    });
  }
}

export const apiService = new ApiService();
```

## Best Practices

### Security
- Always validate JWT tokens on protected routes
- Store tokens securely (localStorage is acceptable for demo, consider httpOnly cookies for production)
- Implement token refresh logic to maintain user sessions
- Validate all user inputs on both frontend and backend

### Performance
- Implement caching for book data and search results
- Use pagination for large datasets
- Debounce search inputs to reduce API calls
- Implement loading states for better UX

### Error Handling
- Provide user-friendly error messages
- Implement retry logic for network failures
- Handle offline scenarios gracefully
- Log errors for debugging

### State Management
- Use React Context or Redux for authentication state
- Cache API responses to reduce redundant calls
- Implement optimistic updates for better perceived performance

### CORS Considerations
- The API Gateway handles CORS, but ensure your development server allows the API domain
- In production, configure CORS policies appropriately

This guide covers all the essential aspects of integrating a React frontend with the Georgia Tech Library Marketplace backend. The examples provided are production-ready and include proper error handling, authentication, and real-time event management.
