namespace Pos.Api.DTOs.Payments;

public record CreatePaymentRequest(
    decimal Amount,
    string Method,
    string? ReferenceNo
);
