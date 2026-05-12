namespace Pos.Api.DTOs.Stock;

public record BulkVendorExchangeItem(
    Guid ProductId,
    int EmptyQuantity,
    int FilledQuantity,
    decimal PurchaseCost
);

public record BulkVendorExchangeRequest(
    Guid LocationId,
    string? Note,
    IEnumerable<BulkVendorExchangeItem> Items
);
