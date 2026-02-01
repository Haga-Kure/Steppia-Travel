namespace Travel.Api.Dtos;

/// <summary>
/// Single login endpoint: use email for user, username for admin.
/// </summary>
public record UnifiedLoginRequest(
    string Login,
    string Password
);
