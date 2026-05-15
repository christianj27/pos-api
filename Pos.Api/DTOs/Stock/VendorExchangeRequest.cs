namespace Pos.Api.DTOs.Stock;

public record VendorExchangeRequest(
    Guid LocationId,
    Guid ProductId,
    int EmptyQuantity,
    int FilledQuantity,
    decimal PurchaseCost,
    string? Note
);
