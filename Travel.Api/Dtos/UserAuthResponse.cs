namespace Travel.Api.Dtos;

public record UserAuthResponse(
    string Token,
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string Role,
    DateTime ExpiresAt
);
