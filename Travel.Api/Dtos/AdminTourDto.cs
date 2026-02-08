using Travel.Api.Models;

namespace Travel.Api.Dtos;

public record AdminTourDto(
    string Id,
    string Slug,
    string Title,
    string Type,
    string? Summary,
    string? Description,
    string? Overview,
    string? Subtitle,
    string? BobbleTitle,
    int DurationDays,
    int? Nights,
    decimal BasePrice,
    string Currency,
    List<TourLocationDto> Locations,
    List<TourImageDto> Images,
    string? Region,
    int? TotalDistanceKm,
    List<string>? Highlights,
    List<string>? Included,
    List<string>? Excluded,
    List<string>? TravelStyle,
    List<string>? Activities,
    List<string>? IdealFor,
    string? Difficulty,
    string? GroupSize,
    List<ItineraryDayDto>? Itinerary,
    TourAccommodation? Accommodation,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record TourImageDto(
    string Url,
    string? Alt,
    bool IsCover
);
