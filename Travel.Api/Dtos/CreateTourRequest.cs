namespace Travel.Api.Dtos;

public record CreateTourRequest(
    string Slug,
    string Title,
    string Type,
    string? Summary,
    string? Description,
    int DurationDays,
    decimal BasePrice,
    string Currency,
    List<string> Locations,
    List<TourImageRequest> Images
);

public record TourImageRequest(
    string Url,
    string? Alt,
    bool IsCover
);
