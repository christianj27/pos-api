namespace Pos.Api.DTOs.Assignments;

public record FulfillAssignmentRequest(
    decimal PaidAmount,
    string PaymentMethod,
    string? Notes,
    IEnumerable<ContainerReturnItem>? ContainerReturns,
    decimal? DebtPaymentAmount
);

public record ContainerReturnItem(Guid ProductId, int Quantity);
