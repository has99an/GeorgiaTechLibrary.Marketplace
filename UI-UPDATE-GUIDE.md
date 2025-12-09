# UI Update Guide - Adressefunktionalitet

## Oversigt

Der er tilføjet adressefunktionalitet til UserService og OrderService, så brugere kan angive leveringsadresse i deres profil, og ordrer automatisk bruger denne adresse ved oprettelse.

**Dato:** November 2024  
**Version:** 1.0

---

## 1. UserService - Nye Adressefelter

### Opdaterede Endpoints

Alle UserService endpoints returnerer nu et nyt `deliveryAddress` felt i response.

### 1.1 Get User by ID / Get Current User

**Endpoints:**
- `GET /api/users/{userId}`
- `GET /api/users/me`

**Response - Opdateret Format:**
```json
{
  "userId": "123e4567-e89b-12d3-a456-426614174000",
  "email": "john@example.com",
  "name": "John Doe",
  "role": "Student",
  "deliveryAddress": {
    "street": "Nørregade 10",
    "city": "København",
    "postalCode": "1165",
    "country": "Denmark"
  },
  "createdDate": "2024-01-15T10:30:00Z",
  "updatedDate": "2024-01-15T10:30:00Z"
}
```

**Bemærk:** `deliveryAddress` kan være `null` hvis brugeren ikke har angivet en adresse endnu.

### 1.2 Update User Profile

**Endpoint:** `PUT /api/users/{userId}`

**Request Body - Opdateret Format:**
```json
{
  "email": "john@example.com",
  "name": "John Doe",
  "role": "Student",
  "deliveryAddress": {
    "street": "Nørregade 10",
    "city": "København",
    "postalCode": "1165",
    "country": "Denmark"
  }
}
```

**Alle felter er optional** - send kun de felter der skal opdateres.

**Validering:**
- `street`: Påkrævet hvis adresse angives, max 200 karakterer
- `city`: Påkrævet hvis adresse angives, max 100 karakterer
- `postalCode`: Påkrævet hvis adresse angives, skal være præcis 4 cifre (dansk postnummer)
- `country`: Optional, max 100 karakterer (default: "Denmark")

**Eksempel - Opdater kun adresse:**
```json
{
  "deliveryAddress": {
    "street": "Hovedgaden 25",
    "city": "Aarhus",
    "postalCode": "8000",
    "country": "Denmark"
  }
}
```

**Eksempel - Fjern adresse (sæt til null):**
```json
{
  "deliveryAddress": null
}
```

**Response (200 OK):**
Returnerer opdateret bruger med den nye adresse.

### 1.3 Create User

**Endpoint:** `POST /api/users`

**Request Body - Opdateret Format:**
```json
{
  "email": "newuser@example.com",
  "name": "New User",
  "role": "Student",
  "deliveryAddress": {
    "street": "Testvej 1",
    "city": "København",
    "postalCode": "2100",
    "country": "Denmark"
  }
}
```

**Bemærk:** `deliveryAddress` er optional ved oprettelse.

---

## 2. OrderService - Leveringsadresse i Ordre

### 2.1 Create Order

**Endpoint:** `POST /api/orders`

**Request Body - Opdateret Format:**
```json
{
  "customerId": "123e4567-e89b-12d3-a456-426614174000",
  "orderItems": [
    {
      "bookISBN": "1234567890123",
      "sellerId": "seller-123",
      "quantity": 1,
      "unitPrice": 29.99
    }
  ],
  "deliveryAddress": {
    "street": "Nørregade 10",
    "city": "København",
    "postalCode": "1165",
    "country": "Denmark"
  }
}
```

**Vigtigt:** 
- `deliveryAddress` er **optional** i request
- Hvis `deliveryAddress` ikke er angivet, hentes brugerens adresse automatisk fra UserService
- Hvis brugeren ikke har en adresse, returneres fejl: `"User must have a delivery address or provide one in the order"`

**Response (201 Created) - Opdateret Format:**
```json
{
  "orderId": "789e0123-e89b-12d3-a456-426614174002",
  "customerId": "123e4567-e89b-12d3-a456-426614174000",
  "orderDate": "2024-01-15T10:30:00Z",
  "totalAmount": 29.99,
  "status": "Pending",
  "deliveryAddress": {
    "street": "Nørregade 10",
    "city": "København",
    "postalCode": "1165",
    "country": "Denmark"
  },
  "orderItems": [
    {
      "orderItemId": "abc-123",
      "orderId": "789e0123-e89b-12d3-a456-426614174002",
      "bookISBN": "1234567890123",
      "sellerId": "seller-123",
      "quantity": 1,
      "unitPrice": 29.99,
      "status": "Pending"
    }
  ]
}
```

**Fejlscenarier:**

1. **Bruger har ingen adresse og ingen adresse i request:**
   ```json
   {
     "status": 400,
     "title": "Bad Request",
     "detail": "User must have a delivery address or provide one in the order"
   }
   ```

2. **Ugyldig postnummer:**
   ```json
   {
     "status": 400,
     "title": "One or more validation errors occurred.",
     "errors": {
       "deliveryAddress.postalCode": ["Postal code must be 4 digits"]
     }
   }
   ```

