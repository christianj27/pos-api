namespace Pos.Api.DTOs.Customers;

public record CustomerPricingItemResponse(
    Guid ProductId,
    string ProductName,
    string Unit,
    decimal BasePrice,
    decimal? CustomPrice
);

public record CustomerPricingResponse(
    Guid CustomerId,
    string CustomerName,
    IEnumerable<CustomerPricingItemResponse> Items
);
