namespace Travel.Api.Dtos;

public record UserAuthResponse(
    string Token,
    string UserId,
    string Email,
    string FullName,
    string? Phone,
    string Role,
    DateTime ExpiresAt
);
