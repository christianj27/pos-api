namespace Pos.Api.DTOs.Stock;

public record VendorExchangeRequest(
    Guid LocationId,
    Guid ProductId,
    int QtyEmptyOut,
    int QtyFilledIn,
    decimal PurchaseCost,
    string? Note
);
