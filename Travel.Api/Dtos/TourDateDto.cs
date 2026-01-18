namespace Travel.Api.Dtos;

public record TourDateDto(
    string Id,
    string TourId,
    DateTime StartDate,
    DateTime EndDate,
    int Capacity,
    decimal? PriceOverride,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
