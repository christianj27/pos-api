namespace Pos.Api.DTOs.Products;

public record UpdateProductRequest(
    string Name,
    string Category,
    string? ProductionType,
    string Type,
    string Unit,
    decimal BasePrice,
    bool IsActive
);
