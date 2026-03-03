# API Request Contract – Exact Fields for Frontend

**Use this as the single source of truth.** The frontend must send exactly these fields. Do not omit any REQUIRED field. For forms: show every REQUIRED field in the UI; add OPTIONAL fields in the UI when they are needed for the feature.

---

## How to use (for frontend agent)

1. **Before building or changing any API request:** Open this file and find the request type (e.g. `POST /bookings`).
2. **REQUIRED** = Must be sent every time. If the form does not collect it, add the field to the UI.
3. **OPTIONAL** = May be omitted or null. Only include in the request if the user has provided a value (or send null/omit).
4. **Conditional** = Required only when a condition is met (e.g. Group vs Private). See the condition column.
5. After implementing, verify: every REQUIRED field is present in the payload and visible/collectible in the UI where relevant.

---

## POST /bookings – CreateBookingRequest

| Field | Required? | Type | Condition / Notes |
|-------|-----------|------|-------------------|
| `tourId` | **REQUIRED** | string | Tour `id` from `GET /tours/{slug}` (ObjectId). |
| `tourType` | **REQUIRED** | string | Exactly `"Group"` or `"Private"`. |
| `tourDateId` | **CONDITIONAL** | string \| null | **Required when tourType is "Group".** From `GET /tours/{slug}/dates` → `id`. Omit or null for Private. |
| `travelDate` | **CONDITIONAL** | string (ISO date) \| null | **Required when tourType is "Private".** e.g. `"2026-07-15"`. Omit or null for Group. |
| `contact` | **REQUIRED** | object | See `contact` below. |
| `guests` | **REQUIRED** | array | At least one guest. See `guests[]` below. |
| `specialRequests` | OPTIONAL | string \| null | Free text. |

### contact (object)

| Field | Required? | Type |
|-------|-----------|------|
| `fullName` | **REQUIRED** | string |
| `email` | **REQUIRED** | string |
| `phone` | OPTIONAL | string \| null |
| `country` | OPTIONAL | string \| null |

### guests[] (array of objects)

| Field | Required? | Type |
|-------|-----------|------|
| `fullName` | **REQUIRED** | string |
| `age` | OPTIONAL | number \| null |
| `passportNo` | OPTIONAL | string \| null |

**UI checklist for booking form:**  
Collect: `tourId`, `tourType`, `tourDateId` (if Group), `travelDate` (if Private), `contact.fullName`, `contact.email`, `contact.phone`, `contact.country`, at least one guest with `fullName`, and `specialRequests` if desired.

---

## POST /payments – CreatePaymentRequest

| Field | Required? | Type |
|-------|-----------|------|
| `bookingCode` | **REQUIRED** | string |
| `provider` | **REQUIRED** | string |

**UI checklist:** Use `bookingCode` from the create-booking response. Provider is chosen by user (e.g. `"stripe"`).

---

## POST /admin/auth/login – LoginRequest

| Field | Required? | Type |
|-------|-----------|------|
| `username` | **REQUIRED** | string |
| `password` | **REQUIRED** | string |

---

## POST /admin/tours – CreateTourRequest

| Field | Required? | Type |
|-------|-----------|------|
| `slug` | **REQUIRED** | string |
| `title` | **REQUIRED** | string |
| `type` | **REQUIRED** | string |
| `durationDays` | **REQUIRED** | number |
| `basePrice` | **REQUIRED** | number |
| `currency` | **REQUIRED** | string |
| `locations` | **REQUIRED** | array of `{ name, latitude?, longitude? }` |
| `images` | **REQUIRED** | array of `{ url, alt?, isCover }` |
| `summary` | OPTIONAL | string \| null |
| `description` | OPTIONAL | string \| null |
| `itinerary` | OPTIONAL | array \| null |

---

## PUT /admin/tours/{id} – UpdateTourRequest

All fields are OPTIONAL (partial update). Send only fields that are being changed.

