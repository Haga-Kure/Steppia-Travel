namespace Travel.Api.Dtos;

public record LoginResponse(
    string Token,
    string Username,
    string Role,
    DateTime ExpiresAt
);
