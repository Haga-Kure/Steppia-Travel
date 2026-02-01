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
  `{ "token": "<JWT>", "expiresAt": "...", "role": "user"|"admin", "userId", "username", "email", "firstName", "lastName", "phone" }`  
  (Nullable fields may be `null` depending on role.)

- **No GET /users/me:**  
  The frontend does **not** call GET /users/me (or any “current user” endpoint) to verify the token. It only uses the JWT from the login response. The backend validates the JWT on each request; a “me” endpoint is not required for this frontend.

- **Admin APIs:**  
  Same JWT in `Authorization: Bearer <token>`. The backend requires role **admin**: **403** if not admin, **401** if token is missing or invalid.

See `FRONTEND_AUTH_FOR_BACKEND.md` in the frontend repo for full detail.

## User registration (email confirmation)

User sign-up is **two steps**. The user is only created after they confirm their email with the 6-digit code.

### Step 1: Register (send code)

- **Endpoint:** `POST /user/register`
- **Body:**  
  `{ "email": "...", "firstName": "...", "lastName": "...", "phone": "..." (optional), "password": "..." }`
- **Backend:** Generates a random **6-digit code**, stores a pending registration (expires in 15 minutes), and **sends the code to the given email** via SMTP.
- **Response (200):**  
  `{ "message": "Confirmation code sent to your email. Check your inbox and confirm with the 6-digit code.", "expiresInMinutes": 15 }`
- **Frontend:** Show a “Check your email” message and a form to enter the **6-digit code** (same email as in step 1).

### Step 2: Confirm email (create user and log in)

- **Endpoint:** `POST /user/confirm-email`
- **Body:**  
  `{ "email": "<same email as step 1>", "code": "123456" }`  
  `code` must be exactly 6 digits (string or number).
- **Backend:** Finds pending registration by email; if code matches and not expired, **creates the user**, deletes the pending record, issues a JWT, and returns the same auth shape as login.
- **Response (200):**  
  `{ "token": "<JWT>", "userId", "email", "firstName", "lastName", "phone", "role": "user", "expiresAt": "..." }`
- **Frontend:** Store `token` (and user info), then redirect to the logged-in user area (same as after login).

### Errors

- **Register:** 409 if email already registered.
- **Confirm:** 404 if no pending registration for that email; 400 if code wrong or expired (“Confirmation code expired. Please register again.” or “Invalid confirmation code.”).

Tell the frontend agent: **User registration is two-step: call POST /user/register, then have the user enter the 6-digit code from their email and call POST /user/confirm-email with that email and code to create the account and get the token.**

## API Endpoints Summary

```typescript
const API_ENDPOINTS = {
  // Auth (single login for user and admin)
  authLogin: `${API_BASE_URL}/auth/login`,
  // User registration (two-step: register then confirm-email)
  userRegister: `${API_BASE_URL}/user/register`,
  userConfirmEmail: `${API_BASE_URL}/user/confirm-email`,
  
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
