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
    List<TourImageRequest> Images
);

public record TourLocationRequest(string Name, string? Latitude, string? Longitude);

public record TourImageRequest(
    string Url,
    string? Alt,
    bool IsCover
);

public record TourAccommodationRequest(int? HotelNights, int? CampNights, string? Notes);

public record TourItineraryItemRequest(int Day, string? Title, string? Notes, string? Stay, int? DistanceKm);
