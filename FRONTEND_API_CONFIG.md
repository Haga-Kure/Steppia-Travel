# Frontend API Configuration

## Base URL

```typescript
export const API_BASE_URL = 'https://steppia-travel-production.up.railway.app';
```

## API Endpoints Summary

```typescript
const API_ENDPOINTS = {
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
