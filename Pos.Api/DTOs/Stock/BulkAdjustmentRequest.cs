namespace Pos.Api.DTOs.Stock;

public record BulkAdjustmentRequest(
    Guid LocationId,
    string Note,
    IEnumerable<BulkAdjustmentItem> Items
);

public record BulkAdjustmentItem(
    Guid ProductId,
    /// <summary>Positive = add stock; negative = remove stock.</summary>
    int AdjustmentQuantity,
    string? ContainerStatus
);
