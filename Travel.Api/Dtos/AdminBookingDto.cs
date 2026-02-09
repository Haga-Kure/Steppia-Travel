using Travel.Api.Models;

namespace Travel.Api.Dtos;

public record AdminBookingDto(
    string Id,
    string BookingCode,
    BookingStatus Status,
    DateTime? ExpiresAt,
    string TourId,
    TourInfo? Tour,
    DateTime? TravelDate,
    string TourType,
    BookingContactDto Contact,
    List<BookingGuestDto> Guests,
    int GuestCount,
    BookingPricingDto Pricing,
    string? SpecialRequests,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record BookingContactDto(
    string FullName,
    string Email,
    string? Phone,
    string? Country
);

public record BookingGuestDto(
    string FullName,
    int? Age,
    string? PassportNo
);

public record BookingPricingDto(
    string Currency,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal Total
);

public record TourInfo(
    string Id,
    string Title,
    string Slug,
    decimal BasePrice,
    string Currency,
    List<TourImageDto> Images
);
