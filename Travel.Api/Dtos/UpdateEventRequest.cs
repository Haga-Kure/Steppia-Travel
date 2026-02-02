namespace Travel.Api.Dtos;

public record UpdateEventRequest(
    string? Slug,
    string? Title,
    EventInfoRequest? Event,
    string? Summary,
    string? Description,
    int? DurationDays,
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
    decimal? PriceUSD,
    List<string>? Includes,
    List<string>? Excludes,
    List<string>? Highlights,
    EventImagesRequest? Images,
    bool? IsActive
);
