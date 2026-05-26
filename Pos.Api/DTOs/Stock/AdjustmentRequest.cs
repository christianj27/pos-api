namespace Pos.Api.DTOs.Stock;

public record AdjustmentRequest(
    Guid LocationId,
    Guid ProductId,
    /// <summary>Positive = add stock; negative = remove stock.</summary>
    int AdjustmentQuantity,
    string? ContainerStatus,
    string Note
);
