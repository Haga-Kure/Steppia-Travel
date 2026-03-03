# Full API Logic – Frontend Agent Reference

Use this as the single source of truth for all Travel API endpoints. Common mistakes and ID/slug conventions are called out.

**Exact request fields (required/optional):** See `Travel/API_REQUEST_CONTRACT.md`. Use that file to ensure no required field is missing in requests and that all needed fields are shown in the frontend.

---

## Base URL & Auth

- **Base URL:** Your deployed API (e.g. `https://your-api.railway.app`)
- **Admin endpoints:** Require `Authorization: Bearer <JWT>`. Get JWT via `POST /admin/auth/login`.
- **Public endpoints:** No auth (tours, events, bookings, payments for customers).

---

## ID vs Slug Conventions

| Resource | Public routes use | Admin routes use | Notes |
|----------|-------------------|------------------|-------|
| **Tour** | `slug` (e.g. `zavkhan-overland-desert-highland-expedition`) | `id` (MongoDB ObjectId string) | Tour `id` = `tourId` for booking |
| **Tour date** | `id` from `GET /tours/{slug}/dates` | `id` from `GET /admin/tours/{tourId}/dates` | Date `id` = `tourDateId` for Group booking |
| **Event** | `slug` | `slug` | Both public and admin use slug |
| **Booking** | `bookingCode` (e.g. `BK-XXXXXX`) | `id` (MongoDB ObjectId) | Use `bookingCode` for customer-facing flows |

---

