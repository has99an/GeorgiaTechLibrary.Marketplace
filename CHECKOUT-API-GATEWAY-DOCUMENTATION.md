# Checkout API Gateway Documentation

## ✅ API Gateway Routing Fixed

### New Route Added
```json
"checkout-route": {
  "ClusterId": "orders-cluster",
  "Match": {
    "Path": "/checkout/{**catch-all}"
  },
  "Transforms": [
    { "PathRemovePrefix": "/checkout" },
    { "PathPrefix": "/api/checkout" }
  ]
}
```

**Mapping:**
- Gateway: `http://localhost:5004/checkout/*`
- Routes to: `http://orderservice:8080/api/checkout/*`

---

## 📋 Complete API Endpoints Documentation

### ENDPOINT 1: Create Checkout Session

**URL:** `POST http://localhost:5004/checkout/session`

**Query Parameters:**
- `customerId` (required): string (GUID format)

**Request Body:**
```json
{
  "street": "123 Main Street",
  "city": "Atlanta",
  "postalCode": "30332",
  "state": "Georgia",
  "country": "USA"
}
```

**Response 201 Created:**
```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "customerId": "user123-guid",
  "totalAmount": 129.97,
  "expiresAt": "2026-01-08T15:30:00Z",
  "deliveryAddress": {
    "street": "123 Main Street",
    "city": "Atlanta",
    "postalCode": "30332",
    "state": "Georgia",
    "country": "USA"
  },
  "itemsBySeller": [
    {
      "sellerId": "seller-abc-123",
      "items": [
        {
          "cartItemId": "item-guid-1",
          "bookISBN": "9780134685991",
          "sellerId": "seller-abc-123",
          "quantity": 2,
          "unitPrice": 49.99,
          "addedDate": "2026-01-08T14:00:00Z"
        }
      ],
      "sellerTotal": 99.98,
      "platformFee": 9.998,
      "sellerPayout": 89.982,
      "platformFeePercentage": 10
    },
    {
      "sellerId": "seller-xyz-456",
      "items": [
        {
          "cartItemId": "item-guid-2",
          "bookISBN": "9780131103627",
          "sellerId": "seller-xyz-456",
          "quantity": 1,
          "unitPrice": 29.99,
          "addedDate": "2026-01-08T14:05:00Z"
        }
      ],
      "sellerTotal": 29.99,
      "platformFee": 2.999,
      "sellerPayout": 26.991,
      "platformFeePercentage": 10
    }
  ]
}
```

**Error Responses:**
- `400 Bad Request`: Empty cart or validation error
- `500 Internal Server Error`: Server error

---

### ENDPOINT 2: Confirm Payment

**URL:** `POST http://localhost:5004/checkout/confirm`

**Request Body:**
```json
{
  "sessionId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "paymentMethod": "card"
}
```

**Response 201 Created:**
```json
{
  "orderId": "order-guid-12345",
  "customerId": "user123-guid",
  "orderDate": "2026-01-08T15:00:00Z",
  "totalAmount": 129.97,
  "status": "Paid",
  "paidDate": "2026-01-08T15:00:00Z",
  "deliveryAddress": {
    "street": "123 Main Street",
    "city": "Atlanta",
    "postalCode": "30332",
    "state": "Georgia",
    "country": "USA"
  },
  "orderItems": [
    {
      "orderItemId": "orderitem-guid-1",
      "bookISBN": "9780134685991",
      "sellerId": "seller-abc-123",
      "quantity": 2,
      "unitPrice": 49.99,
      "status": "Processing"
    },
    {
      "orderItemId": "orderitem-guid-2",
      "bookISBN": "9780131103627",
      "sellerId": "seller-xyz-456",
      "quantity": 1,
      "unitPrice": 29.99,
      "status": "Processing"
    }
  ]
}
```

**Error Responses:**
- `400 Bad Request`: Payment failed (retryable)
- `404 Not Found`: Invalid sessionId
- `410 Gone`: Session expired (user must restart checkout)
- `500 Internal Server Error`: Server error

---

### ENDPOINT 3: Get Checkout Session

**URL:** `GET http://localhost:5004/checkout/session/{sessionId}`

**Path Parameters:**
- `sessionId` (required): string (GUID)

**Response 200 OK:**
```json
{
  // Same structure as POST /checkout/session response
}
```

**Error Responses:**
- `404 Not Found`: Session not found
- `410 Gone`: Session expired

---

## 🎯 Frontend Configuration Guide

### 1. Base Configuration

```typescript
// config/api.ts
export const API_CONFIG = {
  BASE_URL: 'http://localhost:5004',
  ENDPOINTS: {
    CREATE_CHECKOUT_SESSION: '/checkout/session',
    CONFIRM_PAYMENT: '/checkout/confirm',
    GET_CHECKOUT_SESSION: '/checkout/session',
  },
  SESSION_EXPIRY_MINUTES: 30
};
```

### 2. API Service Implementation

