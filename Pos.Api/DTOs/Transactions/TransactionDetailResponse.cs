namespace Pos.Api.DTOs.Transactions;

public record PaymentDetailResponse(
    Guid Id,
    decimal Amount,
    string Method,
    string? ReferenceNo,
    DateTime PaidAt
);

public record ContainerReturnDetail(
    Guid ProductId,
    string ProductName,
    int Quantity
);

public record TransactionDetailResponse(
    Guid Id,
    string TransactionType,
    string Status,
    Guid? CustomerId,
    string? CustomerName,
    Guid StaffId,
    string StaffName,
    Guid LocationId,
    string LocationName,
    string PaymentMethod,
    string? Notes,
    DateTime CreatedAt,
    IEnumerable<TransactionItemResponse> Items,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DebtAmount,
    IEnumerable<PaymentDetailResponse> Payments,
    IEnumerable<ContainerReturnDetail> ContainerReturns
);
