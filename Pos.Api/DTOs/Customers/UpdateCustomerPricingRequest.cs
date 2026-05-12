namespace Pos.Api.DTOs.Customers;

public record CustomerPricingItemRequest(Guid ProductId, decimal? CustomPrice);

public record UpdateCustomerPricingRequest(IEnumerable<CustomerPricingItemRequest> Items);
