namespace Pos.Api.DTOs.Products;

public record CreateProductRequest(
    string Name,
    string Category,
    string? ProductionType,
    string Type,
    string Unit,
    decimal BasePrice
);
