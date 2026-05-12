namespace Pos.Api.DTOs.Stock;

public record BulkTransferItem(
    Guid ProductId,
    string ContainerStatus,
    int Quantity
);

public record BulkTransferRequest(
    Guid FromLocationId,
    Guid ToLocationId,
    string? Note,
    IEnumerable<BulkTransferItem> Items
);
