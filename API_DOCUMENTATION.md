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

Get all active tours with pagination, search, and filtering.

**Query Parameters:**
- `page` (int, optional) - Page number (default: 1)
- `pageSize` (int, optional) - Items per page (default: 20, max: 100)
- `search` (string, optional) - Search in title, summary, description
- `type` (string, optional) - Filter by tour type: "Group" or "Private"
- `minPrice` (decimal, optional) - Minimum price filter
- `maxPrice` (decimal, optional) - Maximum price filter
- `minDuration` (int, optional) - Minimum duration in days
- `maxDuration` (int, optional) - Maximum duration in days

**Example:**
```
GET /tours?page=1&pageSize=20&type=Group&minPrice=1000&maxPrice=5000&search=mongolia
```

**Response:**
```json
{
  "data": [
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
  ],
  "page": 1,
  "pageSize": 20,
  "total": 15,
  "totalPages": 1
}
```

**Status Codes:**
- `200` - Success

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

### 3.1. Get Tour Dates (Public)

**GET** `/tours/{slug}/dates`

Get available tour dates for a specific tour.

**Parameters:**
- `slug` (path) - Tour slug

**Response:**
```json
[
  {
    "id": "507f1f77bcf86cd799439011",
    "startDate": "2024-06-15T00:00:00Z",
    "endDate": "2024-06-22T00:00:00Z",
    "availableSpots": 8,
    "price": 3090,
    "currency": "USD"
  }
]
```

**Status Codes:**
- `200` - Success
- `404` - Tour not found

**Notes:**
- Only returns dates with status "Open" and available spots > 0
- Only returns future dates
- Available spots = capacity - (pending + confirmed bookings)

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

### 8. Get Payment Status by Invoice ID

**GET** `/payments/{invoiceId}`

Get payment status by invoice ID.

**Parameters:**
- `invoiceId` (path) - Payment invoice ID (e.g., `INV-1234567890ABCDEF`)

**Response:**
```json
{
  "id": "507f1f77bcf86cd799439013",
  "invoiceId": "INV-1234567890ABCDEF",
  "bookingId": "507f1f77bcf86cd799439012",
  "bookingCode": "BK-ABC123",
  "provider": "stripe",
  "amount": 3090,
  "currency": "USD",
  "status": "Paid",
  "checkoutUrl": "https://example.com/checkout/INV-1234567890ABCDEF",
  "qrText": "INV-1234567890ABCDEF",
  "createdAt": "2024-01-18T02:00:00Z",
  "updatedAt": "2024-01-18T02:05:00Z",
  "booking": {
    "bookingCode": "BK-ABC123",
    "status": "Confirmed",
    "total": 3090,
    "currency": "USD"
  }
}
```

**Status Codes:**
- `200` - Payment found
- `404` - Payment not found

---

### 9. Get Payment Status for Booking

**GET** `/bookings/{bookingCode}/payment`

Get payment status for a specific booking.

**Parameters:**
- `bookingCode` (path) - Booking code (e.g., `BK-ABC123`)

**Response:**
```json
{
  "bookingCode": "BK-ABC123",
  "hasPayment": true,
  "payment": {
    "id": "507f1f77bcf86cd799439013",
    "invoiceId": "INV-1234567890ABCDEF",
    "provider": "stripe",
    "amount": 3090,
    "currency": "USD",
    "status": "Paid",
    "checkoutUrl": "https://example.com/checkout/INV-1234567890ABCDEF",
    "qrText": "INV-1234567890ABCDEF",
    "createdAt": "2024-01-18T02:00:00Z",
    "updatedAt": "2024-01-18T02:05:00Z"
  }
}
```

**Status Codes:**
- `200` - Booking found (payment may or may not exist)
- `404` - Booking not found

---

## Admin Endpoints

All admin endpoints require JWT authentication. Include the token in the `Authorization` header:

```
Authorization: Bearer <your-jwt-token>
```

### Authentication

#### Admin Login

