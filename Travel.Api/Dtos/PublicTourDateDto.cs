namespace Travel.Api.Dtos;

public record PublicTourDateDto(
    string Id,
    DateTime StartDate,
    DateTime EndDate,
    int AvailableSpots,
    decimal Price,
    string Currency
);
