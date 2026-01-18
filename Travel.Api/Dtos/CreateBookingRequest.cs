namespace Travel.Api.Dtos;

public record CreateBookingRequest(
    string TourId,
    string TourType,              // "Private" | "Group"
    string? TourDateId,           // required for Group
    DateTime? TravelDate,         // required for Private
    CreateBookingContact Contact,
    List<CreateBookingGuest> Guests,
    string? SpecialRequests
);

public record CreateBookingContact(
    string FullName,
    string Email,
    string? Phone,
    string? Country
);

public record CreateBookingGuest(
    string FullName,
    int? Age,
    string? PassportNo
);
