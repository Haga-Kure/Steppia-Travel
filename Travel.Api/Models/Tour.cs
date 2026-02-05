using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;

namespace Travel.Api.Models;

public class TourImage
{
    [BsonElement("url")] public string Url { get; set; } = default!;
    [BsonElement("alt")] public string? Alt { get; set; }
    [BsonElement("isCover")] public bool IsCover { get; set; }
}

public class TourAccommodation
{
    [BsonElement("hotelNights")] public int? HotelNights { get; set; }
    [BsonElement("campNights")] public int? CampNights { get; set; }
    [BsonElement("notes")] public string? Notes { get; set; }
}

/// <summary>Waypoint on the day's route (place + distance to next stop).</summary>
public class TourItineraryRouteWaypoint
{
    [BsonElement("place")] public string Place { get; set; } = string.Empty;
    [BsonElement("distanceToNextKm")] public int? DistanceToNextKm { get; set; }
}

public class TourItineraryItem
{
    [BsonElement("day")] public int Day { get; set; }
    [BsonElement("title")] public string? Title { get; set; }
    [BsonElement("notes")] public string? Notes { get; set; }
    [BsonElement("breakfast")] public string? Breakfast { get; set; }
    [BsonElement("lunch")] public string? Lunch { get; set; }
    [BsonElement("dinner")] public string? Dinner { get; set; }
    [BsonElement("accommodation")] public string? Accommodation { get; set; }
    [BsonElement("stay")] public string? Stay { get; set; }
    [BsonElement("distanceKm")] public int? DistanceKm { get; set; }
    [BsonElement("startPlace")] public string? StartPlace { get; set; }
    [BsonElement("endPlace")] public string? EndPlace { get; set; }
    [BsonElement("firstSegmentDistanceKm")] public int? FirstSegmentDistanceKm { get; set; }
    [BsonElement("routeWaypoints")] public List<TourItineraryRouteWaypoint>? RouteWaypoints { get; set; }
    [BsonElement("imageUrl")] public string? ImageUrl { get; set; }
}

public class TourLocation
{
    [BsonElement("name")] public string Name { get; set; } = string.Empty;
    [BsonElement("latitude")] public string? Latitude { get; set; }
    [BsonElement("longitude")] public string? Longitude { get; set; }
}

[BsonIgnoreExtraElements] // This tells MongoDB to ignore any fields in the database that aren't in this class
public class Tour
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("slug")] public string Slug { get; set; } = default!;
    [BsonElement("title")] public string Title { get; set; } = default!;
    [BsonElement("subtitle")] public string? Subtitle { get; set; }
    [BsonElement("type")] public string Type { get; set; } = default!; // "Private" | "Group"

    [BsonElement("summary")] public string? Summary { get; set; }
    [BsonElement("description")] public string? Description { get; set; } // Allow description field from DB
    [BsonElement("overview")] public string? Overview { get; set; }

    [BsonElement("durationDays")] public int DurationDays { get; set; }

    // Extra descriptive fields used by the frontend
    [BsonElement("nights")] public int? Nights { get; set; }
    [BsonElement("travelStyle")] public List<string>? TravelStyle { get; set; }
    [BsonElement("region")] public string? Region { get; set; }
    [BsonElement("totalDistanceKm")] public int? TotalDistanceKm { get; set; }
    [BsonElement("highlights")] public List<string>? Highlights { get; set; }
    [BsonElement("included")] public List<string>? Included { get; set; }
    [BsonElement("excluded")] public List<string>? Excluded { get; set; }
    [BsonElement("accommodation")] public TourAccommodation? Accommodation { get; set; }
    [BsonElement("itinerary")] public List<TourItineraryItem>? Itinerary { get; set; }
    [BsonElement("activities")] public List<string>? Activities { get; set; }
    [BsonElement("idealFor")] public List<string>? IdealFor { get; set; }
    [BsonElement("difficulty")] public string? Difficulty { get; set; }
    [BsonElement("groupSize")] public string? GroupSize { get; set; }

    [BsonElement("basePrice")] public decimal BasePrice { get; set; }
    [BsonElement("currency")] public string Currency { get; set; } = "USD";

    [BsonElement("locations")]
    [BsonSerializer(typeof(TourLocationListSerializer))]
    public List<TourLocation> Locations { get; set; } = new();
    [BsonElement("images")] public List<TourImage> Images { get; set; } = new();

    [BsonElement("isActive")] public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; }
    [BsonElement("bobbleTitle")] public string? BobbleTitle { get; set; }
}
