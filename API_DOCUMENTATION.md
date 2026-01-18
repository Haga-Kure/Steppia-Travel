# Travel API Documentation

**Base URL**: `https://steppia-travel-production.up.railway.app`

## Endpoints

### 1. Health Check

**GET** `/health/mongo`

Check MongoDB connection status.

**Response:**
```json
{
  "ok": true,
  "result": "..."
}
```

---

### 2. List All Tours

**GET** `/tours`

Get all active tours.

**Response:**
```json
[
  {
    "id": "69693d293953f333a63ad670",
    "slug": "khovd-overland-multi-ethnic-altai-expedition-7d",
    "title": "Khovd Overland Multi-Ethnic & Altai Expedition",
    "type": "Group",
    "summary": "Explore western Mongolia's dramatic lakes...",
    "durationDays": 7,
    "basePrice": 3090,
    "currency": "USD",
    "locations": ["Ulaanbaatar", "Khovd", "Khar-Us Lake", "Munkhkhairkhan"]
  }
]
```

---

### 3. Get Tour by Slug

**GET** `/tours/{slug}`

Get a specific tour by its slug.

**Parameters:**
- `slug` (path) - Tour slug (e.g., `khovd-overland-multi-ethnic-altai-expedition-7d`)

**Response:**
```json
{
  "id": {
    "timestamp": 1768504617,
    "creationTime": "2026-01-15T19:16:57Z"
  },
  "slug": "khovd-overland-multi-ethnic-altai-expedition-7d",
  "title": "Khovd Overland Multi-Ethnic & Altai Expedition",
  "type": "Group",
  "summary": "Explore western Mongolia's dramatic lakes...",
  "description": "Khovd is where Mongolia's landscapes...",
  "durationDays": 7,
  "basePrice": 3090,
  "currency": "USD",
  "locations": ["Ulaanbaatar", "Khovd", "Khar-Us Lake", "Munkhkhairkhan"],
  "images": [
    {
      "url": "https://example.com/images/khovd-cover.jpg",
      "alt": "Khar-Us Lake and Altai landscapes",
      "isCover": true
    }
  ],
  "isActive": true,
  "createdAt": "2026-01-16T00:00:00Z",
  "updatedAt": "2026-01-16T00:00:00Z"
}
```

**Status Codes:**
- `200` - Tour found
- `404` - Tour not found

---

### 4. Create Booking

**POST** `/bookings`

Create a new booking.

**Request Body:**
```json
{
  "tourId": "69693d293953f333a63ad670",
  "tourType": "Private",
  "travelDate": "2024-12-25",
  "tourDateId": "507f1f77bcf86cd799439011",  // Required for Group tours
  "contact": {
    "fullName": "John Doe",
    "email": "john@example.com",
    "phone": "+1234567890",
    "country": "USA"
  },
  "guests": [
    {
      "fullName": "John Doe",
      "age": 30,
      "passportNo": "AB123456"
    }
  ],
  "specialRequests": "Window seat preferred"
}
```

**Response:**
```json
{
  "id": "507f1f77bcf86cd799439012",
  "bookingCode": "BK-ABC123",
  "status": "PendingPayment",
  "expiresAt": "2024-01-18T02:30:00Z",
  "total": 3090,
  "currency": "USD"
}
```

**Status Codes:**
- `200` - Booking created successfully
- `400` - Bad request (validation error)
- `404` - Tour not found
- `409` - Conflict (not enough seats for Group tours)

**Validation Rules:**
- `tourId` - Required, must be valid ObjectId
- `tourType` - Required, must be "Private" or "Group"
- `travelDate` - Required for Private tours
- `tourDateId` - Required for Group tours
- `contact.fullName` - Required
- `contact.email` - Required
- `guests` - Required, must have at least 1 guest

---

### 5. Get Booking by Code

**GET** `/bookings/{bookingCode}`

Get booking details by booking code.

**Parameters:**
- `bookingCode` (path) - Booking code (e.g., `BK-ABC123`)

**Response:**
```json
{
  "id": "507f1f77bcf86cd799439012",
  "bookingCode": "BK-ABC123",
  "status": "PendingPayment",
  "expiresAt": "2024-01-18T02:30:00Z",
  "tourId": "69693d293953f333a63ad670",
  "tourDateId": null,
  "travelDate": "2024-12-25",
  "tourType": "Private",
  "contact": {
    "fullName": "John Doe",
    "email": "john@example.com",
    "phone": "+1234567890",
    "country": "USA"
  },
  "guestCount": 1,
  "pricing": {
    "currency": "USD",
    "subtotal": 3090,
    "discount": 0,
    "tax": 0,
    "total": 3090
  },
  "createdAt": "2024-01-18T02:00:00Z"
}
```

**Status Codes:**
- `200` - Booking found
- `404` - Booking not found

---

### 6. Create Payment

**POST** `/payments`

Create a payment for a booking.

**Request Body:**
```json
{
  "bookingCode": "BK-ABC123",
  "provider": "stripe"
}
```

**Response:**
```json
{
  "id": "507f1f77bcf86cd799439013",
  "status": "Pending",
  "provider": "stripe",
  "invoiceId": "INV-1234567890ABCDEF",
  "checkoutUrl": "https://example.com/checkout/INV-1234567890ABCDEF",
  "qrText": "INV-1234567890ABCDEF"
}
```

**Status Codes:**
- `200` - Payment created
- `400` - Bad request
- `404` - Booking not found
- `409` - Booking not eligible for payment (expired or wrong status)

---

### 7. Payment Webhook

**POST** `/payments/webhook`

Webhook endpoint for payment providers to notify about payment status.

**Request Body:**
```json
{
  "invoiceId": "INV-1234567890ABCDEF",
  "status": "paid"
}
```

**Status Values:**
- `paid` - Payment successful, booking confirmed
- `failed` - Payment failed

**Response:**
```json
{
  "ok": true,
  "message": "Payment marked paid; booking confirmed if eligible."
}
```

**Status Codes:**
- `200` - Webhook processed
- `400` - Bad request
- `404` - Payment not found

---

## Swagger Documentation

Interactive API documentation available at:
**https://steppia-travel-production.up.railway.app/swagger**

---

## Error Responses

All endpoints may return error responses in this format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "TourId is required."
}
```

---

## Notes

- All dates are in ISO 8601 format (UTC)
- Booking codes expire after 30 minutes if payment is not completed
- Group tours require `tourDateId` and check capacity
- Private tours require `travelDate`
- Currency is typically "USD"
- All prices are in the tour's currency
