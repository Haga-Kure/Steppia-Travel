namespace Travel.Api.Dtos;

public record UserRegisterRequest(
    string Email,
    string FullName,
    string? Phone,
    string Password
);
