# data.json (MongoDB export) vs Tour model – checklist

Comparison of your exported tour documents with our C# Tour / itinerary classes.

---

## Match (export has it, model has it)

| Export field | Model | Notes |
|--------------|--------|------|
| `slug` | `Tour.Slug` | OK |
| `title` | `Tour.Title` | OK |
| `type` | `Tour.Type` | OK |
| `summary` | `Tour.Summary` | OK |
| `subtitle` | `Tour.Subtitle` | OK |
| `description` | `Tour.Description` | OK |
| `overview` | `Tour.Overview` | OK |
| `durationDays` | `Tour.DurationDays` | OK |
| `nights` | `Tour.Nights` | OK |
| `basePrice` | `Tour.BasePrice` | OK |
| `currency` | `Tour.Currency` | OK |
| `locations` | `Tour.Locations` | `{ name, latitude, longitude }` – **latitude/longitude** can be numbers in MongoDB; serializer now accepts both number and string. |
| `images` | `Tour.Images` | `{ url, alt, isCover }` – OK |
| `highlights` | `Tour.Highlights` | OK |
| `travelStyle` | `Tour.TravelStyle` | OK |
| `region` | `Tour.Region` | OK |
| `totalDistanceKm` | `Tour.TotalDistanceKm` | OK |
| `bobbleTitle` | `Tour.BobbleTitle` | OK |
| `accommodation` | `Tour.Accommodation` | `{ hotelNights, campNights, notes }` – OK |
| `activities` | `Tour.Activities` | OK |
| `idealFor` | `Tour.IdealFor` | OK |
| `difficulty` | `Tour.Difficulty` | OK |
| `itinerary` | `Tour.Itinerary` | See itinerary table below. |
| `isActive` | `Tour.IsActive` | OK |

### Itinerary item (each day)

| Export field | Model | Notes |
|--------------|--------|------|
| `day` | `TourItineraryItem.Day` | OK |
| `title` | `TourItineraryItem.Title` | OK |
| `notes` | `TourItineraryItem.Notes` | OK |
| `breakfast` | `TourItineraryItem.Breakfast` | OK |
| `lunch` | `TourItineraryItem.Lunch` | OK |
| `dinner` | `TourItineraryItem.Dinner` | OK |
| `accommodation` | `TourItineraryItem.Accommodation` | OK |
| `stay` | `TourItineraryItem.Stay` | OK |
| `distanceKm` | `TourItineraryItem.DistanceKm` | OK (optional) |
| `startPlace` | `TourItineraryItem.StartPlace` | OK |
| `endPlace` | `TourItineraryItem.EndPlace` | OK |
| `routeWaypoints` | `TourItineraryItem.RouteWaypoints` | `[{ place, distanceToNextKm }]` – OK |
| `imageUrl` | `TourItineraryItem.ImageUrl` | OK |
| `firstSegmentDistanceKm` | `TourItineraryItem.FirstSegmentDistanceKm` | Optional; not in your export – fine. |

---

## In model but not in your export (optional or backend-only)

- **`_id`** – MongoDB adds this; omit in JSON if you re-import and let the backend generate new IDs, or keep for reference.
- **`createdAt`**, **`updatedAt`** – Not in your export. If you **re-import** this JSON (e.g. seed or restore), the API or import script should set these to `DateTime.UtcNow` when inserting; otherwise they default to `0001-01-01`.
- **`included`**, **`excluded`** – Optional lists; your export doesn’t have them – OK.
- **`groupSize`** – Optional string; your export doesn’t have it – OK.

---

## Fix applied in code

- **Locations `latitude` / `longitude`** – Your export (and likely MongoDB) has them as **numbers**. The custom locations serializer previously only read **strings**, so numeric values were effectively ignored. It is now updated to accept **both number and string** and convert numbers to string so `TourLocation.Latitude` / `Longitude` are set correctly.

---

## Re-importing data.json

If you re-import this file into MongoDB:

1. Ensure each document has an `_id` (ObjectId) or let the driver generate one.
2. Add **`createdAt`** and **`updatedAt`** (ISO date) when inserting, e.g. `new Date()` or current UTC string, so the Tour model has valid timestamps.
3. Keep **`locations`** as objects with **`name`** and optionally **`latitude`** / **`longitude`** (number or string both work now).
4. Keep **`itinerary`** with the same shape (including **`routeWaypoints`** and **`imageUrl`**); nothing is missing for the current model.
