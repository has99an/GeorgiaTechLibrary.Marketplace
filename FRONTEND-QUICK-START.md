# Frontend Quick Start - Checkout Integration

## 🚀 URLs (Copy & Paste Ready)

```
BASE_URL: http://localhost:5004

Endpoint 1: POST http://localhost:5004/checkout/session?customerId={id}
Endpoint 2: GET http://localhost:5004/checkout/session/{sessionId}
Endpoint 3: POST http://localhost:5004/checkout/confirm
```

## 📝 Request/Response Examples

### 1. Create Session

**Request:**
```bash
POST http://localhost:5004/checkout/session?customerId=user-123

Body:
{
  "street": "123 Main Street",
  "city": "Atlanta",
  "postalCode": "30332",
  "state": "Georgia",
  "country": "USA"
}
```

**Response:**
```json
{
  "sessionId": "a1b2c3d4-...",
  "totalAmount": 129.97,
  "expiresAt": "2026-01-08T15:30:00Z",
  "itemsBySeller": [
    {
      "sellerId": "seller-abc",
      "sellerTotal": 99.98,
      "platformFee": 9.998,
      "sellerPayout": 89.982
    }
  ]
}
```

### 2. Confirm Payment

**Request:**
```bash
POST http://localhost:5004/checkout/confirm

Body:
{
  "sessionId": "a1b2c3d4-...",
  "paymentMethod": "card"
}
```

**Response:**
```json
{
  "orderId": "order-guid",
  "status": "Paid",
  "totalAmount": 129.97,
  "orderItems": [...]
}
```

## 💻 Code Template (TypeScript/React)

```typescript
// Step 1: Create session
const session = await fetch(
  `http://localhost:5004/checkout/session?customerId=${userId}`,
  {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      street: '123 Main St',
      city: 'Atlanta',
      postalCode: '30332',
      state: 'Georgia',
      country: 'USA'
    })
  }
).then(r => r.json());

// Step 2: Confirm payment
const order = await fetch(
  'http://localhost:5004/checkout/confirm',
  {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      sessionId: session.sessionId,
      paymentMethod: 'card'
    })
  }
).then(r => r.json());
```

## ⚠️ Error Handling

```typescript
try {
  // ... API call
} catch (error) {
  if (error.status === 410) {
    // Session expired - redirect to cart
    navigate('/cart');
    alert('Session expired, please start again');
  } else if (error.status === 400) {
    // Payment failed - show retry
    alert('Payment failed: ' + error.message);
  }
}
```

## ⏱️ Session Timer

Sessions expire after **30 minutes**. Show countdown timer to user.

```typescript
const expiryTime = new Date(session.expiresAt);
const now = new Date();
const minutesLeft = Math.floor((expiryTime - now) / 60000);
```

## 📦 What You Get

**Per-Seller Breakdown:**
- Each seller's subtotal
- Platform fee (10%)
- Seller's net payout

**Order Confirmation:**
- Order ID
- Status: "Paid"
- All items with seller info
- Delivery address

## 🧪 Quick Test

```bash
# Test if routing works
curl http://localhost:5004/checkout/session?customerId=test-123 \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{"street":"Test","city":"Atlanta","postalCode":"30332"}'

# Should return: 201 with sessionId
```

## 🔗 Complete Documentation

See `CHECKOUT-API-GATEWAY-DOCUMENTATION.md` for full details, examples, and TypeScript types.