### 2.2 Get Order by ID

**Endpoint:** `GET /api/orders/{orderId}`

**Response - Opdateret Format:**
```json
{
  "orderId": "789e0123-e89b-12d3-a456-426614174002",
  "customerId": "123e4567-e89b-12d3-a456-426614174000",
  "orderDate": "2024-01-15T10:30:00Z",
  "totalAmount": 29.99,
  "status": "Paid",
  "paidDate": "2024-01-15T10:35:00Z",
  "deliveryAddress": {
    "street": "Nørregade 10",
    "city": "København",
    "postalCode": "1165",
    "country": "Denmark"
  },
  "orderItems": [...]
}
```

**Bemærk:** `deliveryAddress` er altid til stede i ordre-respons (ikke nullable).

---

## 3. UI Implementeringsguide

### 3.1 Profilside - Adresseformular

**Komponent:** User Profile Edit Form

**Felter:**
```typescript
interface DeliveryAddress {
  street: string;
  city: string;
  postalCode: string;
  country?: string;
}

interface UserProfile {
  email: string;
  name: string;
  role: string;
  deliveryAddress: DeliveryAddress | null;
}
```

**Validering:**
- Street: Required, max 200 karakterer
- City: Required, max 100 karakterer
- Postal Code: Required, præcis 4 cifre (regex: `^\d{4}$`)
- Country: Optional, max 100 karakterer

**Eksempel - React Component:**
```tsx
const AddressForm = ({ user, onSave }) => {
  const [address, setAddress] = useState<DeliveryAddress | null>(
    user.deliveryAddress || {
      street: '',
      city: '',
      postalCode: '',
      country: 'Denmark'
    }
  );

  const handleSubmit = async () => {
    // Valider postnummer
    if (!/^\d{4}$/.test(address.postalCode)) {
      alert('Postnummer skal være 4 cifre');
      return;
    }

    await fetch(`/api/users/${user.userId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ deliveryAddress: address })
    });
  };

  return (
    <form>
      <input 
        value={address.street} 
        onChange={(e) => setAddress({...address, street: e.target.value})}
        placeholder="Gadenavn og nummer"
        maxLength={200}
        required
      />
      <input 
        value={address.city} 
        onChange={(e) => setAddress({...address, city: e.target.value})}
        placeholder="By"
        maxLength={100}
        required
      />
      <input 
        value={address.postalCode} 
        onChange={(e) => setAddress({...address, postalCode: e.target.value})}
        placeholder="Postnummer (4 cifre)"
        pattern="\d{4}"
        maxLength={4}
        required
      />
      <input 
        value={address.country || 'Denmark'} 
        onChange={(e) => setAddress({...address, country: e.target.value})}
        placeholder="Land"
        maxLength={100}
      />
    </form>
  );
};
```

### 3.2 Checkout Side - Leveringsadresse

**Komponent:** Order Checkout Form

**Logik:**
1. Hvis brugeren har en adresse i profil → vis den som standard
2. Tillad brugeren at ændre adresse for denne ordre
3. Hvis brugeren ikke har adresse → kræv at de angiver en

**Eksempel:**
```tsx
const CheckoutForm = ({ user, cartItems, onCreateOrder }) => {
  const [useProfileAddress, setUseProfileAddress] = useState(
    !!user.deliveryAddress
  );
  const [deliveryAddress, setDeliveryAddress] = useState<DeliveryAddress | null>(
    user.deliveryAddress || {
      street: '',
      city: '',
      postalCode: '',
      country: 'Denmark'
    }
  );

  const handleCreateOrder = async () => {
    const orderData = {
      customerId: user.userId,
      orderItems: cartItems.map(item => ({
        bookISBN: item.isbn,
        sellerId: item.sellerId,
        quantity: item.quantity,
        unitPrice: item.price
      })),
      // Send kun adresse hvis den er ændret fra profil
      ...(useProfileAddress ? {} : { deliveryAddress })
    };

    try {
      const response = await fetch('/api/orders', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(orderData)
      });

      if (!response.ok) {
        const error = await response.json();
        if (error.detail?.includes('delivery address')) {
          alert('Du skal angive en leveringsadresse');
        }
        throw new Error(error.detail || 'Kunne ikke oprette ordre');
      }

      const order = await response.json();
      // Redirect til ordrebekræftelse
    } catch (error) {
      console.error('Order creation failed:', error);
    }
  };

  return (
    <div>
      {user.deliveryAddress && (
        <label>
          <input
            type="checkbox"
            checked={useProfileAddress}
            onChange={(e) => setUseProfileAddress(e.target.checked)}
          />
          Brug adresse fra profil
        </label>
      )}
      
      {!useProfileAddress && (
        <AddressForm 
          address={deliveryAddress}
          onChange={setDeliveryAddress}
        />
      )}
      
      <button onClick={handleCreateOrder}>
        Opret ordre
      </button>
    </div>
  );
};
```

### 3.3 Ordrevisning - Vis Leveringsadresse

**Komponent:** Order Details View

**Eksempel:**
```tsx
const OrderDetails = ({ order }) => {
  return (
    <div>
      <h2>Ordre #{order.orderId}</h2>
      <div>
        <h3>Leveringsadresse</h3>
        <p>{order.deliveryAddress.street}</p>
        <p>{order.deliveryAddress.postalCode} {order.deliveryAddress.city}</p>
        {order.deliveryAddress.country && (
          <p>{order.deliveryAddress.country}</p>
        )}
      </div>
      {/* Resten af ordreinformation */}
    </div>
  );
};
```

---

## 4. Fejlhåndtering

### 4.1 Valideringsfejl

**Format:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "deliveryAddress.street": ["Street address is required"],
    "deliveryAddress.postalCode": ["Postal code must be 4 digits"]
  }
}
```

