namespace Pos.Api.DTOs.Users;

public record CreateUserRequest(
    string Name,
    string Username,
    string Password,
    string Role
);
