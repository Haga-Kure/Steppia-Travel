namespace Travel.Api.Dtos;

public record CreateBookingResponse(
    string BookingId,
    string BookingCode,
    string Status,
    DateTime ExpiresAt,
    decimal Total,
    string Currency
);
