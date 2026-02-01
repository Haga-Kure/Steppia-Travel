# Frontend API Configuration

## Base URL

```typescript
export const API_BASE_URL = 'https://steppia-travel-production.up.railway.app';
```

## Authentication (Frontend–Backend contract)

The backend supports a **single login** used for both users and admins. The frontend uses the response `role` to show the admin section or user section.

- **Single login:** `POST /auth/login`  
  Body: `{ "login": "<email or username>", "password": "..." }`
  - **User:** send **email** → backend looks up in `users` by email.
  - **Admin:** send **username** → backend looks up in `admins` by username.

- **Response (same for both):**  
  `{ "token": "<JWT>", "expiresAt": "...", "role": "user"|"admin", "userId", "username", "email", "fullName", "phone" }`  
  (Nullable fields may be `null` depending on role.)

- **No GET /users/me:**  
  The frontend does **not** call GET /users/me (or any “current user” endpoint) to verify the token. It only uses the JWT from the login response. The backend validates the JWT on each request; a “me” endpoint is not required for this frontend.

- **Admin APIs:**  
  Same JWT in `Authorization: Bearer <token>`. The backend requires role **admin**: **403** if not admin, **401** if token is missing or invalid.

See `FRONTEND_AUTH_FOR_BACKEND.md` in the frontend repo for full detail.

## API Endpoints Summary

```typescript
const API_ENDPOINTS = {
  // Auth (single login for user and admin)
  authLogin: `${API_BASE_URL}/auth/login`,
  
  // Health
  health: `${API_BASE_URL}/health/mongo`,
  
  // Tours
  tours: `${API_BASE_URL}/tours`,
  tourBySlug: (slug: string) => `${API_BASE_URL}/tours/${slug}`,
  
  // Bookings
  createBooking: `${API_BASE_URL}/bookings`,
  getBooking: (code: string) => `${API_BASE_URL}/bookings/${code}`,
  
  // Payments
  createPayment: `${API_BASE_URL}/payments`,
  paymentWebhook: `${API_BASE_URL}/payments/webhook`,
  
  // Documentation
  swagger: `${API_BASE_URL}/swagger`
};
```

## Example Usage

### Fetch Tours
```typescript
const response = await fetch('https://steppia-travel-production.up.railway.app/tours');
const tours = await response.json();
```

### Get Tour by Slug
```typescript
const slug = 'khovd-overland-multi-ethnic-altai-expedition-7d';
const response = await fetch(`https://steppia-travel-production.up.railway.app/tours/${slug}`);
const tour = await response.json();
```

### Create Booking
```typescript
const response = await fetch('https://steppia-travel-production.up.railway.app/bookings', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({
    tourId: '69693d293953f333a63ad670',
    tourType: 'Private',
    travelDate: '2024-12-25',
    contact: {
      fullName: 'John Doe',
      email: 'john@example.com',
      phone: '+1234567890',
      country: 'USA'
    },
    guests: [
      {
        fullName: 'John Doe',
        age: 30,
        passportNo: 'AB123456'
      }
    ]
  })
});
const booking = await response.json();
```

## Current Active Tours (for testing)

1. **Khovd Overland Multi-Ethnic & Altai Expedition**
   - ID: `69693d293953f333a63ad670`
   - Slug: `khovd-overland-multi-ethnic-altai-expedition-7d`
   - Price: $3090
   - Type: Group

2. **Bayan-Ulgii Overland Eagle & Altai Expedition**
   - ID: `69693d293953f333a63ad671`
   - Slug: `bayan-ulgii-overland-eagle-altai-expedition-7d`
   - Price: $3290
   - Type: Group

3. **South Gobi Overland Nomadic Adventure**
   - ID: `69693d293953f333a63ad673`
   - Slug: `south-gobi-overland-nomadic-adventure-7d`
   - Price: $2390
   - Type: Group

## Important Notes

- All endpoints return JSON
- Use `Content-Type: application/json` for POST requests
- Booking codes expire after 30 minutes
- Group tours require `tourDateId`
- Private tours require `travelDate`
- Check Swagger UI for interactive testing: https://steppia-travel-production.up.railway.app/swagger
