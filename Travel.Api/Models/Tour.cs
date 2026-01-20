using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

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

public class TourItineraryItem
{
    [BsonElement("day")] public int Day { get; set; }
    [BsonElement("title")] public string? Title { get; set; }
    [BsonElement("notes")] public string? Notes { get; set; }
    [BsonElement("stay")] public string? Stay { get; set; }
    [BsonElement("distanceKm")] public int? DistanceKm { get; set; }
}

[BsonIgnoreExtraElements] // This tells MongoDB to ignore any fields in the database that aren't in this class
public class Tour
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("slug")] public string Slug { get; set; } = default!;
    [BsonElement("title")] public string Title { get; set; } = default!;
    [BsonElement("type")] public string Type { get; set; } = default!; // "Private" | "Group"

    [BsonElement("summary")] public string? Summary { get; set; }
    [BsonElement("description")] public string? Description { get; set; } // Allow description field from DB

    [BsonElement("durationDays")] public int DurationDays { get; set; }

    // Extra descriptive fields used by the frontend
    [BsonElement("nights")] public int? Nights { get; set; }
    [BsonElement("travelStyle")] public List<string>? TravelStyle { get; set; }
    [BsonElement("region")] public string? Region { get; set; }
    [BsonElement("totalDistanceKm")] public int? TotalDistanceKm { get; set; }
    [BsonElement("highlights")] public List<string>? Highlights { get; set; }
    [BsonElement("accommodation")] public TourAccommodation? Accommodation { get; set; }
    [BsonElement("itinerary")] public List<TourItineraryItem>? Itinerary { get; set; }
    [BsonElement("activities")] public List<string>? Activities { get; set; }
    [BsonElement("idealFor")] public List<string>? IdealFor { get; set; }

    [BsonElement("basePrice")] public decimal BasePrice { get; set; }
    [BsonElement("currency")] public string Currency { get; set; } = "USD";

    [BsonElement("locations")] public List<string> Locations { get; set; } = new();
    [BsonElement("images")] public List<TourImage> Images { get; set; } = new();

    [BsonElement("isActive")] public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; }
}
