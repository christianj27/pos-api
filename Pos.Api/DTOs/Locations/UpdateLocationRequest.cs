namespace Pos.Api.DTOs.Locations;

public record UpdateLocationRequest(
    string Name,
    Guid? AssignedTo,
    bool IsActive
);
