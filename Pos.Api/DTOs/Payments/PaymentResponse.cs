namespace Pos.Api.DTOs.Payments;

public record PaymentResponse(
    Guid Id,
    Guid TransactionId,
    decimal Amount,
    string Method,
    string? ReferenceNo,
    DateTime PaidAt
);
