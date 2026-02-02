using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Travel.Api.Models;

public class EventInfo
{
    [BsonElement("name")] public string Name { get; set; } = default!;
    [BsonElement("type")] public string Type { get; set; } = default!;
    [BsonElement("year")] public int Year { get; set; }
}

public class EventImages
{
    [BsonElement("cover")] public string? Cover { get; set; }
    [BsonElement("gallery")] public List<string>? Gallery { get; set; }
}

[BsonIgnoreExtraElements]
public class Event
{
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("slug")] public string Slug { get; set; } = default!;
    [BsonElement("title")] public string Title { get; set; } = default!;
    [BsonElement("event")] public EventInfo EventDetails { get; set; } = default!;
    [BsonElement("summary")] public string? Summary { get; set; }
    [BsonElement("description")] public string? Description { get; set; }

    [BsonElement("durationDays")] public int DurationDays { get; set; }
    [BsonElement("nights")] public int? Nights { get; set; }

    [BsonElement("startDate")] public DateTime? StartDate { get; set; }
    [BsonElement("endDate")] public DateTime? EndDate { get; set; }
    [BsonElement("bestSeason")] public string? BestSeason { get; set; }

    [BsonElement("region")] public string? Region { get; set; }
    [BsonElement("locations")] public List<string>? Locations { get; set; }

    [BsonElement("travelStyle")] public List<string>? TravelStyle { get; set; }
    [BsonElement("difficulty")] public string? Difficulty { get; set; }

    [BsonElement("groupType")] public string? GroupType { get; set; }
    [BsonElement("maxGroupSize")] public int? MaxGroupSize { get; set; }

    [BsonElement("priceUSD")] public decimal PriceUSD { get; set; }

    [BsonElement("includes")] public List<string>? Includes { get; set; }
    [BsonElement("excludes")] public List<string>? Excludes { get; set; }

    [BsonElement("highlights")] public List<string>? Highlights { get; set; }

    [BsonElement("images")] public EventImages? Images { get; set; }

    [BsonElement("isActive")] public bool IsActive { get; set; } = true;
    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; }
}
