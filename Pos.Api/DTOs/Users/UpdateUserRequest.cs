namespace Pos.Api.DTOs.Users;

public record UpdateUserRequest(
    string Name,
    string Username,
    string? Password,
    string Role,
    bool IsActive
);