**POST** `/admin/auth/login`

Authenticate admin user and receive JWT token.

**Request Body:**
```json
{
  "username": "admin",
  "password": "admin123"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "username": "admin",
  "role": "admin",
  "expiresAt": "2024-01-18T10:00:00Z"
}
```

**Status Codes:**
- `200` - Login successful
- `400` - Bad request (missing username/password)
- `401` - Unauthorized (invalid credentials)

---

#### Get Current Admin User

**GET** `/admin/auth/me`

Get current authenticated admin user information.

**Headers:**
- `Authorization: Bearer <token>`

**Response:**
```json
{
  "username": "admin",
  "email": "admin@example.com",
  "role": "admin",
  "lastLoginAt": "2024-01-18T02:00:00Z"
}
```

**Status Codes:**
- `200` - Success
- `401` - Unauthorized

---

#### Refresh Token

**POST** `/admin/auth/refresh`

Get a new JWT token (extends expiration).

**Headers:**
- `Authorization: Bearer <token>`

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "username": "admin",
  "role": "admin",
  "expiresAt": "2024-01-18T10:00:00Z"
}
```

**Status Codes:**
- `200` - Token refreshed
- `401` - Unauthorized

---

#### Logout

**POST** `/admin/auth/logout`

Logout (client-side token removal).

**Headers:**
- `Authorization: Bearer <token>`

**Response:**
```json
{
  "message": "Logged out successfully"
}
```

**Status Codes:**
- `200` - Success
- `401` - Unauthorized

---

### Admin Tours Management

#### Get All Tours (Admin)

**GET** `/admin/tours`

Get all tours including inactive ones with pagination.

**Headers:**
- `Authorization: Bearer <token>`

**Query Parameters:**
- `page` (int, optional) - Page number (default: 1)
- `pageSize` (int, optional) - Items per page (default: 20, max: 100)

**Response:**
```json
{
  "data": [
    {
      "id": "69693d293953f333a63ad670",
      "slug": "khovd-overland-multi-ethnic-altai-expedition-7d",
      "title": "Khovd Overland Multi-Ethnic & Altai Expedition",
      "type": "Group",
      "summary": "Explore western Mongolia's dramatic lakes...",
      "description": "Khovd is where Mongolia's landscapes...",
      "durationDays": 7,
      "basePrice": 3090,
      "currency": "USD",
      "locations": ["Ulaanbaatar", "Khovd"],
      "images": [
        {
          "url": "https://example.com/images/khovd-cover.jpg",
          "alt": "Khar-Us Lake",
          "isCover": true
        }
      ],
      "isActive": true,
      "createdAt": "2024-01-16T00:00:00Z",
      "updatedAt": "2024-01-16T00:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 25,
  "totalPages": 2
}
```

**Status Codes:**
- `200` - Success
- `401` - Unauthorized

---

#### Create Tour

**POST** `/admin/tours`

Create a new tour.

**Headers:**
- `Authorization: Bearer <token>`

**Request Body:**
```json
{
  "slug": "new-tour-slug",
  "title": "New Tour Title",
  "type": "Group",
  "summary": "Tour summary",
  "description": "Full tour description",
  "durationDays": 7,
  "basePrice": 3090,
  "currency": "USD",
  "locations": ["Location 1", "Location 2"],
  "images": [
    {
      "url": "https://example.com/image.jpg",
      "alt": "Image alt text",
      "isCover": true
    }
  ]
}
```

**Response:**
```json
{
  "id": "69693d293953f333a63ad670",
  "slug": "new-tour-slug",
  "title": "New Tour Title",
  "type": "Group",
  "summary": "Tour summary",
  "description": "Full tour description",
  "durationDays": 7,
  "basePrice": 3090,
  "currency": "USD",
  "locations": ["Location 1", "Location 2"],
  "images": [
    {
      "url": "https://example.com/image.jpg",
      "alt": "Image alt text",
      "isCover": true
    }
  ],
  "isActive": true,
  "createdAt": "2024-01-18T02:00:00Z",
  "updatedAt": "2024-01-18T02:00:00Z"
}
```

**Status Codes:**
- `201` - Tour created
- `400` - Bad request (validation error or duplicate slug)
- `401` - Unauthorized

---

#### Update Tour

**PUT** `/admin/tours/{id}`

Update an existing tour.

**Headers:**
- `Authorization: Bearer <token>`

**Parameters:**
- `id` (path) - Tour ID

**Request Body (all fields optional):**
```json
{
  "slug": "updated-slug",
  "title": "Updated Title",
  "type": "Private",
  "summary": "Updated summary",
  "description": "Updated description",
  "durationDays": 10,
  "basePrice": 4000,
  "currency": "USD",
  "locations": ["New Location"],
  "images": [
    {
      "url": "https://example.com/new-image.jpg",
      "alt": "New image",
      "isCover": false
    }
  ],
  "isActive": true
}
```

**Response:**
```json
{
  "id": "69693d293953f333a63ad670",
  "slug": "updated-slug",
  "title": "Updated Title",
  "type": "Private",
  "summary": "Updated summary",
  "description": "Updated description",
  "durationDays": 10,
  "basePrice": 4000,
  "currency": "USD",
  "locations": ["New Location"],
  "images": [
    {
      "url": "https://example.com/new-image.jpg",
      "alt": "New image",
      "isCover": false
    }
  ],
  "isActive": true,
  "createdAt": "2024-01-16T00:00:00Z",
  "updatedAt": "2024-01-18T02:00:00Z"
}
```

**Status Codes:**
- `200` - Tour updated
- `400` - Bad request (validation error or duplicate slug)
- `401` - Unauthorized
- `404` - Tour not found

---

#### Delete Tour

**DELETE** `/admin/tours/{id}`

Soft delete a tour (sets `isActive` to `false`).

**Headers:**
- `Authorization: Bearer <token>`

**Parameters:**
- `id` (path) - Tour ID

**Response:**
```json
{
  "message": "Tour deleted successfully"
}
```

**Status Codes:**
- `200` - Tour deleted
- `400` - Bad request (invalid ID)
- `401` - Unauthorized
- `404` - Tour not found

---

### Admin Bookings Management

#### Get All Bookings

**GET** `/admin/bookings`

Get all bookings with optional filters and pagination.

**Headers:**
- `Authorization: Bearer <token>`

**Query Parameters:**
- `page` (int, optional) - Page number (default: 1)
- `pageSize` (int, optional) - Items per page (default: 20, max: 100)
- `status` (string, optional) - Filter by status: `PendingPayment`, `Confirmed`, `Cancelled`, `Expired`
- `tourId` (string, optional) - Filter by tour ID
- `startDate` (DateTime, optional) - Filter bookings created after this date (ISO 8601)
- `endDate` (DateTime, optional) - Filter bookings created before this date (ISO 8601)

**Example:**
```
GET /admin/bookings?page=1&pageSize=20&status=Confirmed&startDate=2024-01-01T00:00:00Z
```

**Response:**
```json
{
  "data": [
    {
      "id": "507f1f77bcf86cd799439012",
    "bookingCode": "BK-ABC123",
    "status": "Confirmed",
    "expiresAt": "2024-01-18T02:30:00Z",
    "tourId": "69693d293953f333a63ad670",
    "tour": {
      "id": "69693d293953f333a63ad670",
      "title": "Khovd Overland Multi-Ethnic & Altai Expedition",
      "slug": "khovd-overland-multi-ethnic-altai-expedition-7d",
      "basePrice": 3090,
      "currency": "USD",
      "images": [
        {
          "url": "https://example.com/image.jpg",
          "alt": "Image alt",
          "isCover": true
        }
      ]
    },
    "tourDateId": null,
    "travelDate": "2024-12-25",
    "tourType": "Private",
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
    "guestCount": 1,
    "pricing": {
      "currency": "USD",
      "subtotal": 3090,
      "discount": 0,
      "tax": 0,
      "total": 3090
    },
    "specialRequests": "Window seat preferred",
    "createdAt": "2024-01-18T02:00:00Z",
    "updatedAt": "2024-01-18T02:00:00Z"
  }
]
```

**Status Codes:**
- `200` - Success
- `401` - Unauthorized

---

#### Get Booking by ID

**GET** `/admin/bookings/{id}`

Get booking details with full tour information.

**Headers:**
- `Authorization: Bearer <token>`

**Parameters:**
- `id` (path) - Booking ID

**Response:**
Same format as "Get All Bookings" but returns a single booking object.

**Status Codes:**
- `200` - Booking found
- `400` - Bad request (invalid ID)
- `401` - Unauthorized
- `404` - Booking not found

---

#### Update Booking Status

**PUT** `/admin/bookings/{id}/status`

Update booking status.

**Headers:**
- `Authorization: Bearer <token>`

**Parameters:**
- `id` (path) - Booking ID

**Request Body:**
```json
{
  "status": "Confirmed"
}
```

**Valid Status Values:**
- `PendingPayment`
- `Confirmed`
- `Cancelled`
- `Expired`

**Response:**
```json
{
  "message": "Booking status updated successfully",
  "status": "Confirmed"
}
```

**Status Codes:**
- `200` - Status updated
- `400` - Bad request (invalid ID or status, or invalid transition)
- `401` - Unauthorized
- `404` - Booking not found

**Validation Rules:**
- Cannot change status from `Cancelled` to any other status

---

#### Get Booking Statistics

**GET** `/admin/bookings/stats`

Get booking statistics and revenue data.

**Headers:**
- `Authorization: Bearer <token>`

**Response:**
```json
{
  "totalBookings": 150,
  "pendingPayment": 25,
  "confirmed": 100,
  "cancelled": 15,
  "expired": 10,
  "totalRevenue": 309000,
  "pendingRevenue": 77250,
  "bookingsByStatus": {
    "PendingPayment": 25,
    "Confirmed": 100,
    "Cancelled": 15,
    "Expired": 10
  },
  "revenueByStatus": {
    "PendingPayment": 77250,
    "Confirmed": 309000,
    "Cancelled": 0,
    "Expired": 0
  }
}
```

**Status Codes:**
- `200` - Success
- `401` - Unauthorized

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

## Authentication

### JWT Token Usage

1. **Login** to `/admin/auth/login` with username and password
2. **Receive** JWT token in response
3. **Include** token in `Authorization` header for all admin endpoints:
   ```
   Authorization: Bearer <your-jwt-token>
   ```
4. **Token expires** after 8 hours (use `/admin/auth/refresh` to extend)

### Default Admin Credentials

On first startup, if no admin users exist, a default admin is created:
- **Username**: `admin` (or from `ADMIN_USERNAME` env var)
- **Password**: `admin123` (or from `ADMIN_PASSWORD` env var)

**⚠️ IMPORTANT**: Change the default password in production!

---

## Environment Variables

### Required for Admin Features

- `JWT_SECRET` - Secret key for JWT signing (minimum 32 characters)
- `JWT_ISSUER` - JWT issuer (default: "steppia-travel-api")
- `JWT_AUDIENCE` - JWT audience (default: "steppia-travel-admin")

### Optional

- `ADMIN_USERNAME` - Initial admin username (default: "admin")
- `ADMIN_PASSWORD` - Initial admin password (default: "admin123")

---

## Notes

- All dates are in ISO 8601 format (UTC)
- Booking codes expire after 30 minutes if payment is not completed
- Group tours require `tourDateId` and check capacity
- Private tours require `travelDate`
- Currency is typically "USD"
- All prices are in the tour's currency
- Admin endpoints require JWT authentication
- JWT tokens expire after 8 hours
- Tour deletion is soft delete (sets `isActive=false`)
