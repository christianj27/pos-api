namespace Pos.Api.DTOs.DebtPayments;

public record DebtPaymentResponse(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    decimal Amount,
    string Method,
    string? ReferenceNo,
    string? Note,
    string CreatedByName,
    DateTime CreatedAt
);
