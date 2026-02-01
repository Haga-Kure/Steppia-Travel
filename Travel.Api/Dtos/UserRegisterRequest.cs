namespace Travel.Api.Dtos;

public record UserRegisterRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    string Password
);
