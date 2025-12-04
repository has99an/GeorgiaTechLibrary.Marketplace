# UI Ændringer for Adressefunktionalitet

## Oversigt

Backend har nu implementeret komplet adressefunktionalitet med **State** feltet. UI'en skal opdateres for at understøtte dette nye felt.

## Backend Status

✅ **UserService** - Har nu adresse med State felt:
- `street` (required)
- `city` (required)
- `postalCode` (required, 4 cifre)
- `state` (optional)
- `country` (optional, default "Denmark")

✅ **OrderService** - Har nu adresse med State felt:
- Henter automatisk adresse fra UserService hvis ikke angivet i checkout
- Accepterer adresse i checkout request (optional)
- Returnerer adresse med State i order response

## API Endpoints

### 1. Opdater Bruger Adresse
**PUT** `/api/users/{userId}`

**Request Body:**
```json
{
  "deliveryAddress": {
    "street": "Skoleholdervej 2",
    "city": "København",
    "postalCode": "2400",
    "state": "Hovedstaden",  // <-- NYT FELT (optional)
    "country": "Danmark"
  }
}
```

**Response:**
```json
{
  "userId": "...",
  "email": "...",
  "name": "...",
  "role": "...",
  "deliveryAddress": {
    "street": "Skoleholdervej 2",
    "city": "København",
    "postalCode": "2400",
    "state": "Hovedstaden",  // <-- NYT FELT
    "country": "Danmark"
  },
  "createdDate": "...",
  "updatedDate": "..."
}
```

### 2. Hent Bruger
**GET** `/api/users/{userId}`

**Response:** Samme som ovenfor - inkluderer nu `state` felt i `deliveryAddress`.

### 3. Checkout (OrderService)
**POST** `/api/shoppingcart/{customerId}/checkout`

**Request Body (optional):**
```json
{
  "deliveryAddress": {
    "street": "...",
    "city": "...",
    "postalCode": "...",
    "state": "...",  // <-- NYT FELT (optional)
    "country": "..."
  }
}
```

**Note:** Hvis `deliveryAddress` ikke sendes, henter OrderService automatisk adressen fra brugerens profil i UserService.

**Response:**
```json
{
  "orderId": "...",
  "customerId": "...",
  "orderDate": "...",
  "totalAmount": 123.45,
  "status": "Pending",
  "deliveryAddress": {
    "street": "...",
    "city": "...",
    "postalCode": "...",
    "state": "...",  // <-- NYT FELT
    "country": "..."
  },
  "orderItems": [...]
}
```

## UI Ændringer Nødvendige

### 1. UserProfile Component
**Fil:** `UserProfile.tsx` (eller lignende)

**Ændringer:**
1. Tilføj `state` felt til adresseformular:
   - Input field for "State/Region" (optional)
   - Placer mellem `postalCode` og `country`
   - Validation: Max 100 karakterer

2. Opdater state/interface:
   ```typescript
   interface Address {
     street: string;
     city: string;
     postalCode: string;
     state?: string;  // <-- NYT FELT
     country?: string;
   }
   ```

3. Opdater form submission:
   - Inkluder `state` i request body når adresse opdateres
   - Håndter `state` når adresse hentes fra API

4. Opdater form display:
   - Vis `state` i adressevisning hvis det er sat
   - Format: `Street, PostalCode City, State, Country`

### 2. Checkout Component
**Fil:** `CheckoutPage.tsx` eller lignende

**Ændringer:**
1. Hvis checkout formular har adresse input:
   - Tilføj `state` felt (optional)
   - Inkluder `state` i checkout request hvis adresse sendes

2. Hvis checkout bruger brugerens profil adresse:
   - Vis `state` i adressevisning hvis det er sat
   - Format: `Street, PostalCode City, State, Country`

### 3. Order Details Component
**Fil:** `OrderDetails.tsx` eller lignende

**Ændringer:**
1. Opdater order display:
   - Vis `state` i delivery address hvis det er sat
   - Format: `Street, PostalCode City, State, Country`

### 4. TypeScript Interfaces
**Fil:** `types.ts` eller lignende

**Opdater:**
```typescript
export interface AddressDto {
  street: string;
  city: string;
  postalCode: string;
  state?: string;  // <-- NYT FELT
  country?: string;
}
```

## Eksempel UI Form

```tsx
<TextField
  label="Street"
  value={address.street}
  onChange={(e) => setAddress({...address, street: e.target.value})}
  required
/>

<TextField
  label="City"
  value={address.city}
  onChange={(e) => setAddress({...address, city: e.target.value})}
  required
/>

<TextField
  label="Postal Code"
  value={address.postalCode}
  onChange={(e) => setAddress({...address, postalCode: e.target.value})}
  required
  inputProps={{ maxLength: 4, pattern: "[0-9]{4}" }}
/>

{/* NYT FELT */}
<TextField
  label="State/Region"
  value={address.state || ""}
  onChange={(e) => setAddress({...address, state: e.target.value})}
  helperText="Optional"
/>

<TextField
  label="Country"
  value={address.country || "Denmark"}
  onChange={(e) => setAddress({...address, country: e.target.value})}
/>
```

## Vigtige Noter

1. **State er optional** - UI'en skal ikke kræve dette felt
2. **Backward compatibility** - Eksisterende ordrer uden State vil stadig virke
3. **Default country** - Backend sætter default til "Denmark" hvis ikke angivet
4. **Validation** - State max 100 karakterer (hvis angivet)

## Test Scenarier

1. ✅ Opdater bruger adresse med State - skal gemmes og returneres
2. ✅ Opdater bruger adresse uden State - skal virke (State = null)
3. ✅ Checkout med adresse inkl. State - skal bruges i order
4. ✅ Checkout uden adresse - skal hente fra bruger profil (inkl. State hvis sat)
5. ✅ Vis order med State - skal vises korrekt i order details

