namespace Pos.Api.DTOs.DebtPayments;

public record CreateDebtPaymentRequest(
    Guid CustomerId,
    decimal Amount,
    string Method,
    string? ReferenceNo,
    string? Note
);
