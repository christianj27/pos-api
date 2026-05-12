namespace Pos.Api.DTOs.Transactions;

public record TransactionItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductUnit,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal
);

public record TransactionResponse(
    Guid Id,
    string TransactionType,
    Guid? CustomerId,
    string? CustomerName,
    Guid StaffId,
    string StaffName,
    Guid LocationId,
    string LocationName,
    string Status,
    string PaymentMethod,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DebtAmount,
    string? Notes,
    DateTime CreatedAt,
    IEnumerable<TransactionItemResponse> Items
);
