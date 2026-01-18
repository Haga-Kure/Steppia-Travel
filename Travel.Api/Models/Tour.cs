using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Travel.Api.Models;

public class TourImage
{
    [BsonElement("url")] public string Url { get; set; } = default!;
    [BsonElement("alt")] public string? Alt { get; set; }
    [BsonElement("isCover")] public bool IsCover { get; set; }
}

public class Tour
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("slug")] public string Slug { get; set; } = default!;
    [BsonElement("title")] public string Title { get; set; } = default!;
    [BsonElement("type")] public string Type { get; set; } = default!; // "Private" | "Group"

    [BsonElement("summary")] public string? Summary { get; set; }
    [BsonElement("description")] public string? Description { get; set; } // Allow description field from DB

    [BsonElement("durationDays")] public int DurationDays { get; set; }

    [BsonElement("basePrice")] public decimal BasePrice { get; set; }
    [BsonElement("currency")] public string Currency { get; set; } = "USD";

    [BsonElement("locations")] public List<string> Locations { get; set; } = new();
    [BsonElement("images")] public List<TourImage> Images { get; set; } = new();

    [BsonElement("isActive")] public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; }
}
