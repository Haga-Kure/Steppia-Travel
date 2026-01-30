using Travel.Api.Models;

namespace Travel.Api.Dtos;

public record TourDto(
    string Id,
    string Slug,
    string Title,
    string Type,
    string? Summary,
    string? Description,
    int DurationDays,
    int? Nights,
    string? Region,
    int? TotalDistanceKm,
    List<string>? TravelStyle,
    List<string>? Highlights,
    TourAccommodation? Accommodation,
    List<TourItineraryItem>? Itinerary,
    List<string>? Activities,
    List<string>? IdealFor,
    decimal BasePrice,
    string Currency,
    List<TourLocationDto> Locations,
    List<TourImageDto> Images,
    string? BobbleTitle
);

public record TourLocationDto(string Name, string? Latitude, string? Longitude);
