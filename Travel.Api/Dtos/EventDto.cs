namespace Travel.Api.Dtos;

public record EventDto(
    string Id,
    string Slug,
    string Title,
    EventInfoDto Event,
    string? Summary,
    string? Description,
    int DurationDays,
    int? Nights,
    DateTime? StartDate,
    DateTime? EndDate,
    string? BestSeason,
    string? Region,
    List<string>? Locations,
    List<string>? TravelStyle,
    string? Difficulty,
    string? GroupType,
    int? MaxGroupSize,
    decimal PriceUSD,
    List<string>? Includes,
    List<string>? Excludes,
    List<string>? Highlights,
    EventImagesDto? Images
);

public record AdminEventDto(
    string Id,
    string Slug,
    string Title,
    EventInfoDto Event,
    string? Summary,
    string? Description,
    int DurationDays,
    int? Nights,
    DateTime? StartDate,
    DateTime? EndDate,
    string? BestSeason,
    string? Region,
    List<string>? Locations,
    List<string>? TravelStyle,
    string? Difficulty,
    string? GroupType,
    int? MaxGroupSize,
    decimal PriceUSD,
    List<string>? Includes,
    List<string>? Excludes,
    List<string>? Highlights,
    EventImagesDto? Images,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record EventInfoDto(string Name, string Type, int Year);
public record EventImagesDto(string? Cover, List<string>? Gallery);
