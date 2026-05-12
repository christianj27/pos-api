namespace Pos.Api.DTOs.Products;

public record ProductResponse(
    Guid Id,
    string Name,
    string Category,
    string? ProductionType,
    string Type,
    string Unit,
    decimal BasePrice,
    bool IsActive,
    DateTime CreatedAt
);
