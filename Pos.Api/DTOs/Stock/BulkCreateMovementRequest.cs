namespace Pos.Api.DTOs.Stock;

public record BulkMovementItem(
    Guid ProductId,
    string ContainerStatus,
    int Quantity,
    decimal? PurchaseCost
);

public record BulkCreateMovementRequest(
    string MovementType,
    Guid ToLocationId,
    string? Note,
    IEnumerable<BulkMovementItem> Items
);
