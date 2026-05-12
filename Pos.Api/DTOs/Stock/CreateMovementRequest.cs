namespace Pos.Api.DTOs.Stock;

public record CreateMovementRequest(
    Guid ProductId,
    string MovementType,
    string ContainerStatus,
    int Quantity,
    Guid? FromLocationId,
    Guid? ToLocationId,
    decimal? PurchaseCost,
    string? Note
);
