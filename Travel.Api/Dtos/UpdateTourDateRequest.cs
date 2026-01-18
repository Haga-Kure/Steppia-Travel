namespace Travel.Api.Dtos;

public record UpdateTourDateRequest(
    DateTime? StartDate,
    DateTime? EndDate,
    int? Capacity,
    decimal? PriceOverride,
    string? Status
);
