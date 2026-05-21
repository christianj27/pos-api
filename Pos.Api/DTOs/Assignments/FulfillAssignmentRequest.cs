namespace Pos.Api.DTOs.Assignments;

public record FulfillAssignmentRequest(
    decimal PaidAmount,
    string PaymentMethod,
    string? Notes,
    IEnumerable<FulfillItemRequest>? Items,
    IEnumerable<ContainerReturnItem>? ContainerReturns,
    decimal? DebtPaymentAmount
);

/// <summary>
/// Actual items delivered by the kurir. When provided, overrides the assignment's
/// stored items so stock deduction and transaction total reflect the real delivery.
/// If omitted, falls back to the assignment's original items.
/// </summary>
public record FulfillItemRequest(Guid ProductId, int Quantity, decimal UnitPrice);

public record ContainerReturnItem(Guid ProductId, int Quantity);
