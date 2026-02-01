namespace Travel.Api.Dtos;

/// <summary>
/// Same shape for both user and admin login. Frontend uses Role to show admin section or user section.
/// </summary>
public record UnifiedLoginResponse(
    string Token,
    DateTime ExpiresAt,
    string Role,
    string? UserId,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Phone
);
