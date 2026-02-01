namespace Travel.Api.Dtos;

public record UserLoginRequest(
    string Email,
    string Password
);