**Håndtering:**
```typescript
try {
  const response = await fetch('/api/users/...', { ... });
  if (!response.ok) {
    const error = await response.json();
    if (error.errors) {
      // Vis valideringsfejl ved hvert felt
      Object.entries(error.errors).forEach(([field, messages]) => {
        showFieldError(field, messages[0]);
      });
    }
  }
} catch (error) {
  // Håndter netværksfejl
}
```

### 4.2 Manglende Adresse ved Ordreoprettelse

**Fejlbesked:**
```
"User must have a delivery address or provide one in the order"
```

**Løsning:**
- Vis en besked til brugeren
- Redirect til profilside for at tilføje adresse
- Eller vis adresseformular i checkout

---

## 5. Test Scenarier

### 5.1 Test Cases

1. **Opret bruger uden adresse**
   - ✅ Skal virke
   - Response: `deliveryAddress: null`

2. **Opdater bruger med adresse**
   - ✅ Skal virke
   - Response: `deliveryAddress` med værdier

3. **Opret ordre med adresse i request**
   - ✅ Skal virke
   - Response: Ordre med angivet adresse

4. **Opret ordre uden adresse i request, men bruger har adresse**
   - ✅ Skal virke
   - Response: Ordre med brugerens profiladresse

5. **Opret ordre uden adresse i request og bruger har ingen adresse**
   - ❌ Skal fejle med klar fejlbesked
   - Status: 400 Bad Request
   - Message: "User must have a delivery address or provide one in the order"

---

## 6. Migration Notes

### Database Changes

- **UserService:** Nye nullable kolonner tilføjet til `Users` tabel
  - `DeliveryStreet` (nvarchar(200), nullable)
  - `DeliveryCity` (nvarchar(100), nullable)
  - `DeliveryPostalCode` (nvarchar(10), nullable)
  - `DeliveryCountry` (nvarchar(100), nullable)

- **OrderService:** Nye required kolonner tilføjet til `Orders` tabel
  - `DeliveryStreet` (nvarchar(200), required)
  - `DeliveryCity` (nvarchar(100), required)
  - `DeliveryPostalCode` (nvarchar(10), required)
  - `DeliveryCountry` (nvarchar(100), nullable)

**Bemærk:** Eksisterende ordrer vil kræve en data migration hvis de allerede findes i databasen.

---

## 7. Breaking Changes

### Ingen Breaking Changes

- Alle nye felter er **additive** (tilføjet, ikke fjernet)
- Eksisterende endpoints virker stadig
- `deliveryAddress` er optional i UserService requests
- `deliveryAddress` er optional i OrderService requests (hentes automatisk hvis mangler)

---

## 8. Eksempler på Komplet Requests

### Opdater brugerprofil med adresse
```http
PUT /api/users/123e4567-e89b-12d3-a456-426614174000
Content-Type: application/json
Authorization: Bearer <token>

{
  "name": "John Doe",
  "deliveryAddress": {
    "street": "Nørregade 10",
    "city": "København",
    "postalCode": "1165",
    "country": "Denmark"
  }
}
```

### Opret ordre med eksplicit adresse
```http
POST /api/orders
Content-Type: application/json
Authorization: Bearer <token>

{
  "customerId": "123e4567-e89b-12d3-a456-426614174000",
  "orderItems": [
    {
      "bookISBN": "1234567890123",
      "sellerId": "seller-123",
      "quantity": 1,
      "unitPrice": 29.99
    }
  ],
  "deliveryAddress": {
    "street": "Hovedgaden 25",
    "city": "Aarhus",
    "postalCode": "8000",
    "country": "Denmark"
  }
}
```

### Opret ordre uden adresse (bruger profiladresse)
```http
POST /api/orders
Content-Type: application/json
Authorization: Bearer <token>

{
  "customerId": "123e4567-e89b-12d3-a456-426614174000",
  "orderItems": [
    {
      "bookISBN": "1234567890123",
      "sellerId": "seller-123",
      "quantity": 1,
      "unitPrice": 29.99
    }
  ]
}
```

---

## 9. Support

Ved spørgsmål eller problemer, kontakt backend teamet eller se:
- API Documentation: `API-DOCUMENTATION.md`
- Implementation Plan: `add-address-functionality.plan.md`



