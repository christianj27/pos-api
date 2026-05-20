namespace Pos.Api.DTOs.Customers;

public record DebtTransactionItem(
    Guid Id,
    DateTime CreatedAt,
    string Type,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DebtAmount,
    string CreatedByName
);

public record DebtPaymentHistoryItem(
    Guid Id,
    decimal Amount,
    string? Note,
    string CreatedByName,
    DateTime CreatedAt
);

public record CustomerDebtHistoryResponse(
    Guid CustomerId,
    string CustomerName,
    decimal InitialDebt,
    decimal OutstandingDebt,
    IEnumerable<DebtTransactionItem> DebtTransactions,
    IEnumerable<DebtPaymentHistoryItem> Payments
);
