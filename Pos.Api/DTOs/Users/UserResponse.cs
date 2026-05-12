namespace Pos.Api.DTOs.Users;

public record UserResponse(
    Guid Id,
    string Name,
    string Username,
    string Role,
    bool IsActive,
    DateTime CreatedAt
);
