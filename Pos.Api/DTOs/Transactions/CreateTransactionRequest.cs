namespace Pos.Api.DTOs.Transactions;

public record TransactionItemRequest(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice
);

public record ContainerReturnRequest(
    Guid ProductId,
    int Quantity
);

public record CreateTransactionRequest(
    string TransactionType,
    Guid? CustomerId,
    Guid LocationId,
    IEnumerable<TransactionItemRequest> Items,
    decimal PaidAmount,
    string PaymentMethod,
    string? Notes,
    IEnumerable<ContainerReturnRequest>? ContainerReturns,
    decimal? DebtPaymentAmount
);

/// <summary>
/// Correction of a transaction while its business date is still open (FR-STL-008). Customers and
/// cancellation are deliberately not editable here. <c>Reason</c> is mandatory and audited.
/// </summary>
public record EditTransactionRequest(
    IEnumerable<TransactionItemRequest> Items,
    decimal PaidAmount,
    string PaymentMethod,
    string? ReferenceNo,
    string? Notes,
    string Reason
);
