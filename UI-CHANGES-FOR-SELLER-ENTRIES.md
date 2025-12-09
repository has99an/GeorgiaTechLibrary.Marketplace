# UI Changes for Individual Seller Entries

## Overview

The SearchService API has been updated to return **individual seller entries** instead of aggregated book data. This means that if a book has multiple sellers, each seller will appear as a separate entry in the API response, allowing users to choose which seller to buy from.

## API Changes

### Endpoint: `GET /api/search/available`

**Previous Response Structure:**
```json
{
  "books": [
    {
      "isbn": "0307001164",
      "title": "Book Title",
      "author": "Author Name",
      "totalStock": 15,
      "availableSellers": 3,
      "minPrice": 19.99,
      // ... other book fields
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

**New Response Structure:**
```json
{
  "books": [
    {
      "isbn": "0307001164",
      "title": "Book Title",
      "author": "Author Name",
      "yearOfPublication": 2023,
      "publisher": "Publisher Inc",
      "imageUrlS": "http://example.com/small.jpg",
      "imageUrlM": "http://example.com/medium.jpg",
      "imageUrlL": "http://example.com/large.jpg",
      "genre": "Fiction",
      "language": "English",
      "pageCount": 350,
      "description": "Book description...",
      "rating": 4.5,
      "availabilityStatus": "Available",
      "edition": "First Edition",
      "format": "Paperback",
      // NEW: Seller-specific fields
      "sellerId": "3900d46c-c3b4-48dd-8b7d-e5ab9920fa22",
      "price": 19.99,
      "quantity": 5,
      "condition": "New",
      "lastUpdated": "2025-12-04T10:00:00Z"
    },
    {
      "isbn": "0307001164",  // Same book, different seller
      "title": "Book Title",
      "author": "Author Name",
      // ... same book fields ...
      "sellerId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",  // Different seller
      "price": 22.99,  // Different price
      "quantity": 10,
      "condition": "Used - Like New",
      "lastUpdated": "2025-12-04T11:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 150,  // Now counts individual seller entries, not books
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

## Key Changes

### 1. Multiple Entries per Book
- **Before:** One entry per book (aggregated data)
- **After:** One entry per seller per book
- If a book has 3 sellers, it will appear 3 times in the response

### 2. New Fields in Response
Each entry now includes seller-specific information:
- `sellerId` (string, GUID): Unique identifier for the seller
- `price` (decimal): Price for this specific seller
- `quantity` (int): Available stock from this seller
- `condition` (string): Book condition (e.g., "New", "Used - Like New")
- `lastUpdated` (DateTime): When this seller's information was last updated

### 3. Removed Aggregated Fields
The following fields are **no longer present** in the response:
- `totalStock` (removed - use `quantity` instead)
- `availableSellers` (removed - count entries with same ISBN)
- `minPrice` (removed - use `price` and sort/filter client-side)

## UI Implementation Requirements

### 1. Update TypeScript Interfaces

**Create/Update `BookSellerDto` interface:**
```typescript
interface BookSellerDto {
  // Book information
  isbn: string;
  title: string;
  author: string;
  yearOfPublication: number;
  publisher: string;
  imageUrlS?: string;
  imageUrlM?: string;
  imageUrlL?: string;
  genre: string;
  language: string;
  pageCount: number;
  description: string;
  rating: number;
  availabilityStatus: string;
  edition: string;
  format: string;
  
  // Seller-specific information
  sellerId: string;  // NEW - Required for cart/checkout
  price: number;
  quantity: number;
  condition: string;
  lastUpdated: string;  // ISO 8601 date string
}
```

### 2. Update Book Display Logic

**Before:**
- Display one card/item per book
- Show aggregated price (minPrice)
- Show total stock across all sellers

**After:**
- Display one card/item per seller entry
- Each card should show:
  - Book information (title, author, image, etc.)
  - **Seller-specific price** (`price` field)
  - **Available quantity from this seller** (`quantity` field)
  - **Book condition** (`condition` field)
  - **Seller ID** (hidden, but needed for cart/checkout)

### 3. Grouping Books (Optional)

If you want to group books by ISBN and show sellers as options:

```typescript
// Group entries by ISBN
const groupedByIsbn = books.reduce((acc, book) => {
  if (!acc[book.isbn]) {
    acc[book.isbn] = [];
  }
  acc[book.isbn].push(book);
  return acc;
}, {} as Record<string, BookSellerDto[]>);

// Display grouped books with seller options
Object.entries(groupedByIsbn).forEach(([isbn, sellers]) => {
  // Show book info once
  // Show seller options (price, quantity, condition) as selectable items
});
```

### 4. Update Cart/Checkout Logic

**Critical:** The cart must now include `sellerId` for each item.

**Cart Item Structure:**
```typescript
interface CartItem {
  bookISBN: string;
  sellerId: string;  // REQUIRED - Must match the sellerId from the API response
  quantity: number;
  unitPrice: number;  // Use the 'price' field from the selected seller entry
  // ... other fields
}
```

**When adding to cart:**
```typescript
// User selects a book entry (which is now seller-specific)
const selectedBook: BookSellerDto = /* from API response */;

// Add to cart with sellerId
addToCart({
  bookISBN: selectedBook.isbn,
  sellerId: selectedBook.sellerId,  // CRITICAL: Must include sellerId
  quantity: 1,
  unitPrice: selectedBook.price
});
```

### 5. Update Search/Filter Logic

**Price Filtering:**
- Filter by `price` field (not `minPrice`)
- Each entry has its own price

**Sorting:**
- Sort by `price` field to show cheapest/most expensive sellers first
- Users can now see all price options for the same book

**Stock Availability:**
- Check `quantity > 0` to determine if seller has stock
- Filter out entries where `quantity === 0`

### 6. Update Pagination

**Note:** The `totalCount` now represents the total number of **seller entries**, not books. This means:
- If there are 100 books with an average of 2 sellers each, `totalCount` will be ~200
- Pagination will show more pages than before
- Each page will show more items (since same book can appear multiple times)

## Example UI Flow

### Scenario: Book with 3 Sellers

1. **API Response:**
   ```json
   {
     "books": [
       { "isbn": "123", "title": "Book A", "sellerId": "seller1", "price": 19.99, "quantity": 5 },
       { "isbn": "123", "title": "Book A", "sellerId": "seller2", "price": 22.99, "quantity": 10 },
       { "isbn": "123", "title": "Book A", "sellerId": "seller3", "price": 18.99, "quantity": 3 }
     ]
   }
   ```

2. **UI Display Options:**

   **Option A: Show as separate cards**
   - Display 3 separate cards, each showing:
     - Book info (title, author, image)
     - Seller price: $19.99 / $22.99 / $18.99
     - Stock: 5 / 10 / 3 available
     - Condition: New / Used / Like New
   - User clicks "Add to Cart" on desired card
   - Cart receives: `{ bookISBN: "123", sellerId: "seller1", ... }`

   **Option B: Group by ISBN, show seller options**
   - Display 1 card with book info
   - Show 3 seller options below:
     - Seller 1: $19.99 (5 available) [Add to Cart]
     - Seller 2: $22.99 (10 available) [Add to Cart]
     - Seller 3: $18.99 (3 available) [Add to Cart]
   - User selects desired seller
   - Cart receives: `{ bookISBN: "123", sellerId: "selectedSellerId", ... }`

## Backward Compatibility

**Breaking Change:** This is a **breaking change**. The API response structure has changed significantly.

**Migration Steps:**
1. Update all TypeScript interfaces
2. Update all components that consume `/api/search/available`
3. Update cart logic to include `sellerId`
4. Update checkout logic to send `sellerId` in order items
5. Test thoroughly with books that have multiple sellers

## Testing Checklist

- [ ] API returns multiple entries for books with multiple sellers
- [ ] Each entry includes `sellerId`, `price`, `quantity`, `condition`
- [ ] UI displays all seller entries correctly
- [ ] Cart includes `sellerId` when adding items
- [ ] Checkout sends `sellerId` in order items
- [ ] Stock updates correctly after purchase (seller-specific)
- [ ] Pagination works correctly with new structure
- [ ] Search/filter works with new structure
- [ ] Price sorting works correctly

## Server Expectations

The server expects the following in checkout requests:

```json
{
  "orderItems": [
    {
      "bookISBN": "0307001164",
      "sellerId": "3900d46c-c3b4-48dd-8b7d-e5ab9920fa22",  // REQUIRED - Must match a sellerId from SearchService
      "quantity": 1,
      "unitPrice": 19.99
    }
  ],
  "deliveryAddress": { ... },
  "amount": 19.99,
  "paymentMethod": "card"
}
```

**Important:** The `sellerId` in the checkout request **must match** a `sellerId` from the SearchService API response. The server will validate this and reject orders with invalid `sellerId` values.

## Summary

- **API now returns individual seller entries** (one per seller per book)
- **Each entry includes `sellerId`, `price`, `quantity`, `condition`**
- **UI must display all sellers** and allow user to choose
- **Cart/Checkout must include `sellerId`** for each item
- **This is a breaking change** - requires full UI update