## 1. Health

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/health/mongo` | No | MongoDB connection check |

**Response:** `{ ok: true, result: "..." }` or 503 on failure.

---

## 2. Tours (Public)

### GET /tours

List active tours with pagination and filters.

**Query params:**

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| page | int | 1 | Page number |
| pageSize | int | 20 | Items per page (max 100) |
| search | string | - | Search in title, summary, description (regex, case-insensitive) |
| type | string | - | Filter by tour type |
| minPrice | decimal | - | Minimum base price |
| maxPrice | decimal | - | Maximum base price |
| minDuration | int | - | Minimum duration days |
| maxDuration | int | - | Maximum duration days |

**Response:**
```json
{
  "data": [ { "id", "slug", "title", "type", "summary", "description", "durationDays", "nights", "region", "basePrice", "currency", "itinerary", "images", "locations", ... } ],
  "page": 1,
  "pageSize": 20,
  "total": 50,
  "totalPages": 3
}
```

- Only **active** tours (`isActive: true`) are returned.

---

### GET /tours/{slug}

Get single tour by slug.

**Response:** Full `TourDto` or 404.

**Mistake:** Using `id` instead of `slug`. Public tour routes use **slug**, not id.

---

### GET /tours/{slug}/dates

Get available departure dates for a tour (Group tours only).

**Response:**
```json
[
  { "id": "...", "startDate": "...", "endDate": "...", "availableSpots": 5, "price": 2890, "currency": "USD" }
]
```

- Only dates with `availableSpots > 0` and `status = Open` are returned.
- Use `id` from the chosen date as **`tourDateId`** in `POST /bookings` for Group tours.
- `price` = `PriceOverride` if set on the date, else tour `BasePrice`.

---

## 3. Events (Public)

### GET /events

List active events with pagination.

**Query params:** `page`, `pageSize`, `search`, `type`, `year`, `region`

**Response:** `{ data: [...], page, pageSize, total, totalPages }`

---

### GET /events/{slug}

Get single event by slug. 404 if not found or inactive.

---

## 3.5. Agency Section (Public)

### GET /agency-section

Get current agency section (singleton: heading, subtitle, description, logoUrl, brandName, team cards). No auth.

**Response (200):** Single object:

- `heading`, `subtitle`, `description`, `logoUrl`, `brandName` (all optional strings)
- `team` (optional array of `{ id?, name, title, imageUrl }`; `id` is backend-generated)

If no section exists yet, returns 200 with all fields null/empty.

---

## 4. Bookings (Public)

### POST /bookings

Create a booking.

**Request body (CreateBookingRequest):**

```json
{
  "tourId": "507f1f77bcf86cd799439011",
  "tourType": "Group",
  "tourDateId": "507f1f77bcf86cd799439012",
  "travelDate": null,
  "contact": {
    "fullName": "John Doe",
    "email": "john@example.com",
    "phone": "+1234567890",
    "country": "USA"
  },
  "guests": [
    { "fullName": "John Doe", "age": 30, "passportNo": "AB123456" }
  ],
  "specialRequests": "Vegetarian meals"
}
```

**Rules by tourType:**

| tourType | tourDateId | travelDate |
|----------|------------|------------|
| **Group** | **Required** (from `GET /tours/{slug}/dates` → `id`) | Omit or null |
| **Private** | Omit or null | **Required** (ISO date, e.g. `"2026-07-15"`) |

**Required fields:**
- `tourId` – Tour `id` from `GET /tours/{slug}` (ObjectId string)
- `tourType` – Exactly `"Group"` or `"Private"` (case-insensitive in backend)
- `contact.fullName`, `contact.email`
- `guests` – At least one; each needs `fullName`

**Response:**
```json
{
  "bookingId": "...",
  "bookingCode": "BK-XXXXXX",
  "status": "PendingPayment",
  "expiresAt": "2026-01-29T12:00:00Z",
  "total": 2890.00,
  "currency": "USD"
}
```

**Common mistakes:**
- Using `tourDateId` where `tourId` is expected (or vice versa)
- Group tour without `tourDateId`
- Private tour without `travelDate`
- Wrong `tourType` casing (use `"Group"` / `"Private"`)
- Empty or missing `guests`

Booking expires in **30 minutes**. Payment must complete before expiry.

**Errors:** 400 (validation), 404 (tour not found), 409 (not enough seats for Group).

---

### GET /bookings/{bookingCode}

Get booking by code. Auto-expires if past `expiresAt`.

**Response:** Full booking with `tour` info, `pricing`, `contact`, `guests`, etc.

---

### GET /bookings/{bookingCode}/payment

Get payment info for a booking.

**Response:**
```json
{
  "bookingCode": "BK-XXXXXX",
  "hasPayment": true,
  "payment": { "id", "invoiceId", "provider", "amount", "currency", "status", "checkoutUrl", "qrText", ... }
}
```

Or `hasPayment: false` if no payment exists.

---

## 5. Payments (Public)

### POST /payments

Create a payment for a booking.

**Request body:**
```json
{
  "bookingCode": "BK-XXXXXX",
  "provider": "stripe"
}
```

**Required:** `bookingCode`, `provider` (e.g. `"stripe"`, `"qpay"`, `"manual"`).

**Response:**
```json
{
  "paymentId": "...",
  "status": "Pending",
  "provider": "stripe",
  "invoiceId": "INV-...",
  "providerCheckoutUrl": "https://...",
  "providerQrText": "..."
}
```

**Errors:** 400 (missing fields), 404 (booking not found), 409 (booking expired or status not PendingPayment).

**Order:** Must create booking first, then payment. Use `bookingCode` from create-booking response.

---

### GET /payments/{invoiceId}

Get payment status by invoice ID. Public, no auth.

**Response:** Payment details + linked booking summary.

---

### POST /payments/webhook

Webhook for payment provider (e.g. when user pays). Sends `InvoiceId` and `Status`.

**Request body:**
```json
{
  "invoiceId": "INV-...",
  "status": "paid"
}
```

**Status values:** `"paid"`, `"failed"` (case-insensitive).

When `status = "paid"`: payment marked paid, booking set to `Confirmed` if still `PendingPayment` and not expired.

---

## 6. Auth (Admin)

### POST /admin/auth/login

**Request body:**
```json
{
  "username": "admin",
  "password": "admin123"
}
```

**Response:** `{ token, username, role, expiresAt }`

Use `token` in `Authorization: Bearer <token>` for admin endpoints.

---

### GET /admin/auth/me

Current admin user. Requires Bearer token.

---

### POST /admin/auth/refresh

Refresh JWT. Requires Bearer token.

---

### PUT /admin/auth/change-password

**Request body:** `{ currentPassword, newPassword }`

---

## 7. Admin Tours

All require `Authorization: Bearer <token>`.

### GET /admin/tours

List all tours (including inactive). Pagination: `page`, `pageSize` (default 20, max 100).

**Response:** `{ data: [...], page, pageSize, total, totalPages }`

---

### POST /admin/tours

Create tour.

**Request body (CreateTourRequest):**
- **Required:** `slug`, `title`, `type`, `durationDays`, `basePrice`, `currency`, `locations`, `images`
- `locations`: `[{ name, latitude?, longitude? }]`
- `images`: `[{ url, alt?, isCover }]`
- `itinerary`: optional `[{ day, title, notes, breakfast, lunch, dinner, accommodation, stay, distanceKm, startPlace, endPlace, firstSegmentDistanceKm, routeWaypoints: [{ place, distanceToNextKm }], imageUrl }]`

**Error:** 400 if slug already exists.

---

### PUT /admin/tours/{id}

Update tour. Use **`id`** (ObjectId), not slug.

**Request body (UpdateTourRequest):** All fields optional. Only set fields you want to change.

- `slug`, `title`, `type`, `summary`, `description`, `overview`, `subtitle`, `bobbleTitle`
- `durationDays`, `nights`, `basePrice`, `currency`
- `locations`, `images`, `itinerary`, `region`, `totalDistanceKm`, `highlights`, `included`, `excluded`
- `travelStyle`, `activities`, `idealFor`, `difficulty`, `groupSize`, `accommodation`
- `clearAccommodation: true` – clears accommodation
- `isActive`

**Mistake:** Using slug in URL. Admin tour update uses **`id`** in path.

---

### DELETE /admin/tours/{id}

Soft-delete (sets `isActive: false`). Use **`id`** in path.

---

### GET /admin/tours/{tourId}/dates

List tour dates for a tour. Use **`tourId`** (ObjectId) in path.

**Response:** `[{ id, tourId, startDate, endDate, capacity, priceOverride, status, createdAt, updatedAt }]`

---

### POST /admin/tours/{tourId}/dates

Create tour date.

**Request body:**
```json
{
  "startDate": "2026-07-01T00:00:00Z",
  "endDate": "2026-07-10T00:00:00Z",
  "capacity": 10,
  "priceOverride": 2890,
  "status": "Open"
}
```

- `startDate` must be before `endDate`
- `capacity` > 0
- `status`: `Open`, `Closed`, etc. (optional, default `Open`)

---

### PUT /admin/tour-dates/{id}

Update tour date. Use date **`id`** in path.

---

### DELETE /admin/tour-dates/{id}

Delete tour date.

---

### GET /admin/tour-dates/{id}

Get single tour date by id.

---

## 8. Admin Events

All require Bearer token. Use **slug** in paths.

### GET /admin/events

List all events. Pagination: `page`, `pageSize`.

---

### GET /admin/events/{slug}

Get event by slug.

---

### POST /admin/events

Create event.

**Request body (CreateEventRequest):**
- **Required:** `slug`, `title`, `event` (with `name`, `type`, `year`)
- Plus: `summary`, `description`, `durationDays`, `nights`, `startDate`, `endDate`, `bestSeason`, `region`, `locations`, `travelStyle`, `difficulty`, `groupType`, `maxGroupSize`, `priceUSD`, `includes`, `excludes`, `highlights`, `images`

---

### PUT /admin/events/{slug}

Update event. Use slug in path.

---

### DELETE /admin/events/{slug}

Soft-delete event (sets `isActive: false`).

---

## 8.5. Admin Agency Section

### PUT /admin/agency-section

Replace the full agency section (singleton). Body: same JSON shape as GET /agency-section. Add card = include new object in `team` (no `id` or temporary id); delete card = send `team` without that member. Responds 200 with the saved object. Requires admin auth.

---

## 9. Admin Bookings

All require Bearer token.

### GET /admin/bookings

List bookings with filters.

**Query params:**
- `page`, `pageSize` (default 20, max 100)
- `status` – PendingPayment, Confirmed, Cancelled, Expired
- `tourId` – filter by tour
- `startDate`, `endDate` – filter by `createdAt`

**Response:** Array of `AdminBookingDto` (no pagination metadata in response – frontend must track page/size if needed).

---

### GET /admin/bookings/{id}

Get booking by **`id`** (ObjectId). Use `id`, not `bookingCode`.

---

### PUT /admin/bookings/{id}/status

Update booking status.

**Request body:** `{ "status": "Confirmed" }`

Valid: `PendingPayment`, `Confirmed`, `Cancelled`, `Expired`. Cannot change from `Cancelled`.

---

### GET /admin/bookings/stats

Booking statistics (counts by status, revenue, etc.).

---

## 10. Quick Reference – Booking Flow

```
1. GET /tours/{slug}           → get tourId (id)
2. GET /tours/{slug}/dates     → get tourDateId (id) for Group
3. POST /bookings             → create booking (Group: tourDateId, Private: travelDate)
4. POST /payments             → create payment with bookingCode
5. Redirect user to providerCheckoutUrl
6. Webhook confirms → booking status = Confirmed
7. GET /bookings/{bookingCode} → show confirmation
```

---

## 11. Common Frontend Mistakes

| Mistake | Fix |
|---------|-----|
| Using `tourDateId` as `tourId` | `tourId` = tour id, `tourDateId` = date id |
| Group without `tourDateId` | Get from `GET /tours/{slug}/dates` |
| Private without `travelDate` | Send ISO date string |
| Wrong URL for tour update | Use `PUT /admin/tours/{id}` with **id**, not slug |
| Event URLs | Admin events use **slug** in path |
| Booking vs payment order | Always create booking first, then payment |
| Using slug for booking lookup | Customer flow uses `bookingCode` |
| Admin booking by code | Admin uses `id` (ObjectId) not `bookingCode` |
