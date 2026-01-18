namespace Travel.Api.Dtos;

public record TourDto(
    string Id,
    string Slug,
    string Title,
    string Type,
    string? Summary,
    int DurationDays,
    decimal BasePrice,
    string Currency,
    List<string> Locations
);
