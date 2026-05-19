namespace Pos.Api.DTOs.Stock;

public record StockMovementResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string MovementType,
    string ContainerStatus,
    int Quantity,
    Guid? FromLocationId,
    string? FromLocationName,
    Guid? ToLocationId,
    string? ToLocationName,
    decimal? PurchaseCost,
    string? Note,
    string CreatedByName,
    DateTime CreatedAt,
    string? CustomerName
);
