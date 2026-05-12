namespace Pos.Api.DTOs.Locations;

public record CreateLocationRequest(
    string Name,
    string Type,
    Guid? AssignedTo
);
