namespace Pos.Api.DTOs.Customers;

public record ProductCustomerPricingItem(
    Guid CustomerId,
    string CustomerName,
    bool IsConfidential,
    decimal CustomPrice
);

public record ProductCustomerPricingResponse(
    Guid ProductId,
    string ProductName,
    string Unit,
    decimal BasePrice,
    IEnumerable<ProductCustomerPricingItem> Items
);
