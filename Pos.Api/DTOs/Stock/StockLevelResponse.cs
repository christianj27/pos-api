namespace Pos.Api.DTOs.Stock;

public record StockLevelResponse(
    Guid ProductId,
    string ProductName,
    string ProductUnit,
    string ProductCategory,
    Guid LocationId,
    string LocationName,
    int? QuantityFilled,
    int? QuantityEmpty,
    int? QuantityTotal
);
