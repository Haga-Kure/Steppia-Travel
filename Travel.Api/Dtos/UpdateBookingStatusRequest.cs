namespace Travel.Api.Dtos;

public record UpdateBookingStatusRequest(
    string Status // "PendingPayment" | "Confirmed" | "Cancelled" | "Expired"
);
