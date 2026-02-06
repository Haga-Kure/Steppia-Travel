using System.Text.Json.Serialization;
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
    List<ItineraryDayDto>? Itinerary,
    List<string>? Activities,
    List<string>? IdealFor,
    decimal BasePrice,
    string Currency,
    List<TourLocationDto> Locations,
    List<TourImageDto> Images,
    string? BobbleTitle
);

public record TourLocationDto(string Name, string? Latitude, string? Longitude);

/// <summary>Waypoint on the day's route (place + distance to next stop). Matches frontend ItineraryRouteWaypointApiResponse.</summary>
public record ItineraryRouteWaypointDto(
    [property: JsonPropertyName("place")] string Place,
    [property: JsonPropertyName("distanceToNextKm")] int? DistanceToNextKm
);

/// <summary>Single day in tour itinerary. Matches frontend ItineraryDayApiResponse.</summary>
public record ItineraryDayDto(
    [property: JsonPropertyName("day")] int Day,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("breakfast")] string? Breakfast,
    [property: JsonPropertyName("lunch")] string? Lunch,
    [property: JsonPropertyName("dinner")] string? Dinner,
    [property: JsonPropertyName("accommodation")] string? Accommodation,
    [property: JsonPropertyName("stay")] string? Stay,
    [property: JsonPropertyName("distanceKm")] int? DistanceKm,
    [property: JsonPropertyName("startPlace")] string? StartPlace,
    [property: JsonPropertyName("endPlace")] string? EndPlace,
    [property: JsonPropertyName("firstSegmentDistanceKm")] int? FirstSegmentDistanceKm,
    [property: JsonPropertyName("routeWaypoints")] List<ItineraryRouteWaypointDto>? RouteWaypoints,
    [property: JsonPropertyName("imageUrl")] string? ImageUrl
);
