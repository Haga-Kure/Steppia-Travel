# Events API

This document describes the Events API used by the Travel backend. The API
stores events in MongoDB collection `events`.

## Base URL

Use the same base URL as the Travel API (Railway deployment or local).

## Authentication

Public endpoints require no auth.
Admin endpoints require a JWT with role `admin`:

- Header: `Authorization: Bearer <token>`

## Data Model (MongoDB)

```
{
  _id: ObjectId,
  slug: String,
  title: String,
  event: { name: String, type: String, year: Number },
  summary: String,
  description: String,
  durationDays: Number,
  nights: Number,
  startDate: Date,
  endDate: Date,
  bestSeason: String,
  region: String,
  locations: [String],
  travelStyle: [String],
  difficulty: String,
  groupType: String,
  maxGroupSize: Number,
  priceUSD: Number,
  includes: [String],
  excludes: [String],
  highlights: [String],
  images: { cover: String, gallery: [String] },
  isActive: Boolean,
  createdAt: Date,
  updatedAt: Date
}
```

Notes:
- `slug` is unique.
- `event.type` is free-form (suggested values: National, Cultural, Sports, Music).
- `isActive=false` means soft-deleted / hidden.

## Public Endpoints

### 1) List Events

`GET /events`

Query params:
- `page` (int, default 1)
- `pageSize` (int, default 20, max 100)
- `search` (string, optional) - matches title, summary, description, event.name
- `type` (string, optional) - filters by event.type
- `year` (int, optional) - filters by event.year
- `region` (string, optional)

Response:
```
{
  "data": [ EventDto ],
  "page": 1,
  "pageSize": 20,
  "total": 42,
  "totalPages": 3
}
```

### 2) Get Event by Slug

`GET /events/{slug}`

Response:
- 200 + `EventDto` if found and active
- 404 if not found or inactive

## Admin Endpoints (AdminOnly)

### 1) List All Events (including inactive)

`GET /admin/events`

Query params:
- `page` (int, default 1)
- `pageSize` (int, default 20, max 100)

Response:
```
{
  "data": [ AdminEventDto ],
  "page": 1,
  "pageSize": 20,
  "total": 42,
  "totalPages": 3
}
```

### 2) Get Event by Slug (including inactive)

`GET /admin/events/{slug}`

Response:
- 200 + `AdminEventDto`
- 404 if not found

### 3) Create Event

`POST /admin/events`

Body (`CreateEventRequest`):
```
{
  "slug": "naadam-festival-2026",
  "title": "Naadam Festival 2026",
  "event": { "name": "Naadam Festival", "type": "Cultural", "year": 2026 },
  "summary": "Short summary",
  "description": "Full description",
  "durationDays": 7,
  "nights": 6,
  "startDate": "2026-07-10T00:00:00Z",
  "endDate": "2026-07-17T00:00:00Z",
  "bestSeason": "Summer",
  "region": "Central Mongolia",
  "locations": ["Ulaanbaatar", "Kharkhorin"],
  "travelStyle": ["Festival", "Cultural"],
  "difficulty": "Easy",
  "groupType": "Group",
  "maxGroupSize": 20,
  "priceUSD": 1299,
  "includes": ["Hotel", "Transport"],
  "excludes": ["Flights"],
  "highlights": ["Opening ceremony", "Horse racing"],
  "images": { "cover": "https://...", "gallery": ["https://..."] }
}
```

Response:
- 201 + `AdminEventDto`
- 400 if slug already exists or required fields missing

### 4) Update Event (by slug)

`PUT /admin/events/{slug}`

Body (`UpdateEventRequest`) - all fields optional:
```
{
  "title": "Updated title",
  "event": { "name": "Naadam Festival", "type": "Cultural", "year": 2026 },
  "priceUSD": 1399,
  "isActive": true
}
```

Response:
- 200 + `AdminEventDto`
- 404 if not found
- 400 if slug conflict

### 5) Delete Event (Soft Delete, by slug)

`DELETE /admin/events/{slug}`

Behavior:
- Sets `isActive=false`

Response:
```
{ "message": "Event deactivated successfully." }
```

## DTO Shapes

### EventDto
```
{
  "id": "string",
  "slug": "string",
  "title": "string",
  "event": { "name": "string", "type": "string", "year": 2026 },
  "summary": "string",
  "description": "string",
  "durationDays": 7,
  "nights": 6,
  "startDate": "2026-07-10T00:00:00Z",
  "endDate": "2026-07-17T00:00:00Z",
  "bestSeason": "string",
  "region": "string",
  "locations": ["string"],
  "travelStyle": ["string"],
  "difficulty": "string",
  "groupType": "string",
  "maxGroupSize": 20,
  "priceUSD": 1299,
  "includes": ["string"],
  "excludes": ["string"],
  "highlights": ["string"],
  "images": { "cover": "string", "gallery": ["string"] }
}
```

### AdminEventDto (adds metadata)
```
{
  "id": "string",
  "slug": "string",
  "title": "string",
  "event": { "name": "string", "type": "string", "year": 2026 },
  "summary": "string",
  "description": "string",
  "durationDays": 7,
  "nights": 6,
  "startDate": "2026-07-10T00:00:00Z",
  "endDate": "2026-07-17T00:00:00Z",
  "bestSeason": "string",
  "region": "string",
  "locations": ["string"],
  "travelStyle": ["string"],
  "difficulty": "string",
  "groupType": "string",
  "maxGroupSize": 20,
  "priceUSD": 1299,
  "includes": ["string"],
  "excludes": ["string"],
  "highlights": ["string"],
  "images": { "cover": "string", "gallery": ["string"] },
  "isActive": true,
  "createdAt": "2026-01-01T00:00:00Z",
  "updatedAt": "2026-01-01T00:00:00Z"
}
```
