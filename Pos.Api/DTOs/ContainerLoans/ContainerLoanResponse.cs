namespace Pos.Api.DTOs.ContainerLoans;

public record ContainerLoanResponse(
    Guid Id,
    Guid? TransactionId,
    Guid CustomerId,
    string CustomerName,
    Guid ProductId,
    string ProductName,
    string ProductUnit,
    int Quantity,
    string CreatedByName,
    DateTime CreatedAt
);
