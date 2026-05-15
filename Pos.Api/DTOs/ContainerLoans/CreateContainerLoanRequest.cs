namespace Pos.Api.DTOs.ContainerLoans;

public record CreateContainerLoanRequest(
    Guid CustomerId,
    Guid ProductId,
    int Quantity,
    string? Notes
);
