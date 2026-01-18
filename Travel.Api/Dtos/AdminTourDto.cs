namespace Travel.Api.Dtos;

public record AdminTourDto(
    string Id,
    string Slug,
    string Title,
    string Type,
    string? Summary,
    string? Description,
    int DurationDays,
    decimal BasePrice,
    string Currency,
    List<string> Locations,
    List<TourImageDto> Images,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record TourImageDto(
    string Url,
    string? Alt,
    bool IsCover
);
