# Payment Endpoint 400 Error - Fix Guide

## Problem
The payment endpoint (`POST /orders/{orderId}/pay`) returns 400 Bad Request **BEFORE** reaching the controller, indicating model binding failure.

## Root Cause
The request body is either:
1. **Missing** - No body sent at all
2. **Empty** - Empty JSON object `{}`
3. **Invalid** - Missing required `amount` field
4. **Malformed** - Invalid JSON or missing `Content-Type` header

## Server Requirements

The server expects `PayOrderDto` with:

```csharp
public class PayOrderDto
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }  // REQUIRED

    [StringLength(50)]
    public string PaymentMethod { get; set; } = "card";  // OPTIONAL
}
```

## Correct Request Format

### Required Fields
- `amount` (decimal, required, must be >= 0.01)

### Optional Fields
- `paymentMethod` (string, optional, defaults to "card")

### Headers
- `Content-Type: application/json` (REQUIRED)
- `Authorization: Bearer <token>` (if authenticated)

## Frontend Implementation Fix

### ❌ WRONG - Missing Content-Type or Body
```javascript
// This will fail with 400
fetch(`/orders/api/orders/${orderId}/pay`, {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${token}`
    // Missing Content-Type!
  }
  // Missing body!
});
```

### ✅ CORRECT - Complete Request
```javascript
const payForOrder = async (orderId, orderTotal) => {
  const response = await fetch(`http://localhost:5004/orders/api/orders/${orderId}/pay`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',  // CRITICAL!
      'Authorization': `Bearer ${token}`
    },
    body: JSON.stringify({
      amount: orderTotal,           // REQUIRED - must match order total
      paymentMethod: "card"         // OPTIONAL - defaults to "card"
    })
  });

  if (!response.ok) {
    const error = await response.json();
    throw new Error(error.error || 'Payment failed');
  }

  return await response.json();
};
```

### React Hook Example (usePayment)

```typescript
import { useState } from 'react';

interface PayOrderDto {
  amount: number;
  paymentMethod?: string;
}

export const usePayment = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const payOrder = async (orderId: string, orderTotal: number, paymentMethod: string = "card") => {
    setLoading(true);
    setError(null);

    try {
      const token = localStorage.getItem('accessToken');
      
      const response = await fetch(`http://localhost:5004/orders/api/orders/${orderId}/pay`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',  // CRITICAL!
          ...(token && { 'Authorization': `Bearer ${token}` })
        },
        body: JSON.stringify({
          amount: orderTotal,        // REQUIRED
          paymentMethod: paymentMethod  // OPTIONAL
        } as PayOrderDto)
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || `Payment failed: ${response.status}`);
      }

      const result = await response.json();
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Payment failed';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  };

  return { payOrder, loading, error };
};
```

### Usage in Component

```typescript
import { usePayment } from './hooks/usePayment';

const CheckoutComponent = ({ order }) => {
  const { payOrder, loading, error } = usePayment();

  const handlePayment = async () => {
    try {
      // Ensure amount matches order total
      const result = await payOrder(
        order.orderId, 
        order.totalAmount,  // Use order's totalAmount
        "card"              // Optional payment method
      );
      
      console.log('Payment successful:', result);
      // Redirect to success page
    } catch (err) {
      console.error('Payment error:', err);
      // Show error to user
    }
  };

  return (
    <button onClick={handlePayment} disabled={loading}>
      {loading ? 'Processing...' : 'Pay Now'}
    </button>
  );
};
```

## Common Mistakes to Avoid

1. **Missing Content-Type header**
   ```javascript
   // ❌ WRONG
   headers: { 'Authorization': `Bearer ${token}` }
   
   // ✅ CORRECT
   headers: { 
     'Content-Type': 'application/json',
     'Authorization': `Bearer ${token}` 
   }
   ```

2. **Empty or missing body**
   ```javascript
   // ❌ WRONG
   body: JSON.stringify({})
   // or no body at all
   
   // ✅ CORRECT
   body: JSON.stringify({ amount: 45.98 })
   ```

3. **Invalid amount value**
   ```javascript
   // ❌ WRONG
   amount: 0  // Must be >= 0.01
   amount: -10  // Cannot be negative
   
   // ✅ CORRECT
   amount: 45.98  // Valid decimal >= 0.01
   ```

4. **Amount doesn't match order total**
   ```javascript
   // ❌ WRONG - sending wrong amount
   payOrder(orderId, 10.00)  // But order total is 45.98
   
   // ✅ CORRECT - use order's totalAmount
   payOrder(orderId, order.totalAmount)
   ```

## Testing with Postman/cURL

### Postman
```
POST http://localhost:5004/orders/api/orders/{orderId}/pay
Headers:
  Content-Type: application/json
  Authorization: Bearer <token>
Body (raw JSON):
{
  "amount": 45.98,
  "paymentMethod": "card"
}
```

### cURL
```bash
curl -X POST http://localhost:5004/orders/api/orders/{orderId}/pay \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <token>" \
  -d '{
    "amount": 45.98,
    "paymentMethod": "card"
  }'
```

## Debugging Steps

1. **Check Network Tab in Browser**
   - Open DevTools → Network tab
   - Find the payment request
   - Check:
     - Request URL is correct
     - Request Method is POST
     - Headers include `Content-Type: application/json`
     - Request Payload shows the JSON body with `amount` field

2. **Verify Request Body**
   ```javascript
   const body = JSON.stringify({ amount: orderTotal });
   console.log('Request body:', body);  // Should show: {"amount":45.98}
   ```

3. **Check Server Logs**
   - If you see "PayOrder called for order..." log → Request reached controller
   - If no logs → Model binding failed before controller

4. **Test with Minimal Request**
   ```javascript
   // Test with minimal valid request
   fetch(`/orders/api/orders/${orderId}/pay`, {
     method: 'POST',
     headers: { 'Content-Type': 'application/json' },
     body: JSON.stringify({ amount: 0.01 })  // Minimum valid amount
   });
   ```

## Summary

**The fix is simple:**
1. Always include `Content-Type: application/json` header
2. Always send `amount` field in request body (must be >= 0.01)
3. Optionally include `paymentMethod` (defaults to "card")
4. Ensure `amount` matches the order's `totalAmount`

The server will return 400 if any of these are missing or invalid.

