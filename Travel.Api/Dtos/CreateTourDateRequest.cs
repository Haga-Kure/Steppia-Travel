namespace Travel.Api.Dtos;

public record CreateTourDateRequest(
    DateTime StartDate,
    DateTime EndDate,
    int Capacity,
    decimal? PriceOverride,
    string? Status
);