| Field | Type |
|-------|------|
| `title`, `subtitle`, `summary`, `slug`, `bobbleTitle`, `description`, `overview`, `type` | string \| null |
| `durationDays`, `nights` | number \| null |
| `basePrice` | number \| null |
| `currency`, `region`, `difficulty`, `groupSize` | string \| null |
| `totalDistanceKm` | number \| null |
| `locations` | array \| null |
| `accommodation` | object \| null |
| `clearAccommodation` | boolean \| null |
| `images`, `highlights`, `included`, `excluded`, `travelStyle`, `activities`, `idealFor` | array \| null |
| `itinerary` | array \| null |
| `isActive` | boolean \| null |

---

## POST /admin/tours/{tourId}/dates – CreateTourDateRequest

| Field | Required? | Type |
|-------|-----------|------|
| `startDate` | **REQUIRED** | string (ISO datetime) |
| `endDate` | **REQUIRED** | string (ISO datetime), must be after startDate |
| `capacity` | **REQUIRED** | number (integer > 0) |
| `priceOverride` | OPTIONAL | number \| null |
| `status` | OPTIONAL | string \| null (e.g. `"Open"`) |

---

## PUT /admin/bookings/{id}/status – UpdateBookingStatusRequest

| Field | Required? | Type |
|-------|-----------|------|
| `status` | **REQUIRED** | string |

Allowed values: `"PendingPayment"`, `"Confirmed"`, `"Cancelled"`, `"Expired"`.

---

## POST /admin/events – CreateEventRequest

| Field | Required? | Type |
|-------|-----------|------|
| `slug` | **REQUIRED** | string |
| `title` | **REQUIRED** | string |
| `event` | **REQUIRED** | object: `{ name, type, year }` |
| `durationDays` | **REQUIRED** | number |
| `priceUSD` | **REQUIRED** | number |
| `summary` | OPTIONAL | string \| null |
| `description` | OPTIONAL | string \| null |
| `nights` | OPTIONAL | number \| null |
| `startDate`, `endDate` | OPTIONAL | string (ISO) \| null |
| `bestSeason`, `region`, `difficulty`, `groupType` | OPTIONAL | string \| null |
| `maxGroupSize` | OPTIONAL | number \| null |
| `locations`, `travelStyle`, `includes`, `excludes`, `highlights` | OPTIONAL | array \| null |
| `images` | OPTIONAL | `{ cover?, gallery? }` \| null |

---

## PUT /admin/events/{slug} – UpdateEventRequest

All fields OPTIONAL (partial update). Send only fields being changed.

---

## PUT /admin/agency-section – AgencySectionDto (full replace)

Replaces the entire agency section (singleton). Same JSON shape as GET response. Add card = include new member in `team` (omit `id` or send temporary id); delete card = send `team` without that member.

| Field | Required? | Type | Notes |
|-------|-----------|------|--------|
| `heading` | OPTIONAL | string \| null | |
| `subtitle` | OPTIONAL | string \| null | |
| `description` | OPTIONAL | string \| null | |
| `logoUrl` | OPTIONAL | string \| null | |
| `brandName` | OPTIONAL | string \| null | |
| `team` | OPTIONAL | array \| null | Full array; order preserved. |

### team[] (each element)

| Field | Required? | Type |
|-------|-----------|------|
| `id` | OPTIONAL | string | Omit for new cards; backend returns id on next GET. |
| `name` | **REQUIRED** | string |
| `title` | **REQUIRED** | string |
| `imageUrl` | **REQUIRED** | string |

**Response (200):** The saved object (same shape as GET /agency-section).

---

## Summary: Never miss these

- **POST /bookings:** `tourId`, `tourType`, `contact.fullName`, `contact.email`, `guests` (≥1 with `fullName`). Plus `tourDateId` for Group, `travelDate` for Private.
- **POST /payments:** `bookingCode`, `provider`.
- **Admin login:** `username`, `password`.
- **Create tour:** `slug`, `title`, `type`, `durationDays`, `basePrice`, `currency`, `locations`, `images`.
- **Create tour date:** `startDate`, `endDate`, `capacity`.
- **Booking status update:** `status`.
- **PUT /admin/agency-section:** For each team member: `name`, `title`, `imageUrl` (all required). Send full `team` array to add/remove cards.

If the frontend form or request is missing any of the above, add the field to the UI and include it in the request payload.
