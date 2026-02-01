namespace Travel.Api.Dtos;

public record ConfirmEmailRequest(
    string Email,
    string Code
);