```typescript
// services/checkoutService.ts
import axios from 'axios';
import { API_CONFIG } from '../config/api';

export interface DeliveryAddress {
  street: string;
  city: string;
  postalCode: string;
  state?: string;
  country?: string;
}

export interface CheckoutSession {
  sessionId: string;
  customerId: string;
  totalAmount: number;
  expiresAt: string;
  deliveryAddress: DeliveryAddress;
  itemsBySeller: SellerAllocation[];
}

export interface SellerAllocation {
  sellerId: string;
  items: CartItem[];
  sellerTotal: number;
  platformFee: number;
  sellerPayout: number;
  platformFeePercentage: number;
}

export interface Order {
  orderId: string;
  customerId: string;
  orderDate: string;
  totalAmount: number;
  status: string;
  paidDate: string;
  deliveryAddress: DeliveryAddress;
  orderItems: OrderItem[];
}

export class CheckoutService {
  private baseUrl = API_CONFIG.BASE_URL;

  async createSession(
    customerId: string, 
    deliveryAddress: DeliveryAddress
  ): Promise<CheckoutSession> {
    const response = await axios.post(
      `${this.baseUrl}/checkout/session?customerId=${customerId}`,
      deliveryAddress
    );
    return response.data;
  }

  async getSession(sessionId: string): Promise<CheckoutSession> {
    const response = await axios.get(
      `${this.baseUrl}/checkout/session/${sessionId}`
    );
    return response.data;
  }

  async confirmPayment(
    sessionId: string,
    paymentMethod: string = 'card'
  ): Promise<Order> {
    const response = await axios.post(
      `${this.baseUrl}/checkout/confirm`,
      { sessionId, paymentMethod }
    );
    return response.data;
  }
}

export const checkoutService = new CheckoutService();
```

### 3. React Component Example

```typescript
// pages/CheckoutPage.tsx
import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { checkoutService } from '../services/checkoutService';

export const CheckoutPage: React.FC = () => {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleCheckout = async (deliveryAddress) => {
    setLoading(true);
    setError(null);

    try {
      // Step 1: Create checkout session
      const session = await checkoutService.createSession(
        getCurrentUserId(), // Your user context
        deliveryAddress
      );

      // Step 2: Navigate to payment page with sessionId
      navigate(`/payment/${session.sessionId}`, {
        state: { session }
      });
    } catch (err: any) {
      if (err.response?.status === 400) {
        setError('Cart is empty or invalid');
      } else {
        setError('Failed to create checkout session');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      {/* Your checkout form */}
    </div>
  );
};
```

```typescript
// pages/PaymentPage.tsx
import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { checkoutService } from '../services/checkoutService';

export const PaymentPage: React.FC = () => {
  const { sessionId } = useParams();
  const navigate = useNavigate();
  const [session, setSession] = useState(null);
  const [processing, setProcessing] = useState(false);

  useEffect(() => {
    loadSession();
  }, [sessionId]);

  const loadSession = async () => {
    try {
      const data = await checkoutService.getSession(sessionId!);
      setSession(data);
    } catch (err: any) {
      if (err.response?.status === 410) {
        alert('Session expired. Please start checkout again.');
        navigate('/cart');
      }
    }
  };

  const handlePayment = async () => {
    setProcessing(true);

    try {
      const order = await checkoutService.confirmPayment(
        sessionId!,
        'card'
      );

      // Navigate to order confirmation
      navigate(`/order-confirmation/${order.orderId}`, {
        state: { order }
      });
    } catch (err: any) {
      if (err.response?.status === 410) {
        alert('Session expired. Please start checkout again.');
        navigate('/cart');
      } else if (err.response?.status === 400) {
        alert(`Payment failed: ${err.response.data.error}`);
      }
    } finally {
      setProcessing(false);
    }
  };

  if (!session) return <div>Loading...</div>;

  return (
    <div>
      <h2>Payment Summary</h2>
      <p>Total: ${session.totalAmount}</p>
      
      <h3>Seller Breakdown:</h3>
      {session.itemsBySeller.map(seller => (
        <div key={seller.sellerId}>
          <p>Seller: {seller.sellerId}</p>
          <p>Subtotal: ${seller.sellerTotal}</p>
          <p>Platform Fee (10%): ${seller.platformFee}</p>
        </div>
      ))}

      <button onClick={handlePayment} disabled={processing}>
        {processing ? 'Processing...' : 'Complete Payment'}
      </button>
    </div>
  );
};
```

### 4. Error Handling Guide

```typescript
// utils/errorHandler.ts
export const handleCheckoutError = (error: any, navigate: any) => {
  const status = error.response?.status;
  
  switch (status) {
    case 400:
      // Validation error or payment failed
      return {
        message: error.response.data.error || 'Invalid request',
        retryable: error.response.data.retryable || false
      };
      
    case 404:
      // Session not found
      return {
        message: 'Checkout session not found',
        action: () => navigate('/cart')
      };
      
    case 410:
      // Session expired
      return {
        message: 'Your checkout session has expired. Please start again.',
        action: () => navigate('/cart')
      };
      
    case 500:
      // Server error
      return {
        message: 'Server error. Please try again later.',
        retryable: true
      };
      
    default:
      return {
        message: 'An unexpected error occurred',
        retryable: false
      };
  }
};
```

