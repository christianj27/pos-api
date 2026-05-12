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
