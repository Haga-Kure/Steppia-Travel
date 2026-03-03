using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Travel.Api.Models;

/// <summary>Team member card in the agency section.</summary>
public class AgencyTeamMember
{
    [BsonElement("id")] public string? Id { get; set; }
    [BsonElement("name")] public string Name { get; set; } = default!;
    [BsonElement("title")] public string Title { get; set; } = default!;
    [BsonElement("imageUrl")] public string ImageUrl { get; set; } = default!;
}

/// <summary>Singleton document for the "Our agency" section (heading, subtitle, description, logo, brand, team cards).</summary>
[BsonIgnoreExtraElements]
public class AgencySection
{
    /// <summary>Fixed id so we have exactly one document in the collection.</summary>
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = "agency-section-singleton";

    [BsonElement("heading")] public string? Heading { get; set; }
    [BsonElement("subtitle")] public string? Subtitle { get; set; }
    [BsonElement("description")] public string? Description { get; set; }
    [BsonElement("logoUrl")] public string? LogoUrl { get; set; }
    [BsonElement("brandName")] public string? BrandName { get; set; }
    [BsonElement("team")] public List<AgencyTeamMember>? Team { get; set; }
}