### 5. Session Expiry Timer

```typescript
// components/CheckoutTimer.tsx
import React, { useState, useEffect } from 'react';

export const CheckoutTimer: React.FC<{ expiresAt: string }> = ({ expiresAt }) => {
  const [timeLeft, setTimeLeft] = useState<number>(0);

  useEffect(() => {
    const calculateTimeLeft = () => {
      const expires = new Date(expiresAt).getTime();
      const now = Date.now();
      const diff = Math.max(0, expires - now);
      setTimeLeft(Math.floor(diff / 1000));
    };

    calculateTimeLeft();
    const interval = setInterval(calculateTimeLeft, 1000);

    return () => clearInterval(interval);
  }, [expiresAt]);

  const minutes = Math.floor(timeLeft / 60);
  const seconds = timeLeft % 60;

  if (timeLeft === 0) {
    return <div className="text-red-600">Session Expired!</div>;
  }

  return (
    <div className="text-orange-600">
      Session expires in: {minutes}:{seconds.toString().padStart(2, '0')}
    </div>
  );
};
```

---

## 🧪 Testing Instructions

### 1. Test Session Creation

```bash
# Test creating checkout session
curl -X POST "http://localhost:5004/checkout/session?customerId=test-user-123" \
  -H "Content-Type: application/json" \
  -d '{
    "street": "123 Main Street",
    "city": "Atlanta",
    "postalCode": "30332",
    "state": "Georgia",
    "country": "USA"
  }'

# Expected: 201 Created with sessionId
```

### 2. Test Session Retrieval

```bash
# Replace {sessionId} with actual session ID from step 1
curl -X GET "http://localhost:5004/checkout/session/{sessionId}"

# Expected: 200 OK with session details
```

### 3. Test Payment Confirmation

```bash
# Replace {sessionId} with actual session ID
curl -X POST "http://localhost:5004/checkout/confirm" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "{sessionId}",
    "paymentMethod": "card"
  }'

# Expected: 201 Created with order details
```

### 4. Test Session Expiration

```bash
# Use expired or invalid sessionId
curl -X GET "http://localhost:5004/checkout/session/invalid-session-id"

# Expected: 410 Gone with expiration message
```

### 5. Complete Integration Test Script

```bash
#!/bin/bash

# Integration test script for checkout flow
BASE_URL="http://localhost:5004"
CUSTOMER_ID="test-user-$(date +%s)"

echo "=== Testing Checkout Flow ==="

# Step 1: Create session
echo -e "\n1. Creating checkout session..."
SESSION_RESPONSE=$(curl -s -X POST "$BASE_URL/checkout/session?customerId=$CUSTOMER_ID" \
  -H "Content-Type: application/json" \
  -d '{
    "street": "123 Test Street",
    "city": "Atlanta",
    "postalCode": "30332",
    "state": "Georgia",
    "country": "USA"
  }')

echo "$SESSION_RESPONSE" | jq '.'

SESSION_ID=$(echo "$SESSION_RESPONSE" | jq -r '.sessionId')
echo "Session ID: $SESSION_ID"

# Step 2: Retrieve session
echo -e "\n2. Retrieving session..."
curl -s -X GET "$BASE_URL/checkout/session/$SESSION_ID" | jq '.'

# Step 3: Confirm payment
echo -e "\n3. Confirming payment..."
ORDER_RESPONSE=$(curl -s -X POST "$BASE_URL/checkout/confirm" \
  -H "Content-Type: application/json" \
  -d "{
    \"sessionId\": \"$SESSION_ID\",
    \"paymentMethod\": \"card\"
  }")

echo "$ORDER_RESPONSE" | jq '.'

ORDER_ID=$(echo "$ORDER_RESPONSE" | jq -r '.orderId')
echo "Order ID: $ORDER_ID"

echo -e "\n=== Test Complete ==="
```

---

## 📊 Summary

### URLs for Frontend

**Base URL:** `http://localhost:5004`

**Endpoints:**
1. `POST /checkout/session?customerId={id}` - Create session
2. `GET /checkout/session/{sessionId}` - Get session details
3. `POST /checkout/confirm` - Confirm payment

**Flow:**
1. Cart Page → POST session (with delivery address)
2. Payment Page → GET session (display details)
3. Payment Confirm → POST confirm (process payment)
4. Order Confirmation → Show order from response

**Key Points:**
- Sessions expire after 30 minutes
- Payment is processed before order creation
- Multi-seller breakdown is automatic
- Platform takes 10% fee from each seller
- All monetary values are in USD with 2 decimal places

**Error Handling:**
- 400: Validation errors (show error to user)
- 404: Session not found (redirect to cart)
- 410: Session expired (redirect to cart with message)
- 500: Server error (show retry option)
