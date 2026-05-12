namespace Pos.Api.DTOs.Locations;

public record LocationResponse(
    Guid Id,
    string Name,
    string Type,
    Guid? AssignedTo,
    string? AssignedUserName,
    bool IsActive,
    DateTime CreatedAt
);
