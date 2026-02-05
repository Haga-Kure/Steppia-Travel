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
    List<TourLocationRequest> Locations,
    List<TourImageRequest> Images,
    List<TourItineraryItemRequest>? Itinerary
);

public record TourLocationRequest(string Name, string? Latitude, string? Longitude);

public record TourImageRequest(
    string Url,
    string? Alt,
    bool IsCover
);

public record TourAccommodationRequest(int? HotelNights, int? CampNights, string? Notes);

/// <summary>Waypoint on the day's route (place + distance to next stop).</summary>
public record TourItineraryRouteWaypointRequest(string Place, int? DistanceToNextKm);

public record TourItineraryItemRequest(
    int Day,
    string? Title,
    string? Notes,
    string? Breakfast,
    string? Lunch,
    string? Dinner,
    string? Accommodation,
    string? Stay,
    int? DistanceKm,
    string? StartPlace,
    string? EndPlace,
    int? FirstSegmentDistanceKm,
    List<TourItineraryRouteWaypointRequest>? RouteWaypoints,
    string? ImageUrl
);
