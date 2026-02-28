namespace Travel.Api.Dtos;

/// <summary>
/// Payload for Telegram booking notification (e.g. when customer proceeds to payment).
/// Matches the Angular TelegramNotificationService payload.
/// </summary>
public record NotifyBookingRequest(
    string? BookingCode,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? TourName,
    string? TourSlug,
    string? Amount,
    int? Travelers
);
