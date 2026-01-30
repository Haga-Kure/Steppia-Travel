namespace Travel.Api.Dtos;

public record UpdateTourRequest(
    string? Slug,
    string? Title,
    string? Type,
    string? Summary,
    string? Description,
    int? DurationDays,
    decimal? BasePrice,
    string? Currency,
    List<TourLocationRequest>? Locations,
    List<TourImageRequest>? Images,
    bool? IsActive
);
