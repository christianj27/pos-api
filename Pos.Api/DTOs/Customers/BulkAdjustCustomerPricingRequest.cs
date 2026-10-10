namespace Pos.Api.DTOs.Customers;

public record BulkAdjustCustomerPricingRequest(
    Guid ProductId,
    decimal Amount,
    IEnumerable<Guid> CustomerIds
);

public record BulkAdjustCustomerPricingItem(
    Guid CustomerId,
    string CustomerName,
    decimal OldPrice,
    decimal NewPrice
);

public record BulkAdjustCustomerPricingResponse(
    int UpdatedCount,
    IEnumerable<BulkAdjustCustomerPricingItem> Items
);
